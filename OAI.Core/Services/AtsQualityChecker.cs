using OAI.Core.Models;

namespace OAI.Core.Services;

public class AtsValidationResult
{
    public bool IsValid { get; set; }
    public int Score { get; set; }
    public List<string> MissingRequirements { get; set; } = new();
}

public class AtsQualityChecker
{
    public AtsValidationResult ValidateProfile(MasterUserProfile profile)
    {
        var result = new AtsValidationResult();
        profile ??= new MasterUserProfile();
        profile.Identity ??= new PersonalInfo();
        profile.Skills ??= new SkillSet();

        if (string.IsNullOrWhiteSpace(profile.Identity.FullName))
            result.MissingRequirements.Add("الاسم الكامل مطلوب.");
        if (string.IsNullOrWhiteSpace(profile.Identity.Email) || !profile.Identity.Email.Contains('@'))
            result.MissingRequirements.Add("بريد إلكتروني صحيح مطلوب.");
        if (string.IsNullOrWhiteSpace(profile.Identity.Phone))
            result.MissingRequirements.Add("رقم الهاتف مطلوب.");
        if (string.IsNullOrWhiteSpace(profile.Identity.TargetJobTitle))
            result.MissingRequirements.Add("المسمى الوظيفي المستهدف مطلوب.");

        if (profile.Experiences == null || profile.Experiences.Count == 0)
        {
            result.MissingRequirements.Add("يجب إضافة خبرة عملية واحدة على الأقل.");
        }
        else
        {
            foreach (var exp in profile.Experiences)
            {
                if (string.IsNullOrWhiteSpace(exp.CompanyName) || string.IsNullOrWhiteSpace(exp.RoleTitle))
                    result.MissingRequirements.Add("اسم الشركة والمسمى الوظيفي مطلوبان لكل خبرة.");
                if (exp.ResponsibilitiesAndAchievements == null || !exp.ResponsibilitiesAndAchievements.Any(a => !string.IsNullOrWhiteSpace(a)))
                    result.MissingRequirements.Add($"يرجى إضافة إنجازات/مهام مفصلة للخبرة في ({exp.CompanyName}).");
            }
        }

        if (profile.Education == null || !profile.Education.Any(e =>
                !string.IsNullOrWhiteSpace(e.Institution) || !string.IsNullOrWhiteSpace(e.Degree)))
        {
            result.MissingRequirements.Add("يجب إضافة المؤهل التعليمي (الدرجة والتخصص).");
        }

        var totalSkills = (profile.Skills.TechnicalSkills?.Count(s => !string.IsNullOrWhiteSpace(s)) ?? 0)
                          + (profile.Skills.ConceptsAndTools?.Count(s => !string.IsNullOrWhiteSpace(s)) ?? 0);
        if (totalSkills < 5)
            result.MissingRequirements.Add("يجب إدخال 5 مهارات تقنية/أدوات أساسية على الأقل.");

        result.IsValid = result.MissingRequirements.Count == 0;
        result.Score = result.IsValid ? 100 : Math.Max(0, 100 - (result.MissingRequirements.Count * 15));
        return result;
    }
}
