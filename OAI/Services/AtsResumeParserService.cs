using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Threading.Tasks;
using UglyToad.PdfPig;
using OAI.Models;

namespace OAI.Services
{
    public class ParsedResumeResult
    {
        [JsonPropertyName("fullName")]
        public string FullName { get; set; } = string.Empty;

        [JsonPropertyName("email")]
        public string Email { get; set; } = string.Empty;

        [JsonPropertyName("phone")]
        public string Phone { get; set; } = string.Empty;

        [JsonPropertyName("location")]
        public string Location { get; set; } = string.Empty;

        [JsonPropertyName("linkedInUrl")]
        public string? LinkedInUrl { get; set; }

        [JsonPropertyName("gitHubUrl")]
        public string? GitHubUrl { get; set; }

        [JsonPropertyName("portfolioUrl")]
        public string? PortfolioUrl { get; set; }

        [JsonPropertyName("targetJobTitles")]
        public List<string> TargetJobTitles { get; set; } = new List<string>();

        [JsonPropertyName("skills")]
        public List<string> Skills { get; set; } = new List<string>();

        [JsonPropertyName("experienceSummary")]
        public string ExperienceSummary { get; set; } = string.Empty;

        [JsonPropertyName("education")]
        public string Education { get; set; } = string.Empty;

        [JsonPropertyName("keywords")]
        public List<string> Keywords { get; set; } = new List<string>();

        public string RawJson { get; set; } = string.Empty;
    }

    public class AtsResumeParserService
    {
        private readonly GeminiApiService _geminiApiService;

        public AtsResumeParserService(GeminiApiService geminiApiService)
        {
            _geminiApiService = geminiApiService;
        }

        /// <summary>
        /// Extracts full plain text content from a PDF document page by page using PdfPig
        /// </summary>
        public static string ExtractTextFromPdf(string pdfFilePath)
        {
            if (string.IsNullOrWhiteSpace(pdfFilePath) || !File.Exists(pdfFilePath))
            {
                return string.Empty;
            }

            try
            {
                using var document = PdfDocument.Open(pdfFilePath);
                var sb = new StringBuilder();
                foreach (var page in document.GetPages())
                {
                    if (!string.IsNullOrWhiteSpace(page.Text))
                    {
                        sb.AppendLine(page.Text);
                    }
                }
                return sb.ToString().Trim();
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[AtsResumeParserService] PDF extraction error: {ex.Message}");
                return string.Empty;
            }
        }

        public async Task<UserProfile> ParseAndCreateUserProfileAsync(string rawCvText)
        {
            const string systemInstruction = @"Extract only facts written in the CV. Never invent missing values. Use empty string or empty arrays when unknown.
JSON schema:
{
  ""fullName"": """",
  ""email"": """",
  ""phone"": """",
  ""location"": """",
  ""linkedInUrl"": """",
  ""gitHubUrl"": """",
  ""portfolioUrl"": """",
  ""targetJobTitles"": [],
  ""skills"": [],
  ""experienceSummary"": """"
}
Output ONLY JSON.";

            var prompt = $"Please parse the following CV content into structured ATS JSON:\n\n{rawCvText}";

            var jsonResponse = await _geminiApiService.GenerateTextAsync(prompt, systemInstruction);

            // Clean json markdown delimiters if present
            var cleanedJson = CleanJsonString(jsonResponse);

            var options = new JsonSerializerOptions
            {
                PropertyNameCaseInsensitive = true
            };

            var parsedResult = JsonSerializer.Deserialize<ParsedResumeResult>(cleanedJson, options) ?? new ParsedResumeResult();
            parsedResult.RawJson = cleanedJson;

            return new UserProfile
            {
                FullName = parsedResult.FullName,
                Email = parsedResult.Email,
                Phone = parsedResult.Phone,
                Location = parsedResult.Location,
                LinkedInUrl = parsedResult.LinkedInUrl,
                GitHubUrl = parsedResult.GitHubUrl,
                PortfolioUrl = parsedResult.PortfolioUrl,
                TargetJobTitles = JsonSerializer.Serialize(parsedResult.TargetJobTitles),
                Skills = JsonSerializer.Serialize(parsedResult.Skills),
                Education = parsedResult.Education,
                Keywords = JsonSerializer.Serialize(parsedResult.Keywords),
                ExperienceSummary = parsedResult.ExperienceSummary,
                RawResumeContent = rawCvText,
                ParsedProfileJson = cleanedJson,
                UpdatedAt = DateTime.UtcNow
            };
        }

        private static string CleanJsonString(string input)
        {
            if (string.IsNullOrWhiteSpace(input)) return "{}";

            var trimmed = input.Trim();
            if (trimmed.StartsWith("```json", StringComparison.OrdinalIgnoreCase))
            {
                trimmed = trimmed.Substring(7);
            }
            else if (trimmed.StartsWith("```"))
            {
                trimmed = trimmed.Substring(3);
            }

            if (trimmed.EndsWith("```"))
            {
                trimmed = trimmed.Substring(0, trimmed.Length - 3);
            }

            return trimmed.Trim();
        }
    }
}
