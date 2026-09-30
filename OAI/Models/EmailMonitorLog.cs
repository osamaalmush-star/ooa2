using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace OAI.Models
{
    [Table("EmailMonitorLogs")]
    public class EmailMonitorLog
    {
        [Key]
        public int Id { get; set; }

        [Required]
        [MaxLength(200)]
        public string Sender { get; set; } = string.Empty;

        [Required]
        [MaxLength(300)]
        public string Subject { get; set; } = string.Empty;

        public DateTime ReceivedAt { get; set; } = DateTime.UtcNow;

        [MaxLength(1000)]
        public string? ExtractedJobLink { get; set; }

        public double? MatchScore { get; set; }

        public bool NotificationTriggered { get; set; } = false;

        [Required]
        [MaxLength(50)]
        public string Status { get; set; } = "Processed";

        public string? LogDetails { get; set; }
    }
}
