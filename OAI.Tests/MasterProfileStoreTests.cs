using OAI.Core.Models;
using OAI.Core.Services;
using Xunit;

namespace OAI.Tests;

public class MasterProfileStoreTests : IDisposable
{
    private readonly string _dbPath = Path.Combine(Path.GetTempPath(), $"oai_test_{Guid.NewGuid():N}.db");

    [Fact]
    public void Save_then_load_roundtrips_profile()
    {
        var store = new MasterProfileStore(_dbPath);
        var profile = new MasterUserProfile
        {
            Identity = { FullName = "Omar Ali", Email = "omar@example.com", Phone = "500", TargetJobTitle = "SE" },
            Experiences = { new WorkExperience { CompanyName = "Acme", RoleTitle = "Dev", ResponsibilitiesAndAchievements = { "Ship" } } },
            Education = { new EducationItem { Institution = "KSU", Degree = "B.Sc" } },
            Skills = { TechnicalSkills = { "C#", "SQL" } }
        };

        store.Save(profile);
        var loaded = store.Load();

        Assert.NotNull(loaded);
        Assert.Equal("Omar Ali", loaded!.Identity.FullName);
        Assert.Equal("omar@example.com", loaded.Identity.Email);
        Assert.Single(loaded.Experiences);
        Assert.Equal("Acme", loaded.Experiences[0].CompanyName);
        Assert.Contains("C#", loaded.Skills.TechnicalSkills);
    }

    public void Dispose()
    {
        try { if (File.Exists(_dbPath)) File.Delete(_dbPath); } catch { }
    }
}
