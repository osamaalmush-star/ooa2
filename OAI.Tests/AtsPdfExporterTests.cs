using OAI.Core.Models;
using OAI.Core.Services;
using Xunit;

namespace OAI.Tests;

public class AtsPdfExporterTests
{
    [Fact]
    public void Builds_single_column_ats_text_with_required_headings()
    {
        var profile = new MasterUserProfile
        {
            Identity =
            {
                FullName = "Omar Ali",
                TargetJobTitle = "Software Engineer",
                Email = "omar@example.com",
                Phone = "+966500000000",
                Location = "Riyadh"
            },
            Experiences =
            {
                new WorkExperience
                {
                    CompanyName = "Acme",
                    RoleTitle = "Developer",
                    ResponsibilitiesAndAchievements = { "Built billing APIs that cut invoice time 30%" }
                }
            },
            Education = { new EducationItem { Institution = "KSU", Degree = "B.Sc", FieldOfStudy = "CS", GraduationYear = "2018" } },
            Skills = { TechnicalSkills = { "C#", "SQL", "WPF" }, ConceptsAndTools = { "Git", "EF Core" } }
        };

        var html = AtsPdfExporter.BuildSingleColumnHtml(profile);

        Assert.Contains("PROFESSIONAL SUMMARY", html);
        Assert.Contains("WORK EXPERIENCE", html);
        Assert.Contains("EDUCATION", html);
        Assert.Contains("SKILLS", html);
        Assert.Contains("Omar Ali", html);
        Assert.DoesNotContain("<table", html, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("column-count", html, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("<img", html, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void Writes_pdf_file_to_disk()
    {
        var path = Path.Combine(Path.GetTempPath(), $"cv_{Guid.NewGuid():N}.pdf");
        try
        {
            var profile = new MasterUserProfile
            {
                Identity = { FullName = "Omar Ali", TargetJobTitle = "SE", Email = "o@x.com", Phone = "1" }
            };
            AtsPdfExporter.Export(profile, path);
            Assert.True(File.Exists(path));
            Assert.True(new FileInfo(path).Length > 100);
        }
        finally
        {
            try { if (File.Exists(path)) File.Delete(path); } catch { }
        }
    }
}
