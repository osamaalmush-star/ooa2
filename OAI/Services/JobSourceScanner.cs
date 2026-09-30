using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using HtmlAgilityPack;
using OAI.Models;

namespace OAI.Services
{
    public class JobSourceScanner
    {
        private static readonly HttpClient Http = CreateClient();
        private static readonly Regex EmailRegex = new(@"[a-zA-Z0-9._%+\-]+@[a-zA-Z0-9.\-]+\.[a-zA-Z]{2,}", RegexOptions.Compiled);
        private static readonly Regex AbsoluteUrlRegex = new(@"https?://[^\s""'<>]+", RegexOptions.IgnoreCase | RegexOptions.Compiled);

        private static readonly string[] ClosedMarkers =
        {
            "expired", "closed", "no longer accepting", "لم تعد", "منتهية", "انتهى التقديم", "filled", "position filled"
        };

        private static readonly string[] NationalityMarkers =
        {
            "nationality", "nationals only", "citizens only", "سعودي فقط", "مواطن", "must be a national", "gcc national"
        };

        private static HttpClient CreateClient()
        {
            var client = new HttpClient { Timeout = TimeSpan.FromSeconds(20) };
            client.DefaultRequestHeaders.UserAgent.ParseAdd("Mozilla/5.0 (Windows NT 10.0; Win64; x64) OAI/1.0");
            return client;
        }

        public async Task<List<JobListing>> ScanAsync(SearchSource source)
        {
            var results = new List<JobListing>();
            if (source == null || string.IsNullOrWhiteSpace(source.TargetUrl))
                return results;

            string html;
            try
            {
                html = await Http.GetStringAsync(source.TargetUrl);
            }
            catch
            {
                return results;
            }

            var doc = new HtmlDocument();
            doc.LoadHtml(html);
            var nodes = doc.DocumentNode.SelectNodes("//a[@href]");
            if (nodes == null) return results;

            foreach (var node in nodes)
            {
                var href = node.GetAttributeValue("href", "");
                var text = Normalize(node.InnerText);
                if (string.IsNullOrWhiteSpace(href) || text.Length < 8)
                    continue;
                if (!LooksLikeJobLink(href, text))
                    continue;

                var url = ToAbsolute(source.TargetUrl, href);
                if (string.IsNullOrWhiteSpace(url))
                    continue;

                var listing = await BuildListingAsync(source, url, text);
                if (listing == null)
                    continue;
                if (!listing.IsOpen || listing.RequiresNationality)
                    continue;

                if (results.All(r => !string.Equals(r.Url, listing.Url, StringComparison.OrdinalIgnoreCase)))
                    results.Add(listing);

                if (results.Count >= 12)
                    break;
            }

            return results;
        }

        private async Task<JobListing?> BuildListingAsync(SearchSource source, string url, string titleHint)
        {
            string pageHtml = string.Empty;
            try
            {
                pageHtml = await Http.GetStringAsync(url);
            }
            catch
            {
                pageHtml = string.Empty;
            }

            var combined = (titleHint + " " + StripHtml(pageHtml)).ToLowerInvariant();
            var isClosed = ClosedMarkers.Any(m => combined.Contains(m));
            var requiresNationality = NationalityMarkers.Any(m => combined.Contains(m));

            var pageDoc = new HtmlDocument();
            pageDoc.LoadHtml(pageHtml);

            var title = FirstNonEmpty(
                Meta(pageDoc, "og:title"),
                Heading(pageDoc),
                titleHint);

            var company = FirstNonEmpty(
                Meta(pageDoc, "og:site_name"),
                source.PlatformName);

            var location = ExtractLabeled(pageDoc, pageHtml, new[] { "location", "الموقع", "city", "المدينة" });
            var salary = ExtractLabeled(pageDoc, pageHtml, new[] { "salary", "الراتب", "compensation" });
            var description = FirstNonEmpty(Meta(pageDoc, "og:description"), StripHtml(pageHtml));
            if (description.Length > 2500) description = description[..2500];

            var hrEmail = EmailRegex.Match(pageHtml).Success ? EmailRegex.Match(pageHtml).Value : null;
            var applyUrl = FindApplyUrl(pageDoc, url) ?? url;

            return new JobListing
            {
                Title = title,
                Company = company,
                Location = string.IsNullOrWhiteSpace(location) ? "غير مذكور" : location,
                Salary = string.IsNullOrWhiteSpace(salary) ? "غير مذكور" : salary,
                Url = url,
                ApplyUrl = applyUrl,
                HrEmail = hrEmail,
                Description = description,
                Platform = source.PlatformName,
                IsOpen = !isClosed,
                RequiresNationality = requiresNationality
            };
        }

        private static bool LooksLikeJobLink(string href, string text)
        {
            var hay = (href + " " + text).ToLowerInvariant();
            return hay.Contains("job") || hay.Contains("career") || hay.Contains("vacancy")
                || hay.Contains("وظيف") || hay.Contains("توظيف") || hay.Contains("وظائف");
        }

        private static string? ToAbsolute(string baseUrl, string href)
        {
            if (href.StartsWith("javascript:", StringComparison.OrdinalIgnoreCase) || href.StartsWith("#"))
                return null;
            if (Uri.TryCreate(href, UriKind.Absolute, out var abs))
                return abs.ToString();
            if (Uri.TryCreate(baseUrl, UriKind.Absolute, out var root) && Uri.TryCreate(root, href, out var combined))
                return combined.ToString();
            return AbsoluteUrlRegex.IsMatch(href) ? href : null;
        }

        private static string? FindApplyUrl(HtmlDocument doc, string fallback)
        {
            var links = doc.DocumentNode.SelectNodes("//a[@href]");
            if (links == null) return fallback;
            foreach (var a in links)
            {
                var t = Normalize(a.InnerText).ToLowerInvariant();
                var h = a.GetAttributeValue("href", "");
                if (t.Contains("apply") || t.Contains("تقديم") || t.Contains("تقدم") || h.Contains("apply", StringComparison.OrdinalIgnoreCase))
                {
                    var abs = ToAbsolute(fallback, h);
                    if (!string.IsNullOrWhiteSpace(abs)) return abs;
                }
            }
            return fallback;
        }

        private static string ExtractLabeled(HtmlDocument doc, string html, string[] labels)
        {
            var text = StripHtml(html);
            foreach (var label in labels)
            {
                var match = Regex.Match(text, label + @"\s*[:：]\s*([^\n]{3,80})", RegexOptions.IgnoreCase);
                if (match.Success) return match.Groups[1].Value.Trim();
            }
            return string.Empty;
        }

        private static string? Meta(HtmlDocument doc, string property)
        {
            var node = doc.DocumentNode.SelectSingleNode($"//meta[@property='{property}']")
                       ?? doc.DocumentNode.SelectSingleNode($"//meta[@name='{property}']");
            return node?.GetAttributeValue("content", null);
        }

        private static string Heading(HtmlDocument doc)
        {
            var h = doc.DocumentNode.SelectSingleNode("//h1") ?? doc.DocumentNode.SelectSingleNode("//title");
            return h == null ? string.Empty : Normalize(h.InnerText);
        }

        private static string StripHtml(string html)
        {
            if (string.IsNullOrWhiteSpace(html)) return string.Empty;
            var doc = new HtmlDocument();
            doc.LoadHtml(html);
            return Normalize(doc.DocumentNode.InnerText);
        }

        private static string Normalize(string value)
        {
            if (string.IsNullOrWhiteSpace(value)) return string.Empty;
            return Regex.Replace(System.Net.WebUtility.HtmlDecode(value), @"\s+", " ").Trim();
        }

        private static string FirstNonEmpty(params string?[] values)
        {
            return values.FirstOrDefault(v => !string.IsNullOrWhiteSpace(v)) ?? string.Empty;
        }
    }
}
