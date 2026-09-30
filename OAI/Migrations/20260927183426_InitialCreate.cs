using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

#pragma warning disable CA1814 // Prefer jagged arrays over multidimensional

namespace OAI.Migrations
{
    /// <inheritdoc />
    public partial class InitialCreate : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "EmailMonitorLogs",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    Sender = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    Subject = table.Column<string>(type: "nvarchar(300)", maxLength: 300, nullable: false),
                    ReceivedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    ExtractedJobLink = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: true),
                    MatchScore = table.Column<double>(type: "float", nullable: true),
                    NotificationTriggered = table.Column<bool>(type: "bit", nullable: false),
                    Status = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                    LogDetails = table.Column<string>(type: "nvarchar(max)", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_EmailMonitorLogs", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "JobApplications",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    JobTitle = table.Column<string>(type: "nvarchar(250)", maxLength: 250, nullable: false),
                    CompanyName = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    Location = table.Column<string>(type: "nvarchar(150)", maxLength: 150, nullable: true),
                    JobUrl = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: false),
                    Platform = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    JobDescription = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    MatchScore = table.Column<double>(type: "float", nullable: false),
                    MatchAnalysisJson = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    Status = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                    AppliedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    Notes = table.Column<string>(type: "nvarchar(max)", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_JobApplications", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "SearchSources",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    PlatformName = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    BaseUrl = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: false),
                    IsEnabled = table.Column<bool>(type: "bit", nullable: false),
                    FilterKeywords = table.Column<string>(type: "nvarchar(250)", maxLength: 250, nullable: true),
                    LocationFilter = table.Column<string>(type: "nvarchar(150)", maxLength: 150, nullable: true),
                    AutoApplyEnabled = table.Column<bool>(type: "bit", nullable: false),
                    DailyApplyLimit = table.Column<int>(type: "int", nullable: false),
                    LastSearchedAt = table.Column<DateTime>(type: "datetime2", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_SearchSources", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "UserProfiles",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    FullName = table.Column<string>(type: "nvarchar(150)", maxLength: 150, nullable: false),
                    Email = table.Column<string>(type: "nvarchar(150)", maxLength: 150, nullable: false),
                    Phone = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                    Location = table.Column<string>(type: "nvarchar(150)", maxLength: 150, nullable: false),
                    LinkedInUrl = table.Column<string>(type: "nvarchar(250)", maxLength: 250, nullable: true),
                    GitHubUrl = table.Column<string>(type: "nvarchar(250)", maxLength: 250, nullable: true),
                    PortfolioUrl = table.Column<string>(type: "nvarchar(250)", maxLength: 250, nullable: true),
                    TargetJobTitles = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    Skills = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    ExperienceSummary = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    RawResumeContent = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    ParsedProfileJson = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_UserProfiles", x => x.Id);
                });

            migrationBuilder.InsertData(
                table: "SearchSources",
                columns: new[] { "Id", "AutoApplyEnabled", "BaseUrl", "DailyApplyLimit", "FilterKeywords", "IsEnabled", "LastSearchedAt", "LocationFilter", "PlatformName" },
                values: new object[,]
                {
                    { 1, false, "https://www.linkedin.com/jobs", 25, ".NET Developer, C# Developer", true, null, null, "LinkedIn" },
                    { 2, false, "https://www.indeed.com", 20, "Full Stack C# .NET", true, null, null, "Indeed" },
                    { 3, false, "https://www.glassdoor.com", 15, "Software Engineer .NET", false, null, null, "Glassdoor" },
                    { 4, false, "https://wuzzuf.net", 30, ".NET Core, WPF, C#", true, null, null, "Wuzzuf" }
                });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "EmailMonitorLogs");

            migrationBuilder.DropTable(
                name: "JobApplications");

            migrationBuilder.DropTable(
                name: "SearchSources");

            migrationBuilder.DropTable(
                name: "UserProfiles");
        }
    }
}
