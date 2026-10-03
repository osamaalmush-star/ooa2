using System.IO;
using System.Linq;
using OAI.Core.Services;
using OAI.Data;
using OAI.Models;

namespace OAI.Services
{
    public class SetupStatus
    {
        public bool HasResume { get; set; }
        public bool HasEmail { get; set; }
        public bool HasSource { get; set; }
        public bool HasSchedule { get; set; }
        public bool IsComplete { get; set; }
    }

    public static class SetupStatusService
    {
        public static SetupStatus GetStatus()
        {
            using var db = new OAIDbContext();
            var profile = db.UserProfiles.FirstOrDefault();
            var email = db.EmailSettings.FirstOrDefault();
            var settings = db.AppSettings.FirstOrDefault();
            var hasSource = db.SearchSources.Any(s => s.IsEnabled && !string.IsNullOrWhiteSpace(s.TargetUrl));

            var master = TryLoadMaster();
            var masterValid = master != null && new AtsQualityChecker().ValidateProfile(master).IsValid;
            var hasResume = masterValid
                && profile != null
                && !string.IsNullOrWhiteSpace(profile.ResumeFilePath)
                && File.Exists(profile.ResumeFilePath)
                && !string.IsNullOrWhiteSpace(profile.FullName);

            var hasEmail = email != null
                && email.IsEnabled
                && !string.IsNullOrWhiteSpace(email.EmailAddress)
                && !string.IsNullOrWhiteSpace(email.EncryptedAppPassword);

            var hasSchedule = settings != null && settings.MonitoringIntervalMinutes > 0;

            return new SetupStatus
            {
                HasResume = hasResume,
                HasEmail = hasEmail,
                HasSource = hasSource,
                HasSchedule = hasSchedule,
                IsComplete = settings?.IsSetupComplete == true && hasResume && hasSource && !string.IsNullOrWhiteSpace(settings.GeminiApiKey)
            };
        }

        public static AppSetting GetOrCreateSettings()
        {
            using var db = new OAIDbContext();
            var settings = db.AppSettings.FirstOrDefault();
            if (settings == null)
            {
                settings = new AppSetting { GeminiModelName = "gemini-2.0-flash", MonitoringIntervalMinutes = 60 };
                db.AppSettings.Add(settings);
                db.SaveChanges();
            }
            return settings;
        }

        private static OAI.Core.Models.MasterUserProfile? TryLoadMaster()
        {
            try
            {
                return new MasterProfileStore().Load();
            }
            catch
            {
                return null;
            }
        }
    }
}
