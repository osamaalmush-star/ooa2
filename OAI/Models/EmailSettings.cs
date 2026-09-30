using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace OAI.Models
{
    [Table("EmailSettings")]
    public class EmailSettings
    {
        [Key]
        public int Id { get; set; }

        public bool IsEnabled { get; set; }

        [MaxLength(200)]
        public string EmailAddress { get; set; } = string.Empty;

        [MaxLength(200)]
        public string ImapServer { get; set; } = "imap.gmail.com";

        public int ImapPort { get; set; } = 993;

        public bool UseSsl { get; set; } = true;

        [MaxLength(2000)]
        public string EncryptedAppPassword { get; set; } = string.Empty;

        public int PollingIntervalMinutes { get; set; } = 15;

        public double MinMatchScoreThreshold { get; set; } = 70.0;
    }
}
