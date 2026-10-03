namespace OAI.Core.Models;

public class MasterUserProfile
{
    public PersonalInfo Identity { get; set; } = new();
    public List<string> TargetRoles { get; set; } = new();
    public List<WorkExperience> Experiences { get; set; } = new();
    public List<EducationItem> Education { get; set; } = new();
    public List<CertificationItem> Certifications { get; set; } = new();
    public SkillSet Skills { get; set; } = new();
    public DateTime LastUpdated { get; set; } = DateTime.UtcNow;
}

public class PersonalInfo
{
    public string FullName { get; set; } = string.Empty;
    public string TargetJobTitle { get; set; } = string.Empty;
    public string Email { get; set; } = string.Empty;
    public string Phone { get; set; } = string.Empty;
    public string Location { get; set; } = string.Empty;
    public string LinkedInUrl { get; set; } = string.Empty;
    public string GitHubUrl { get; set; } = string.Empty;
}

public class WorkExperience
{
    public string CompanyName { get; set; } = string.Empty;
    public string RoleTitle { get; set; } = string.Empty;
    public string Location { get; set; } = string.Empty;
    public DateTime StartDate { get; set; }
    public DateTime? EndDate { get; set; }
    public bool IsCurrentRole { get; set; }
    public List<string> ResponsibilitiesAndAchievements { get; set; } = new();
    public List<string> AssociatedSkills { get; set; } = new();
}

public class EducationItem
{
    public string Institution { get; set; } = string.Empty;
    public string Degree { get; set; } = string.Empty;
    public string FieldOfStudy { get; set; } = string.Empty;
    public string Location { get; set; } = string.Empty;
    public string GraduationYear { get; set; } = string.Empty;
}

public class CertificationItem
{
    public string Name { get; set; } = string.Empty;
    public string IssuingOrganization { get; set; } = string.Empty;
    public string IssueYear { get; set; } = string.Empty;
    public string CredentialUrl { get; set; } = string.Empty;
}

public class SkillSet
{
    public List<string> TechnicalSkills { get; set; } = new();
    public List<string> ConceptsAndTools { get; set; } = new();
    public List<string> SoftSkills { get; set; } = new();
}
