using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace OAI.Models
{
    [Table("AppSettings")]
    public class AppSetting
    {
        [Key]
        public int Id { get; set; }

        [MaxLength(300)]
        public string GeminiApiKey { get; set; } = string.Empty;

        [MaxLength(80)]
        public string GeminiModelName { get; set; } = "gemini-2.0-flash";

        public bool IsSetupComplete { get; set; }

        public int MonitoringIntervalMinutes { get; set; } = 60;
    }
}
