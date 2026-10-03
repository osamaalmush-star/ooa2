using System;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using OAI.Data;
using OAI.Models;

namespace OAI.Services
{
    public sealed class JobDiscoveryResult
    {
        public int NewJobs { get; set; }
        public int SourcesScanned { get; set; }
    }

    public sealed class JobDiscoveryService
    {
        public async Task<JobDiscoveryResult> DiscoverAsync(bool notifyHighMatches)
        {
            var result = new JobDiscoveryResult();
            using var db = new OAIDbContext();
            var enabledSources = db.SearchSources.Where(s => s.IsEnabled && !string.IsNullOrWhiteSpace(s.TargetUrl)).ToList();
            result.SourcesScanned = enabledSources.Count;
            if (enabledSources.Count == 0)
                return result;

            var profile = db.UserProfiles.FirstOrDefault();
            if (profile == null || string.IsNullOrWhiteSpace(profile.ResumeFilePath) || !File.Exists(profile.ResumeFilePath))
                return result;

            var engine = new JobMatchingEngine(new GeminiApiService());
            var scanner = new JobSourceScanner();

            foreach (var source in enabledSources)
            {
                var listings = await scanner.ScanAsync(source).ConfigureAwait(false);
                foreach (var listing in listings)
                {
                    if (!listing.IsOpen || listing.RequiresNationality)
                        continue;

                    if (db.JobApplications.Any(j => j.JobUrl == listing.Url))
                        continue;

                    var matchResult = await engine.EvaluateJobMatchAsync(profile, listing.Title, listing.Company, listing.Description).ConfigureAwait(false);
                    var advice = string.IsNullOrWhiteSpace(matchResult.Rationale)
                        ? "راجع الوصف قبل التقديم. الزر يفتح صفحة التقديم الحقيقية ويتوقف قبل الإرسال."
                        : matchResult.Rationale;

                    var job = new JobApplication
                    {
                        JobTitle = listing.Title,
                        CompanyName = listing.Company,
                        Location = listing.Location,
                        Platform = listing.Platform,
                        MatchScore = matchResult.MatchScore,
                        Status = "شاغرة",
                        JobUrl = listing.Url,
                        ApplyUrl = string.IsNullOrWhiteSpace(listing.ApplyUrl) ? listing.Url : listing.ApplyUrl,
                        JobDescription = listing.Description,
                        Salary = listing.Salary,
                        IsOpen = true,
                        HrEmail = listing.HrEmail,
                        Advice = advice,
                        MatchAnalysisJson = matchResult.RawJson,
                        CreatedAt = DateTime.UtcNow
                    };

                    db.JobApplications.Add(job);
                    await db.SaveChangesAsync().ConfigureAwait(false);
                    result.NewJobs++;

                    if (notifyHighMatches && job.MatchScore >= 80.0)
                        NotificationService.ShowJobAlertNotification(job.JobTitle, job.CompanyName, job.MatchScore, job.ApplyUrl);
                }
            }

            return result;
        }
    }
}
