using OAI.Core.Services;
using Xunit;

namespace OAI.Tests;

public class MasterProfileJsonMapperTests
{
    [Fact]
    public void Maps_structured_json_into_master_profile()
    {
        var json = """
        {
          "identity": {
            "fullName": "Omar Ali",
            "email": "omar@example.com",
            "phone": "+966500000000",
            "targetJobTitle": "Software Engineer"
          },
          "experiences": [
            { "companyName": "Acme", "roleTitle": "Developer", "responsibilitiesAndAchievements": ["Shipped APIs"] }
          ],
          "education": [
            { "institution": "KSU", "degree": "B.Sc", "fieldOfStudy": "CS" }
          ],
          "skills": {
            "technicalSkills": ["C#", "SQL"],
            "conceptsAndTools": ["Git"]
          }
        }
        """;

        var profile = MasterProfileJsonMapper.FromJson(json);

        Assert.Equal("Omar Ali", profile.Identity.FullName);
        Assert.Equal("omar@example.com", profile.Identity.Email);
        Assert.Single(profile.Experiences);
        Assert.Equal("Acme", profile.Experiences[0].CompanyName);
        Assert.Contains("C#", profile.Skills.TechnicalSkills);
    }

    [Fact]
    public void Invalid_json_returns_empty_profile()
    {
        var profile = MasterProfileJsonMapper.FromJson("not-json");
        Assert.True(string.IsNullOrWhiteSpace(profile.Identity.FullName));
    }
}
