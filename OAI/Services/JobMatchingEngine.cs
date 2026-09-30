using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Threading.Tasks;
using OAI.Models;

namespace OAI.Services
{
    public class JobMatchResult
    {
        [JsonPropertyName("matchScore")]
        public double MatchScore { get; set; } // 0 to 100

        [JsonPropertyName("matchingSkills")]
        public List<string> MatchingSkills { get; set; } = new List<string>();

        [JsonPropertyName("missingSkills")]
        public List<string> MissingSkills { get; set; } = new List<string>();

        [JsonPropertyName("rationale")]
        public string Rationale { get; set; } = string.Empty;

        [JsonPropertyName("recommendation")]
        public string Recommendation { get; set; } = "ModerateMatch"; // StrongMatch, ModerateMatch, WeakMatch

        public string RawJson { get; set; } = string.Empty;
    }

    public class JobMatchingEngine
    {
        private readonly GeminiApiService _geminiApiService;

        public JobMatchingEngine(GeminiApiService geminiApiService)
        {
            _geminiApiService = geminiApiService;
        }

        public async Task<JobMatchResult> EvaluateJobMatchAsync(UserProfile profile, string jobTitle, string companyName, string jobDescription)
        {
            const string systemInstruction = @"You are an expert AI Career Copilot and ATS Job Matching Engine.
Compare the user's profile with the provided Job Description.
Evaluate how well the candidate matches the position requirements and produce a JSON evaluation strictly formatted as:
{
  ""matchScore"": 85.5,
  ""matchingSkills"": [""C#"", "".NET 8"", ""SQL Server""],
  ""missingSkills"": [""Kubernetes"", ""Docker""],
  ""rationale"": ""The candidate has strong .NET and WPF expertise required for this role but lacks containerization experience."",
  ""recommendation"": ""StrongMatch""
}
Valid values for recommendation: ""StrongMatch"", ""ModerateMatch"", ""WeakMatch"".
Do NOT include any extra text outside valid JSON.";

            var userProfileSummary = $@"Name: {profile.FullName}
Target Job Titles: {profile.TargetJobTitle}
Skills: {profile.SkillsList}
Keywords: {profile.Keywords}
Education: {profile.Education}
Experience: {profile.YearsOfExperience}
Summary: {profile.ExperienceSummary}";

            var prompt = $@"=== USER PROFILE ===
{userProfileSummary}

=== JOB DETAILS ===
Title: {jobTitle}
Company: {companyName}
Description: {jobDescription}";

            try
            {
                var jsonResponse = await _geminiApiService.GenerateTextAsync(prompt, systemInstruction);
                var cleanedJson = CleanJsonString(jsonResponse);
                var options = new JsonSerializerOptions { PropertyNameCaseInsensitive = true };
                var matchResult = JsonSerializer.Deserialize<JobMatchResult>(cleanedJson, options) ?? new JobMatchResult();
                matchResult.RawJson = cleanedJson;
                if (matchResult.MatchScore > 0) return matchResult;
            }
            catch
            {
            }

            return HeuristicMatch(profile, jobTitle, jobDescription);
        }

        private static JobMatchResult HeuristicMatch(UserProfile profile, string jobTitle, string jobDescription)
        {
            var skills = SplitTokens(profile.SkillsList + " " + profile.Keywords + " " + profile.Education);
            var haystack = (jobTitle + " " + jobDescription).ToLowerInvariant();
            var matching = skills.Where(s => haystack.Contains(s.ToLowerInvariant())).Distinct().ToList();
            var score = skills.Count == 0 ? 50 : Math.Min(95, 40 + matching.Count * 12);
            var rec = score >= 80 ? "StrongMatch" : score >= 60 ? "ModerateMatch" : "WeakMatch";
            return new JobMatchResult
            {
                MatchScore = score,
                MatchingSkills = matching,
                Rationale = "مطابقة محلية بدون استجابة من Gemini.",
                Recommendation = rec
            };
        }

        private static List<string> SplitTokens(string raw)
        {
            if (string.IsNullOrWhiteSpace(raw)) return new List<string>();
            return raw.Replace("[", "").Replace("]", "").Replace("\"", "")
                .Split(new[] { ',', '|', ';' }, StringSplitOptions.RemoveEmptyEntries)
                .Select(s => s.Trim())
                .Where(s => s.Length > 1)
                .ToList();
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
