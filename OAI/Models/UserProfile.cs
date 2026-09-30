using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace OAI.Models
{
    [Table("UserProfiles")]
    public class UserProfile
    {
        [Key]
        public int Id { get; set; }

        [Required]
        [MaxLength(150)]
        public string FullName { get; set; } = string.Empty;

        [Required]
        [MaxLength(150)]
        public string Email { get; set; } = string.Empty;

        [MaxLength(50)]
        public string PhoneNumber { get; set; } = string.Empty;

        /// <summary>
        /// Backward-compatible alias for PhoneNumber
        /// </summary>
        [NotMapped]
        public string Phone
        {
            get => PhoneNumber;
            set => PhoneNumber = value;
        }

        [MaxLength(50)]
        public string Age { get; set; } = string.Empty;

        [MaxLength(50)]
        public string Gender { get; set; } = string.Empty;

        [MaxLength(200)]
        public string TargetJobTitle { get; set; } = string.Empty;

        /// <summary>
        /// Backward-compatible alias for TargetJobTitle
        /// </summary>
        [Column(TypeName = "nvarchar(max)")]
        public string TargetJobTitles
        {
            get => string.IsNullOrEmpty(TargetJobTitle) ? "[]" : TargetJobTitle;
            set
            {
                TargetJobTitle = value;
            }
        }

        [MaxLength(100)]
        public string YearsOfExperience { get; set; } = string.Empty;

        [Column(TypeName = "nvarchar(max)")]
        public string Education { get; set; } = string.Empty;

        [Column(TypeName = "nvarchar(max)")]
        public string Keywords { get; set; } = string.Empty;

        [Column(TypeName = "nvarchar(max)")]
        public string SkillsList { get; set; } = string.Empty;

        /// <summary>
        /// Backward-compatible alias for SkillsList
        /// </summary>
        [Column(TypeName = "nvarchar(max)")]
        public string Skills
        {
            get => string.IsNullOrEmpty(SkillsList) ? "[]" : SkillsList;
            set
            {
                SkillsList = value;
            }
        }

        [MaxLength(150)]
        public string Location { get; set; } = string.Empty;

        [MaxLength(250)]
        public string? LinkedInUrl { get; set; }

        [MaxLength(250)]
        public string? GitHubUrl { get; set; }

        [MaxLength(250)]
        public string? PortfolioUrl { get; set; }

        /// <summary>
        /// Local saved absolute path of original PDF CV document
        /// </summary>
        [Column(TypeName = "nvarchar(1000)")]
        public string? ResumeFilePath { get; set; }

        /// <summary>
        /// Detailed experience summary or formatted resume sections
        /// </summary>
        [Column(TypeName = "nvarchar(max)")]
        public string ExperienceSummary { get; set; } = string.Empty;

        /// <summary>
        /// Plain text extracted from original CV document
        /// </summary>
        [Column(TypeName = "nvarchar(max)")]
        public string? RawResumeContent { get; set; }

        /// <summary>
        /// Full JSON parsed profile output from Gemini ATS parser
        /// </summary>
        [Column(TypeName = "nvarchar(max)")]
        public string? ParsedProfileJson { get; set; }

        public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
    }
}
