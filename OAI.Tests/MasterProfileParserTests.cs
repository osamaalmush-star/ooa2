using OAI.Core.Services;
using Xunit;

namespace OAI.Tests;

public class MasterProfileParserTests
{
    [Fact]
    public void Parses_contact_experience_education_and_skills_from_plain_text()
    {
        var text = @"
Omar Ali
Software Engineer
omar@example.com
+966500000000
Riyadh, Saudi Arabia
linkedin.com/in/omarali
github.com/omarali

WORK EXPERIENCE
Acme Corp — Developer
- Built billing APIs that cut invoice time 30%
- Led a team of 4 engineers

EDUCATION
King Saud University — B.Sc Computer Science, 2018

SKILLS
C#, SQL, WPF, Git, EF Core, Communication
";

        var profile = MasterProfileParser.Parse(text);

        Assert.Equal("Omar Ali", profile.Identity.FullName);
        Assert.Equal("omar@example.com", profile.Identity.Email);
        Assert.Contains("966500000000", profile.Identity.Phone.Replace(" ", "").Replace("+", ""));
        Assert.False(string.IsNullOrWhiteSpace(profile.Identity.TargetJobTitle));
        Assert.NotEmpty(profile.Experiences);
        Assert.Contains(profile.Experiences, e => e.CompanyName.Contains("Acme"));
        Assert.NotEmpty(profile.Education);
        Assert.True((profile.Skills.TechnicalSkills.Count + profile.Skills.ConceptsAndTools.Count) >= 5);
    }

    [Fact]
    public void Empty_text_returns_empty_profile()
    {
        var profile = MasterProfileParser.Parse("   ");
        Assert.True(string.IsNullOrWhiteSpace(profile.Identity.FullName));
        Assert.Empty(profile.Experiences);
    }
}
