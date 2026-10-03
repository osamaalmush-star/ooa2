using System.Net;
using System.Text;
using OAI.Core.Models;
using QuestPDF.Fluent;
using QuestPDF.Helpers;
using QuestPDF.Infrastructure;

namespace OAI.Core.Services;

public static class AtsPdfExporter
{
    static AtsPdfExporter()
    {
        QuestPDF.Settings.License = LicenseType.Community;
    }

    public static string BuildSingleColumnHtml(MasterUserProfile profile)
    {
        profile ??= new MasterUserProfile();
        var identity = profile.Identity ?? new PersonalInfo();
        var sb = new StringBuilder();
        sb.Append("<html><head><meta charset='utf-8'><style>");
        sb.Append("body{font-family:Arial,Calibri,'Segoe UI',sans-serif;font-size:11pt;color:#111;max-width:720px;margin:24px auto;line-height:1.45;}");
        sb.Append("h1{font-size:20pt;margin:0 0 4px 0;} h2{font-size:12pt;letter-spacing:.08em;border-bottom:1px solid #222;padding-bottom:4px;margin:18px 0 8px;}");
        sb.Append("p,li{margin:0 0 6px 0;} ul{padding-left:18px;}");
        sb.Append("</style></head><body>");
        sb.Append($"<h1>{Enc(identity.FullName)}</h1>");
        sb.Append($"<p>{Enc(identity.TargetJobTitle)}</p>");
        sb.Append($"<p>{Enc(JoinContact(identity))}</p>");

        sb.Append("<h2>PROFESSIONAL SUMMARY</h2>");
        sb.Append($"<p>{Enc(BuildSummary(profile))}</p>");

        sb.Append("<h2>WORK EXPERIENCE</h2>");
        foreach (var exp in profile.Experiences ?? new())
        {
            sb.Append($"<p><strong>{Enc(exp.RoleTitle)}</strong> — {Enc(exp.CompanyName)}</p>");
            sb.Append("<ul>");
            foreach (var bullet in exp.ResponsibilitiesAndAchievements ?? new())
                sb.Append($"<li>{Enc(bullet)}</li>");
            sb.Append("</ul>");
        }

        sb.Append("<h2>EDUCATION</h2>");
        foreach (var edu in profile.Education ?? new())
            sb.Append($"<p>{Enc(edu.Degree)} {Enc(edu.FieldOfStudy)} — {Enc(edu.Institution)} {Enc(edu.GraduationYear)}</p>");

        if (profile.Certifications?.Count > 0)
        {
            sb.Append("<h2>CERTIFICATIONS</h2>");
            foreach (var cert in profile.Certifications)
                sb.Append($"<p>{Enc(cert.Name)} — {Enc(cert.IssuingOrganization)} {Enc(cert.IssueYear)}</p>");
        }

        sb.Append("<h2>SKILLS</h2>");
        var skills = (profile.Skills?.TechnicalSkills ?? new())
            .Concat(profile.Skills?.ConceptsAndTools ?? new())
            .Concat(profile.Skills?.SoftSkills ?? new())
            .Where(s => !string.IsNullOrWhiteSpace(s));
        sb.Append($"<p>{Enc(string.Join(", ", skills))}</p>");
        sb.Append("</body></html>");
        return sb.ToString();
    }

    public static void Export(MasterUserProfile profile, string path)
    {
        var identity = profile.Identity ?? new PersonalInfo();
        Directory.CreateDirectory(Path.GetDirectoryName(path) ?? ".");
        Document.Create(container =>
        {
            container.Page(page =>
            {
                page.Size(PageSizes.A4);
                page.Margin(40);
                page.DefaultTextStyle(x => x.FontSize(11).FontColor("#111111"));
                page.Content().Column(col =>
                {
                    col.Spacing(6);
                    col.Item().Text(identity.FullName).FontSize(20).Bold();
                    if (!string.IsNullOrWhiteSpace(identity.TargetJobTitle))
                        col.Item().Text(identity.TargetJobTitle);
                    col.Item().Text(JoinContact(identity)).FontSize(10);

                    Section(col, "PROFESSIONAL SUMMARY");
                    col.Item().Text(BuildSummary(profile));

                    Section(col, "WORK EXPERIENCE");
                    foreach (var exp in profile.Experiences ?? new())
                    {
                        col.Item().Text(t =>
                        {
                            t.Span(exp.RoleTitle).Bold();
                            t.Span($" — {exp.CompanyName}");
                        });
                        foreach (var bullet in exp.ResponsibilitiesAndAchievements ?? new())
                            col.Item().Text($"• {bullet}");
                    }

                    Section(col, "EDUCATION");
                    foreach (var edu in profile.Education ?? new())
                        col.Item().Text($"{edu.Degree} {edu.FieldOfStudy} — {edu.Institution} {edu.GraduationYear}");

                    if (profile.Certifications?.Count > 0)
                    {
                        Section(col, "CERTIFICATIONS");
                        foreach (var cert in profile.Certifications)
                            col.Item().Text($"{cert.Name} — {cert.IssuingOrganization} {cert.IssueYear}");
                    }

                    Section(col, "SKILLS");
                    var skills = (profile.Skills?.TechnicalSkills ?? new())
                        .Concat(profile.Skills?.ConceptsAndTools ?? new())
                        .Concat(profile.Skills?.SoftSkills ?? new())
                        .Where(s => !string.IsNullOrWhiteSpace(s));
                    col.Item().Text(string.Join(", ", skills));
                });
            });
        }).GeneratePdf(path);
    }

    private static void Section(ColumnDescriptor col, string title)
    {
        col.Item().PaddingTop(10).BorderBottom(1).BorderColor("#222222").Text(title).Bold().FontSize(12);
    }

    private static string JoinContact(PersonalInfo identity)
    {
        return string.Join(" | ", new[] { identity.Email, identity.Phone, identity.Location, identity.LinkedInUrl, identity.GitHubUrl }
            .Where(s => !string.IsNullOrWhiteSpace(s)));
    }

    private static string BuildSummary(MasterUserProfile profile)
    {
        var title = profile.Identity?.TargetJobTitle;
        var skills = (profile.Skills?.TechnicalSkills ?? new()).Take(5);
        var skillText = string.Join(", ", skills);
        if (string.IsNullOrWhiteSpace(title) && string.IsNullOrWhiteSpace(skillText))
            return string.Empty;
        return $"{title} with demonstrated experience. Core skills: {skillText}.".Trim();
    }

    private static string Enc(string? value) => WebUtility.HtmlEncode(value ?? string.Empty);
}
