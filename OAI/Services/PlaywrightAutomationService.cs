using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Threading.Tasks;
using Microsoft.Playwright;
using OAI.Data;
using OAI.Models;

namespace OAI.Services
{
    public class AutoFillResult
    {
        public bool Success { get; set; }
        public int FieldsFilled { get; set; }
        public bool ResumeUploaded { get; set; }
        public bool HardStopTriggered { get; set; }
        public string Message { get; set; } = string.Empty;
    }

    public class PlaywrightAutomationService : IAsyncDisposable
    {
        private IPlaywright? _playwright;
        private IBrowserContext? _browserContext;
        private IPage? _page;

        public event Action<string>? OnLogEmitted;

        public async Task InitializeBrowserAsync(bool nonHeadless = true, string channel = "chrome")
        {
            _playwright = await Playwright.CreateAsync();

            var userDataDir = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
                "OAI",
                "PlaywrightUserData"
            );

            Directory.CreateDirectory(userDataDir);

            Log($"Initializing Playwright browser context (Channel: {channel}, Non-Headless: {nonHeadless})...");

            var options = new BrowserTypeLaunchPersistentContextOptions
            {
                Headless = !nonHeadless,
                ViewportSize = new ViewportSize { Width = 1280, Height = 850 },
                Args = new[] { "--start-maximized", "--disable-blink-features=AutomationControlled" }
            };

            try
            {
                options.Channel = channel;
                _browserContext = await _playwright.Chromium.LaunchPersistentContextAsync(userDataDir, options);
            }
            catch (Exception ex)
            {
                Log($"Channel '{channel}' unavailable, falling back to default Chromium: {ex.Message}");
                options.Channel = null; // Fallback to bundled Chromium
                _browserContext = await _playwright.Chromium.LaunchPersistentContextAsync(userDataDir, options);
            }

            _page = _browserContext.Pages.Count > 0 ? _browserContext.Pages[0] : await _browserContext.NewPageAsync();
            Log("Browser context initialized successfully.");
        }

        public async Task NavigateToUrlAsync(string url)
        {
            if (_page == null)
            {
                await InitializeBrowserAsync();
            }

            Log($"Navigating to: {url}");
            await _page!.GotoAsync(url, new PageGotoOptions { WaitUntil = WaitUntilState.DOMContentLoaded });
        }

        public async Task<AutoFillResult> AutoFillJobApplicationFormAsync(UserProfile profile, string? cvFilePath = null)
        {
            if (_page == null)
            {
                throw new InvalidOperationException("Browser page is not initialized.");
            }

            Log("Starting dynamic form-field auto-fill process...");
            int fieldsFilled = 0;
            bool resumeUploaded = false;

            // Wait brief moment for dynamic JS forms to render
            await _page.WaitForTimeoutAsync(1500);

            // 1. Auto-fill Text Inputs based on heuristics
            var inputs = await _page.QuerySelectorAllAsync("input:not([type='hidden']):not([type='submit']):not([type='button']), textarea");

            foreach (var input in inputs)
            {
                try
                {
                    var type = (await input.GetAttributeAsync("type") ?? "text").ToLower();
                    var name = (await input.GetAttributeAsync("name") ?? "").ToLower();
                    var id = (await input.GetAttributeAsync("id") ?? "").ToLower();
                    var placeholder = (await input.GetAttributeAsync("placeholder") ?? "").ToLower();
                    var ariaLabel = (await input.GetAttributeAsync("aria-label") ?? "").ToLower();
                    var labelText = await GetAssociatedLabelTextAsync(input);

                    var combinedMeta = $"{name} {id} {placeholder} {ariaLabel} {labelText}".ToLower();

                    if (type == "file")
                    {
                        if (!string.IsNullOrEmpty(cvFilePath) && File.Exists(cvFilePath))
                        {
                            Log($"Uploading CV file: {cvFilePath}");
                            await input.SetInputFilesAsync(cvFilePath);
                            resumeUploaded = true;
                            fieldsFilled++;
                        }
                        continue;
                    }

                    if (type == "email" || combinedMeta.Contains("email") || combinedMeta.Contains("mail"))
                    {
                        if (!string.IsNullOrEmpty(profile.Email))
                        {
                            await input.FillAsync(profile.Email);
                            fieldsFilled++;
                        }
                    }
                    else if (type == "tel" || combinedMeta.Contains("phone") || combinedMeta.Contains("mobile") || combinedMeta.Contains("tel") || combinedMeta.Contains("hاتف"))
                    {
                        if (!string.IsNullOrEmpty(profile.Phone))
                        {
                            await input.FillAsync(profile.Phone);
                            fieldsFilled++;
                        }
                    }
                    else if (combinedMeta.Contains("first") && combinedMeta.Contains("name"))
                    {
                        var firstName = profile.FullName.Split(' ')[0];
                        await input.FillAsync(firstName);
                        fieldsFilled++;
                    }
                    else if (combinedMeta.Contains("last") && combinedMeta.Contains("name"))
                    {
                        var names = profile.FullName.Split(' ');
                        var lastName = names.Length > 1 ? names[names.Length - 1] : profile.FullName;
                        await input.FillAsync(lastName);
                        fieldsFilled++;
                    }
                    else if (combinedMeta.Contains("full name") || combinedMeta.Contains("fullname") || combinedMeta.Contains("name") || combinedMeta.Contains("الاسم"))
                    {
                        if (!string.IsNullOrEmpty(profile.FullName))
                        {
                            await input.FillAsync(profile.FullName);
                            fieldsFilled++;
                        }
                    }
                    else if (combinedMeta.Contains("linkedin"))
                    {
                        if (!string.IsNullOrEmpty(profile.LinkedInUrl))
                        {
                            await input.FillAsync(profile.LinkedInUrl);
                            fieldsFilled++;
                        }
                    }
                    else if (combinedMeta.Contains("github"))
                    {
                        if (!string.IsNullOrEmpty(profile.GitHubUrl))
                        {
                            await input.FillAsync(profile.GitHubUrl);
                            fieldsFilled++;
                        }
                    }
                    else if (combinedMeta.Contains("portfolio") || combinedMeta.Contains("website") || combinedMeta.Contains("site"))
                    {
                        if (!string.IsNullOrEmpty(profile.PortfolioUrl))
                        {
                            await input.FillAsync(profile.PortfolioUrl);
                            fieldsFilled++;
                        }
                    }
                    else if (combinedMeta.Contains("location") || combinedMeta.Contains("city") || combinedMeta.Contains("address") || combinedMeta.Contains("الموقع") || combinedMeta.Contains("المدينة"))
                    {
                        if (!string.IsNullOrEmpty(profile.Location))
                        {
                            await input.FillAsync(profile.Location);
                            fieldsFilled++;
                        }
                    }
                    else if (input.EvaluateAsync<string>("el => el.tagName.toLowerCase()").Result == "textarea" || combinedMeta.Contains("summary") || combinedMeta.Contains("cover") || combinedMeta.Contains("bio") || combinedMeta.Contains("about"))
                    {
                        if (!string.IsNullOrEmpty(profile.ExperienceSummary))
                        {
                            await input.FillAsync(profile.ExperienceSummary);
                            fieldsFilled++;
                        }
                    }
                }
                catch (Exception ex)
                {
                    Log($"Skipped field due to interaction error: {ex.Message}");
                }
            }

            // 2. HARD STOP RULE EXECUTION
            Log("=== EXECUTING HARD STOP RULE ===");
            var hardStopApplied = await EnforceHardStopRuleAsync();

            return new AutoFillResult
            {
                Success = true,
                FieldsFilled = fieldsFilled,
                ResumeUploaded = resumeUploaded,
                HardStopTriggered = hardStopApplied,
                Message = hardStopApplied 
                    ? "HARD STOP RULE ACTIVATED: Form fields auto-filled successfully. Submit button is highlighted in red for user review. Automation STOPPED." 
                    : "Auto-fill completed. Submit button hard stop applied."
            };
        }

        private async Task<bool> EnforceHardStopRuleAsync()
        {
            if (_page == null) return false;

            // Find all potential submit buttons
            var submitButtons = await _page.QuerySelectorAllAsync(
                "button[type='submit'], input[type='submit'], button:has-text('Submit'), button:has-text('Apply'), button:has-text('إرسال'), input[value*='Submit']"
            );

            bool elementHighlighted = false;

            foreach (var btn in submitButtons)
            {
                try
                {
                    // HARD STOP: Highlight button visually in red border & shadow, and scroll into view. DO NOT CLICK.
                    await btn.EvaluateAsync(@"el => {
                        el.style.border = '5px solid #EF4444';
                        el.style.boxShadow = '0 0 25px #EF4444';
                        el.style.backgroundColor = '#FEF2F2';
                        el.style.color = '#991B1B';
                        el.style.fontWeight = 'bold';
                        el.scrollIntoView({ behavior: 'smooth', block: 'center' });
                    }");

                    elementHighlighted = true;
                    Log("CRITICAL HARD STOP: Highlighted submit button with red outline. WILL NOT CLICK SUBMIT.");
                }
                catch (Exception ex)
                {
                    Log($"Hard stop highlight attempt failed on button: {ex.Message}");
                }
            }

            return elementHighlighted;
        }

        private async Task<string> GetAssociatedLabelTextAsync(IElementHandle input)
        {
            try
            {
                var id = await input.GetAttributeAsync("id");
                if (!string.IsNullOrEmpty(id) && _page != null)
                {
                    var label = await _page.QuerySelectorAsync($"label[for='{id}']");
                    if (label != null)
                    {
                        return await label.InnerTextAsync() ?? string.Empty;
                    }
                }
            }
            catch
            {
                // Fallthrough if label not found
            }
            return string.Empty;
        }

        private void Log(string message)
        {
            OnLogEmitted?.Invoke($"[PlaywrightAutomationService] {DateTime.Now:HH:mm:ss} - {message}");
        }

        /// <summary>
        /// Retrieves and scans all active target links solely from enabled records in SearchSources table
        /// </summary>
        public async Task<List<string>> NavigateAndScanEnabledSourcesAsync()
        {
            var scannedUrls = new List<string>();
            try
            {
                using var db = new OAIDbContext();
                var activeSources = db.SearchSources.Where(s => s.IsEnabled).ToList();

                Log($"Pulling active target links solely from enabled records in SearchSources table ({activeSources.Count} enabled sources found)...");

                foreach (var source in activeSources)
                {
                    if (string.IsNullOrWhiteSpace(source.TargetUrl)) continue;

                    Log($"Opening enabled target source: {source.PlatformName} -> {source.TargetUrl}");
                    await NavigateToUrlAsync(source.TargetUrl);
                    scannedUrls.Add(source.TargetUrl);
                }
            }
            catch (Exception ex)
            {
                Log($"Error scanning enabled search sources: {ex.Message}");
            }

            return scannedUrls;
        }

        /// <summary>
        /// Verifies whether the specified job URL belongs to an enabled SearchSource in the database
        /// </summary>
        public static bool IsSourceEnabled(string url)
        {
            if (string.IsNullOrWhiteSpace(url)) return false;

            try
            {
                using var db = new OAIDbContext();
                var enabledUrls = db.SearchSources.Where(s => s.IsEnabled).Select(s => s.TargetUrl).ToList();
                return enabledUrls.Any(u => !string.IsNullOrEmpty(u) && (url.Contains(u, StringComparison.OrdinalIgnoreCase) || u.Contains(url, StringComparison.OrdinalIgnoreCase)));
            }
            catch
            {
                return true; // Fallback to allowing if DB not accessible
            }
        }

        public async ValueTask DisposeAsync()
        {
            if (_browserContext != null)
            {
                await _browserContext.CloseAsync();
                await _browserContext.DisposeAsync();
            }

            if (_playwright != null)
            {
                _playwright.Dispose();
            }
        }
    }
}
