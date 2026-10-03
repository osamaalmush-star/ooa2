using System.Text.RegularExpressions;
using OAI.Core.Models;

namespace OAI.Core.Services;

public static class MasterProfileParser
{
    private static readonly Regex EmailRegex = new(@"[a-zA-Z0-9._%+\-]+@[a-zA-Z0-9.\-]+\.[a-zA-Z]{2,}", RegexOptions.Compiled);
    private static readonly Regex PhoneRegex = new(@"(\+?\d[\d\s\-()]{7,20})", RegexOptions.Compiled);

    public static MasterUserProfile Parse(string text)
    {
        var profile = new MasterUserProfile();
        if (string.IsNullOrWhiteSpace(text))
            return profile;

        var lines = text.Replace("\r", "").Split('\n')
            .Select(l => l.Trim())
            .Where(l => l.Length > 0)
            .ToList();

        if (lines.Count == 0)
            return profile;

        profile.Identity.FullName = lines[0];

        var email = EmailRegex.Match(text);
        if (email.Success)
            profile.Identity.Email = email.Value;

        var phone = PhoneRegex.Match(text);
        if (phone.Success)
            profile.Identity.Phone = phone.Value.Trim();

        profile.Identity.TargetJobTitle = DetectTitle(lines);
        profile.Identity.Location = DetectLocation(text);
        profile.Identity.LinkedInUrl = DetectUrl(text, "linkedin.com");
        profile.Identity.GitHubUrl = DetectUrl(text, "github.com");

        profile.Experiences = ParseExperiences(lines);
        profile.Education = ParseEducation(lines);
        profile.Skills = ParseSkills(text);
        profile.LastUpdated = DateTime.UtcNow;
        return profile;
    }

    private static string DetectTitle(List<string> lines)
    {
        foreach (var line in lines.Skip(1).Take(6))
        {
            if (line.Contains('@') || PhoneRegex.IsMatch(line))
                continue;
            if (line.Contains("linkedin", StringComparison.OrdinalIgnoreCase) || line.Contains("github", StringComparison.OrdinalIgnoreCase))
                continue;
            if (line.Equals("WORK EXPERIENCE", StringComparison.OrdinalIgnoreCase) ||
                line.Equals("EDUCATION", StringComparison.OrdinalIgnoreCase) ||
                line.Equals("SKILLS", StringComparison.OrdinalIgnoreCase))
                continue;
            if (line.Length is > 2 and < 80)
                return line;
        }
        return string.Empty;
    }

    private static string DetectLocation(string text)
    {
        var match = Regex.Match(text, @"(Riyadh|Jeddah|Dammam|Mecca|Medina|Dubai|Abu Dhabi|Cairo|Amman|Doha|Kuwait)[^,\n]*,?\s*[^\n]{0,40}", RegexOptions.IgnoreCase);
        return match.Success ? match.Value.Trim() : string.Empty;
    }

    private static string DetectUrl(string text, string host)
    {
        var match = Regex.Match(text, $@"(https?://)?(www\.)?{Regex.Escape(host)}/[^\s]+", RegexOptions.IgnoreCase);
        return match.Success ? match.Value.Trim() : string.Empty;
    }

    private static List<WorkExperience> ParseExperiences(List<string> lines)
    {
        var results = new List<WorkExperience>();
        var start = IndexOfHeading(lines, "WORK EXPERIENCE", "EXPERIENCE", "الخبرة");
        if (start < 0) return results;

        var end = NextHeading(lines, start + 1);
        WorkExperience? current = null;
        for (var i = start + 1; i < end; i++)
        {
            var line = lines[i];
            if (line.StartsWith("-") || line.StartsWith("•") || line.StartsWith("*"))
            {
                current ??= new WorkExperience();
                current.ResponsibilitiesAndAchievements.Add(line.TrimStart('-', '•', '*', ' ').Trim());
                continue;
            }

            if (line.Contains("—") || line.Contains(" - ") || line.Contains("–"))
            {
                if (current != null && (!string.IsNullOrWhiteSpace(current.CompanyName) || current.ResponsibilitiesAndAchievements.Count > 0))
                    results.Add(current);
                current = SplitCompanyRole(line);
            }
        }

        if (current != null && (!string.IsNullOrWhiteSpace(current.CompanyName) || current.ResponsibilitiesAndAchievements.Count > 0))
            results.Add(current);

        return results;
    }

    private static WorkExperience SplitCompanyRole(string line)
    {
        var parts = Regex.Split(line, @"\s+[—–-]\s+");
        if (parts.Length >= 2)
            return new WorkExperience { CompanyName = parts[0].Trim(), RoleTitle = parts[1].Trim() };
        return new WorkExperience { CompanyName = line, RoleTitle = "Professional" };
    }

    private static List<EducationItem> ParseEducation(List<string> lines)
    {
        var results = new List<EducationItem>();
        var start = IndexOfHeading(lines, "EDUCATION", "التعليم");
        if (start < 0) return results;
        var end = NextHeading(lines, start + 1);
        for (var i = start + 1; i < end; i++)
        {
            var line = lines[i];
            if (line.StartsWith("-") || line.StartsWith("•")) continue;
            var item = new EducationItem();
            var year = Regex.Match(line, @"(19|20)\d{2}");
            if (year.Success) item.GraduationYear = year.Value;
            var parts = Regex.Split(line, @"\s+[—–-]\s+");
            item.Institution = parts[0].Trim();
            if (parts.Length > 1)
            {
                item.Degree = parts[1].Trim();
                var field = Regex.Match(parts[1], @"(Computer Science|CS|Engineering|IT|Information)", RegexOptions.IgnoreCase);
                if (field.Success) item.FieldOfStudy = field.Value;
            }
            results.Add(item);
        }
        return results;
    }

    private static SkillSet ParseSkills(string text)
    {
        var skills = new SkillSet();
        var match = Regex.Match(text, @"(SKILLS|المهارات)\s*:?\s*(.+)", RegexOptions.IgnoreCase | RegexOptions.Singleline);
        var blob = match.Success ? match.Groups[2].Value : text;
        var headingCut = Regex.Split(blob, @"\n[A-Z ]{4,}\n").FirstOrDefault() ?? blob;
        var firstLine = headingCut.Split('\n').Select(l => l.Trim()).FirstOrDefault(l => l.Length > 0) ?? headingCut;
        var tokens = firstLine.Split(new[] { ',', '|', '،', ';' }, StringSplitOptions.RemoveEmptyEntries)
            .Select(t => t.Trim())
            .Where(t => t.Length is > 1 and < 40)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();

        var soft = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
            { "communication", "leadership", "teamwork", "problem solving", "التواصل" };
        var tools = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
            { "git", "ef core", "docker", "kubernetes", "jira", "visual studio" };

        foreach (var token in tokens)
        {
            if (soft.Contains(token)) skills.SoftSkills.Add(token);
            else if (tools.Contains(token)) skills.ConceptsAndTools.Add(token);
            else skills.TechnicalSkills.Add(token);
        }
        return skills;
    }

    private static int IndexOfHeading(List<string> lines, params string[] headings)
    {
        return lines.FindIndex(l => headings.Any(h => l.Equals(h, StringComparison.OrdinalIgnoreCase) || l.StartsWith(h, StringComparison.OrdinalIgnoreCase)));
    }

    private static int NextHeading(List<string> lines, int from)
    {
        var headings = new[] { "WORK EXPERIENCE", "EXPERIENCE", "EDUCATION", "SKILLS", "CERTIFICATIONS", "الخبرة", "التعليم", "المهارات" };
        var idx = lines.FindIndex(from, l => headings.Any(h => l.Equals(h, StringComparison.OrdinalIgnoreCase)));
        return idx < 0 ? lines.Count : idx;
    }
}
