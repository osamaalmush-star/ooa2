using OAI.Core.Models;
using OAI.Core.Services;
using Xunit;

namespace OAI.Tests;

public class AtsQualityCheckerTests
{
    private readonly AtsQualityChecker _checker = new();

    [Fact]
    public void Empty_profile_is_invalid_and_lists_all_core_gaps()
    {
        var result = _checker.ValidateProfile(new MasterUserProfile());

        Assert.False(result.IsValid);
        Assert.True(result.Score < 100);
        Assert.Contains(result.MissingRequirements, m => m.Contains("الاسم الكامل"));
        Assert.Contains(result.MissingRequirements, m => m.Contains("بريد"));
        Assert.Contains(result.MissingRequirements, m => m.Contains("الهاتف"));
        Assert.Contains(result.MissingRequirements, m => m.Contains("المسمى الوظيفي"));
        Assert.Contains(result.MissingRequirements, m => m.Contains("خبرة"));
        Assert.Contains(result.MissingRequirements, m => m.Contains("المؤهل"));
        Assert.Contains(result.MissingRequirements, m => m.Contains("مهارات"));
    }

    [Fact]
    public void Rejects_email_without_at_sign()
    {
        var profile = CompleteProfile();
        profile.Identity.Email = "not-an-email";

        var result = _checker.ValidateProfile(profile);

        Assert.False(result.IsValid);
        Assert.Contains(result.MissingRequirements, m => m.Contains("بريد"));
    }

    [Fact]
    public void Rejects_experience_without_achievements()
    {
        var profile = CompleteProfile();
        profile.Experiences[0].ResponsibilitiesAndAchievements.Clear();

        var result = _checker.ValidateProfile(profile);

        Assert.False(result.IsValid);
        Assert.Contains(result.MissingRequirements, m => m.Contains("إنجازات"));
    }

    [Fact]
    public void Rejects_fewer_than_five_technical_or_tool_skills()
    {
        var profile = CompleteProfile();
        profile.Skills.TechnicalSkills = new List<string> { "C#", "SQL" };
        profile.Skills.ConceptsAndTools = new List<string> { "Git" };

        var result = _checker.ValidateProfile(profile);

        Assert.False(result.IsValid);
        Assert.Contains(result.MissingRequirements, m => m.Contains("5"));
    }

    [Fact]
    public void Complete_profile_is_valid_with_score_100()
    {
        var result = _checker.ValidateProfile(CompleteProfile());

        Assert.True(result.IsValid);
        Assert.Equal(100, result.Score);
        Assert.Empty(result.MissingRequirements);
    }

    private static MasterUserProfile CompleteProfile()
    {
        return new MasterUserProfile
        {
            Identity = new PersonalInfo
            {
                FullName = "Omar Ali",
                Email = "omar@example.com",
                Phone = "+966500000000",
                TargetJobTitle = "Software Engineer",
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
            Education =
            {
                new EducationItem { Institution = "KSU", Degree = "B.Sc", FieldOfStudy = "CS", GraduationYear = "2018" }
            },
            Skills = new SkillSet
            {
                TechnicalSkills = { "C#", "SQL", "WPF" },
                ConceptsAndTools = { "Git", "EF Core" }
            }
        };
    }
}
