using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace OAI.Models
{
    [Table("SearchSources")]
    public class SearchSource
    {
        [Key]
        public int Id { get; set; }

        [Required]
        [MaxLength(150)]
        public string PlatformName { get; set; } = string.Empty; // LinkedIn, Aramco Careers, Bayt, etc.

        [Required]
        [Column(TypeName = "nvarchar(1000)")]
        public string TargetUrl { get; set; } = string.Empty; // Full website or job search link

        [MaxLength(250)]
        public string? AccountUsername { get; set; } // Optional saved login handle/email for the platform

        public bool IsEnabled { get; set; } = true; // Status toggle

        /// <summary>
        /// Foreign key reference to UserProfile
        /// </summary>
        public int? UserProfileId { get; set; }

        [ForeignKey(nameof(UserProfileId))]
        public UserProfile? UserProfile { get; set; }

        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    }
}
