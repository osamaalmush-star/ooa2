using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace OAI.Models
{
    [Table("JobApplications")]
    public class JobApplication
    {
        [Key]
        public int Id { get; set; }

        [Required]
        [MaxLength(250)]
        public string JobTitle { get; set; } = string.Empty;

        [Required]
        [MaxLength(200)]
        public string CompanyName { get; set; } = string.Empty;

        [MaxLength(150)]
        public string? Location { get; set; }

        [Required]
        [MaxLength(1000)]
        public string JobUrl { get; set; } = string.Empty;

        [MaxLength(100)]
        public string Platform { get; set; } = string.Empty;

        public string? JobDescription { get; set; }

        /// <summary>
        /// Gemini AI Match score (0 to 100 percentage)
        /// </summary>
        public double MatchScore { get; set; }

        /// <summary>
        /// JSON object with breakdown of match analysis (matching skills, missing skills, rationale)
        /// </summary>
        public string? MatchAnalysisJson { get; set; }

        /// <summary>
        /// Status: Discovered, Matched, PendingSubmit, Submitted, Rejected, Interviewing
        /// </summary>
        [Required]
        [MaxLength(50)]
        public string Status { get; set; } = "Discovered";

        [MaxLength(150)]
        public string Salary { get; set; } = string.Empty;

        public bool IsOpen { get; set; } = true;

        [MaxLength(250)]
        public string? HrEmail { get; set; }

        [MaxLength(1000)]
        public string ApplyUrl { get; set; } = string.Empty;

        [Column(TypeName = "nvarchar(max)")]
        public string Advice { get; set; } = string.Empty;

        public DateTime? AppliedAt { get; set; }

        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

        public string? Notes { get; set; }
    }
}
