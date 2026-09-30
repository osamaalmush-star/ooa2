using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;

namespace OAI.Services
{
    public class ParsedResumeData
    {
        public string FullName { get; set; } = string.Empty;
        public string Email { get; set; } = string.Empty;
        public string Phone { get; set; } = string.Empty;
        public string Age { get; set; } = string.Empty;
        public string Gender { get; set; } = string.Empty;
        public string TargetTitle { get; set; } = string.Empty;
        public string Experience { get; set; } = string.Empty;
        public string Skills { get; set; } = string.Empty;
        public string Location { get; set; } = string.Empty;
        public string Education { get; set; } = string.Empty;
        public string Keywords { get; set; } = string.Empty;
    }

    public class GeminiApiService
    {
        private readonly HttpClient _httpClient;
        private string _apiKey;
        private string _modelName;

        public GeminiApiService(string? apiKey = null, string? modelName = null)
        {
            _httpClient = new HttpClient();
            
            // Try loading from appsettings.json if present
            var (configKey, configModel) = LoadConfigFromAppSettings();

            _apiKey = apiKey ?? LoadStoredApiKey() ?? Environment.GetEnvironmentVariable("GEMINI_API_KEY") ?? configKey;
            _modelName = modelName ?? LoadStoredModelName() ?? configModel ?? "gemini-2.0-flash";
        }

        private static (string key, string model) LoadConfigFromAppSettings()
        {
            try
            {
                var appSettingsPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "appsettings.json");
                if (!File.Exists(appSettingsPath))
                {
                    appSettingsPath = Path.Combine(Directory.GetCurrentDirectory(), "appsettings.json");
                }

                if (File.Exists(appSettingsPath))
                {
                    var json = File.ReadAllText(appSettingsPath);
                    using var doc = JsonDocument.Parse(json);
                    if (doc.RootElement.TryGetProperty("GeminiSettings", out var gemini))
                    {
                        var key = gemini.TryGetProperty("ApiKey", out var k) ? k.GetString() ?? "" : "";
                        var model = gemini.TryGetProperty("ModelName", out var m) ? m.GetString() ?? "" : "";
                        return (key, model);
                    }
                }
            }
            catch
            {
                // Fallthrough to defaults
            }
            return (string.Empty, "gemini-2.0-flash");
        }

        private static string? LoadStoredApiKey()
        {
            try
            {
                using var db = new Data.OAIDbContext();
                var stored = db.AppSettings.AsNoTracking().FirstOrDefault();
                if (stored != null && !string.IsNullOrWhiteSpace(stored.GeminiApiKey))
                    return stored.GeminiApiKey;
            }
            catch
            {
            }
            return null;
        }

        private static string? LoadStoredModelName()
        {
            try
            {
                using var db = new Data.OAIDbContext();
                var stored = db.AppSettings.AsNoTracking().FirstOrDefault();
                if (stored != null && !string.IsNullOrWhiteSpace(stored.GeminiModelName))
                    return stored.GeminiModelName;
            }
            catch
            {
            }
            return null;
        }

        public void SetApiKey(string apiKey)
        {
            _apiKey = apiKey;
        }

        public void SetModelName(string modelName)
        {
            _modelName = modelName;
        }

        public async Task<string> GenerateTextAsync(string prompt, string? systemInstruction = null)
        {
            if (string.IsNullOrWhiteSpace(_apiKey))
            {
                throw new InvalidOperationException("Gemini API key is not configured. Please set a valid API key in appsettings.json, settings, or environment variables.");
            }

            var candidateModels = new List<string>();
            void AddModel(string name)
            {
                if (!string.IsNullOrWhiteSpace(name) &&
                    !candidateModels.Contains(name, StringComparer.OrdinalIgnoreCase))
                    candidateModels.Add(name);
            }

            AddModel(_modelName);
            AddModel("gemini-2.0-flash");
            AddModel("gemini-1.5-flash");
            AddModel("gemini-1.5-flash-latest");

            string lastError = string.Empty;

            foreach (var model in candidateModels)
            {
                var url = $"https://generativelanguage.googleapis.com/v1beta/models/{model}:generateContent?key={_apiKey}";

                var requestBody = new GeminiRequest();

                if (!string.IsNullOrEmpty(systemInstruction))
                {
                    requestBody.SystemInstruction = new GeminiContent
                    {
                        Parts = new[] { new GeminiPart { Text = systemInstruction } }
                    };
                }

                requestBody.Contents = new[]
                {
                    new GeminiContent
                    {
                        Parts = new[] { new GeminiPart { Text = prompt } }
                    }
                };

                // Force JSON response formatting if system instruction requests JSON
                if (systemInstruction != null && systemInstruction.Contains("JSON", StringComparison.OrdinalIgnoreCase))
                {
                    requestBody.GenerationConfig = new GeminiGenerationConfig
                    {
                        ResponseMimeType = "application/json"
                    };
                }

                var jsonOptions = new JsonSerializerOptions
                {
                    DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
                    PropertyNamingPolicy = JsonNamingPolicy.CamelCase
                };

                var jsonContent = JsonSerializer.Serialize(requestBody, jsonOptions);
                var content = new StringContent(jsonContent, Encoding.UTF8, "application/json");

                var response = await _httpClient.PostAsync(url, content);
                var responseString = await response.Content.ReadAsStringAsync();

                if (response.IsSuccessStatusCode)
                {
                    using var doc = JsonDocument.Parse(responseString);
                    var root = doc.RootElement;

                    if (root.TryGetProperty("candidates", out var candidates) && candidates.GetArrayLength() > 0)
                    {
                        var firstCandidate = candidates[0];
                        if (firstCandidate.TryGetProperty("content", out var candidateContent) &&
                            candidateContent.TryGetProperty("parts", out var parts) &&
                            parts.GetArrayLength() > 0)
                        {
                            return parts[0].GetProperty("text").GetString() ?? string.Empty;
                        }
                    }
                    return string.Empty;
                }

                lastError = $"Gemini API error for model '{model}' ({response.StatusCode}): {responseString}";
                
                // If error is not a 404 (model not found), don't keep looping if it's invalid key (400/403)
                if ((int)response.StatusCode == 400 || (int)response.StatusCode == 403)
                {
                    throw new HttpRequestException(lastError);
                }
            }

            throw new HttpRequestException(lastError);
        }

        public async Task<ParsedResumeData> ParseResumeAsync(string cvText)
        {
            const string systemInstruction = @"You are an ATS resume parser.
Extract ONLY facts explicitly written in the CV. Never guess, invent, or estimate missing values. Use empty string when a field is not present.
Return JSON:
{
  ""fullName"": """",
  ""email"": """",
  ""phone"": """",
  ""age"": """",
  ""gender"": """",
  ""location"": """",
  ""targetTitle"": """",
  ""experience"": """",
  ""education"": """",
  ""skills"": """",
  ""keywords"": """"
}
Rules:
- skills and keywords are comma-separated lists taken from the CV.
- experience is only what the CV states.
- Output ONLY valid JSON.";

            var prompt = $"Analyze and parse this CV:\n\n{cvText}";

            try
            {
                var json = await GenerateTextAsync(prompt, systemInstruction);
                var cleaned = CleanJsonString(json);
                var options = new JsonSerializerOptions { PropertyNameCaseInsensitive = true };
                var result = JsonSerializer.Deserialize<ParsedResumeData>(cleaned, options);
                if (result != null && !string.IsNullOrWhiteSpace(result.FullName)) return result;
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[GeminiApiService] ParseResumeAsync API error: {ex.Message}");
            }

            return ExtractHeuristics(cvText);
        }

        private static string CleanJsonString(string input)
        {
            if (string.IsNullOrWhiteSpace(input)) return "{}";
            var trimmed = input.Trim();
            if (trimmed.StartsWith("```json", StringComparison.OrdinalIgnoreCase)) trimmed = trimmed.Substring(7);
            else if (trimmed.StartsWith("```")) trimmed = trimmed.Substring(3);
            if (trimmed.EndsWith("```")) trimmed = trimmed.Substring(0, trimmed.Length - 3);
            return trimmed.Trim();
        }

        private static ParsedResumeData ExtractHeuristics(string cvText)
        {
            var data = new ParsedResumeData();
            if (string.IsNullOrWhiteSpace(cvText)) return data;

            var emailMatch = System.Text.RegularExpressions.Regex.Match(cvText, @"[a-zA-Z0-9._%+-]+@[a-zA-Z0-9.-]+\.[a-zA-Z]{2,}");
            if (emailMatch.Success) data.Email = emailMatch.Value;

            var phoneMatch = System.Text.RegularExpressions.Regex.Match(cvText, @"(\+?[0-9\s\-()]{8,20})");
            if (phoneMatch.Success) data.Phone = phoneMatch.Value.Trim();

            var firstLine = cvText.Split('\n').Select(l => l.Trim()).FirstOrDefault(l => l.Length > 2 && l.Length < 80);
            data.FullName = firstLine ?? string.Empty;
            return data;
        }

        private class GeminiRequest
        {
            [JsonPropertyName("contents")]
            public GeminiContent[] Contents { get; set; } = Array.Empty<GeminiContent>();

            [JsonPropertyName("systemInstruction")]
            public GeminiContent? SystemInstruction { get; set; }

            [JsonPropertyName("generationConfig")]
            public GeminiGenerationConfig? GenerationConfig { get; set; }
        }

        private class GeminiContent
        {
            [JsonPropertyName("parts")]
            public GeminiPart[] Parts { get; set; } = Array.Empty<GeminiPart>();
        }

        private class GeminiPart
        {
            [JsonPropertyName("text")]
            public string Text { get; set; } = string.Empty;
        }

        private class GeminiGenerationConfig
        {
            [JsonPropertyName("responseMimeType")]
            public string? ResponseMimeType { get; set; }
        }
    }
}
