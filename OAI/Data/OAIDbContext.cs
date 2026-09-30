using Microsoft.EntityFrameworkCore;
using OAI.Models;

namespace OAI.Data
{
    public class OAIDbContext : DbContext
    {
        public const string DefaultConnectionString = @"Server=(localdb)\mssqllocaldb;Database=OAIDatabase;Trusted_Connection=True;MultipleActiveResultSets=true;TrustServerCertificate=True;";

        public OAIDbContext()
        {
        }

        public OAIDbContext(DbContextOptions<OAIDbContext> options) : base(options)
        {
        }

        public DbSet<UserProfile> UserProfiles { get; set; } = null!;
        public DbSet<SearchSource> SearchSources { get; set; } = null!;
        public DbSet<JobApplication> JobApplications { get; set; } = null!;
        public DbSet<EmailMonitorLog> EmailMonitorLogs { get; set; } = null!;
        public DbSet<EmailSettings> EmailSettings { get; set; } = null!;
        public DbSet<AppSetting> AppSettings { get; set; } = null!;

        protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder)
        {
            if (!optionsBuilder.IsConfigured)
            {
                optionsBuilder.UseSqlServer(DefaultConnectionString);
            }
        }

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            base.OnModelCreating(modelBuilder);

            // Configure UserProfile nvarchar(max) text columns
            modelBuilder.Entity<UserProfile>(entity =>
            {
                entity.Property(e => e.ResumeFilePath).HasMaxLength(1000);
                entity.Property(e => e.TargetJobTitles).HasColumnType("nvarchar(max)");
                entity.Property(e => e.Skills).HasColumnType("nvarchar(max)");
                entity.Property(e => e.ExperienceSummary).HasColumnType("nvarchar(max)");
                entity.Property(e => e.RawResumeContent).HasColumnType("nvarchar(max)");
                entity.Property(e => e.ParsedProfileJson).HasColumnType("nvarchar(max)");
                entity.Property(e => e.Education).HasColumnType("nvarchar(max)");
                entity.Property(e => e.Keywords).HasColumnType("nvarchar(max)");
            });

            // Configure SearchSource entity
            modelBuilder.Entity<SearchSource>(entity =>
            {
                entity.Property(e => e.TargetUrl).HasColumnType("nvarchar(1000)");
                entity.Property(e => e.PlatformName).HasMaxLength(150);
                entity.Property(e => e.AccountUsername).HasMaxLength(250);

                entity.HasOne(e => e.UserProfile)
                      .WithMany()
                      .HasForeignKey(e => e.UserProfileId)
                      .OnDelete(DeleteBehavior.SetNull);
            });

            // Configure JobApplication nvarchar(max) text columns
            modelBuilder.Entity<JobApplication>(entity =>
            {
                entity.Property(e => e.JobDescription).HasColumnType("nvarchar(max)");
                entity.Property(e => e.MatchAnalysisJson).HasColumnType("nvarchar(max)");
                entity.Property(e => e.Notes).HasColumnType("nvarchar(max)");
                entity.Property(e => e.Advice).HasColumnType("nvarchar(max)");
                entity.Property(e => e.Salary).HasMaxLength(150);
                entity.Property(e => e.HrEmail).HasMaxLength(250);
                entity.Property(e => e.ApplyUrl).HasMaxLength(1000);
            });

            modelBuilder.Entity<EmailMonitorLog>(entity =>
            {
                entity.Property(e => e.LogDetails).HasColumnType("nvarchar(max)");
            });

            modelBuilder.Entity<EmailSettings>(entity =>
            {
                entity.Property(e => e.EncryptedAppPassword).HasMaxLength(2000);
            });
        }
    }
}
