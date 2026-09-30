using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net.Security;
using System.Net.Sockets;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using OAI.Data;
using OAI.Models;

namespace OAI.Services
{
    public class EmailMonitorService
    {
        private readonly JobMatchingEngine _jobMatchingEngine;
        private static readonly Regex JobUrlRegex = new(
            @"https?://[^\s<>""']+",
            RegexOptions.IgnoreCase | RegexOptions.Compiled);

        public event Action<string>? OnLogEmitted;

        public EmailMonitorService(JobMatchingEngine jobMatchingEngine)
        {
            _jobMatchingEngine = jobMatchingEngine;
        }

        public static EmailSettings? LoadSettings()
        {
            try
            {
                using var db = new OAIDbContext();
                return db.EmailSettings.AsNoTracking().FirstOrDefault();
            }
            catch
            {
                return null;
            }
        }

        public async Task<int> PollJobEmailsAsync(EmailSettings settings, UserProfile profile)
        {
            if (settings == null || !settings.IsEnabled || string.IsNullOrWhiteSpace(settings.EmailAddress))
            {
                Log("مراقبة البريد غير مفعّلة أو لم تُحفظ بيانات الدخول.");
                return 0;
            }

            var decryptedPassword = EmailSecurityService.UnprotectPassword(settings.EncryptedAppPassword);
            if (string.IsNullOrWhiteSpace(decryptedPassword))
            {
                Log("تعذر فك تشفير كلمة مرور التطبيق.");
                return 0;
            }

            Log($"الاتصال بـ {settings.ImapServer}:{settings.ImapPort}");
            int processedAlerts = 0;

            try
            {
                using var client = new TcpClient();
                await client.ConnectAsync(settings.ImapServer, settings.ImapPort);

                using var sslStream = new SslStream(client.GetStream(), false, (_, _, _, _) => true);
                await sslStream.AuthenticateAsClientAsync(settings.ImapServer);

                using var reader = new StreamReader(sslStream, Encoding.ASCII);
                using var writer = new StreamWriter(sslStream, Encoding.ASCII) { AutoFlush = true, NewLine = "\r\n" };

                await reader.ReadLineAsync();

                await writer.WriteLineAsync($"A001 LOGIN \"{EscapeImap(settings.EmailAddress)}\" \"{EscapeImap(decryptedPassword)}\"");
                var login = await ReadUntilTagAsync(reader, "A001");
                if (!login.Contains("A001 OK", StringComparison.OrdinalIgnoreCase))
                {
                    Log("فشل تسجيل الدخول إلى IMAP.");
                    await LogToDatabaseAsync(settings.EmailAddress, "IMAP login failed", null, null, false, "AuthFailed");
                    return 0;
                }

                await writer.WriteLineAsync("A002 SELECT INBOX");
                await ReadUntilTagAsync(reader, "A002");

                await writer.WriteLineAsync("A003 SEARCH UNSEEN");
                var searchResponse = await ReadUntilTagAsync(reader, "A003");
                var messageIds = ParseSearchIds(searchResponse);

                Log($"رسائل غير مقروءة: {messageIds.Count}");

                foreach (var id in messageIds.Take(12))
                {
                    try
                    {
                        await writer.WriteLineAsync($"A{id} FETCH {id} (BODY.PEEK[HEADER.FIELDS (FROM SUBJECT)] BODY.PEEK[TEXT])");
                        var fetch = await ReadUntilTagAsync(reader, $"A{id}");
                        var (from, subject, body) = ParseFetch(fetch);
                        var jobLink = ExtractJobLink(subject + " " + body);

                        if (string.IsNullOrWhiteSpace(jobLink) || !IsUrlFromEnabledSource(jobLink))
                        {
                            await LogToDatabaseAsync(from, subject, jobLink, null, false, "Skipped");
                            continue;
                        }

                        var title = string.IsNullOrWhiteSpace(subject) ? "فرصة وظيفية" : subject;
                        var match = await _jobMatchingEngine.EvaluateJobMatchAsync(profile, title, from, body);
                        var notified = match.MatchScore >= settings.MinMatchScoreThreshold;

                        if (notified)
                        {
                            NotificationService.ShowJobAlertNotification(title, from, match.MatchScore, jobLink);
                            processedAlerts++;
                        }

                        await SaveDiscoveredJobAsync(title, from, jobLink, body, match.MatchScore);
                        await LogToDatabaseAsync(from, subject, jobLink, match.MatchScore, notified, notified ? "Matched" : "Processed");
                    }
                    catch (Exception ex)
                    {
                        Log($"خطأ في رسالة {id}: {ex.Message}");
                    }
                }

                await writer.WriteLineAsync("A999 LOGOUT");
                await ReadUntilTagAsync(reader, "A999");
                Log("انتهت جلسة مراقبة البريد.");
            }
            catch (Exception ex)
            {
                Log($"خطأ مراقبة البريد: {ex.Message}");
                await LogToDatabaseAsync(settings.EmailAddress, "Error Polling Inbox", null, null, false, $"Error: {ex.Message}");
            }

            return processedAlerts;
        }

        private static string EscapeImap(string value) => value.Replace("\\", "\\\\").Replace("\"", "\\\"");

        private static List<int> ParseSearchIds(string searchResponse)
        {
            var ids = new List<int>();
            var star = searchResponse.Split('\n').FirstOrDefault(l => l.StartsWith("* SEARCH", StringComparison.OrdinalIgnoreCase));
            if (string.IsNullOrWhiteSpace(star)) return ids;

            foreach (var token in star.Replace("* SEARCH", "", StringComparison.OrdinalIgnoreCase).Split(' ', StringSplitOptions.RemoveEmptyEntries))
            {
                if (int.TryParse(token.Trim(), out var id))
                    ids.Add(id);
            }
            return ids;
        }

        private static (string from, string subject, string body) ParseFetch(string fetch)
        {
            var from = ExtractHeader(fetch, "From") ?? "unknown";
            var subject = ExtractHeader(fetch, "Subject") ?? string.Empty;
            var body = fetch;
            return (from, subject, body);
        }

        private static string? ExtractHeader(string text, string name)
        {
            var match = Regex.Match(text, $"^{Regex.Escape(name)}:\\s*(.+)$", RegexOptions.IgnoreCase | RegexOptions.Multiline);
            return match.Success ? match.Groups[1].Value.Trim() : null;
        }

        private static string? ExtractJobLink(string text)
        {
            foreach (Match match in JobUrlRegex.Matches(text ?? string.Empty))
            {
                var url = match.Value.TrimEnd('.', ',', ')', ']');
                if (url.Contains("job", StringComparison.OrdinalIgnoreCase) ||
                    url.Contains("career", StringComparison.OrdinalIgnoreCase) ||
                    url.Contains("linkedin.com", StringComparison.OrdinalIgnoreCase) ||
                    url.Contains("indeed.", StringComparison.OrdinalIgnoreCase) ||
                    url.Contains("wuzzuf.", StringComparison.OrdinalIgnoreCase) ||
                    url.Contains("glassdoor.", StringComparison.OrdinalIgnoreCase))
                {
                    return url;
                }
            }
            return JobUrlRegex.Match(text ?? string.Empty).Success ? JobUrlRegex.Match(text).Value : null;
        }

        private static async Task SaveDiscoveredJobAsync(string title, string company, string url, string description, double score)
        {
            try
            {
                using var db = new OAIDbContext();
                if (db.JobApplications.Any(j => j.JobUrl == url || (j.JobTitle == title && j.CompanyName == company)))
                    return;

                db.JobApplications.Add(new JobApplication
                {
                    JobTitle = title.Length > 250 ? title[..250] : title,
                    CompanyName = company.Length > 200 ? company[..200] : company,
                    JobUrl = url.Length > 1000 ? url[..1000] : url,
                    Platform = "Email",
                    JobDescription = description,
                    MatchScore = score,
                    Status = score >= 80 ? "PendingSubmit" : "Discovered",
                    CreatedAt = DateTime.UtcNow
                });
                await db.SaveChangesAsync();
            }
            catch
            {
            }
        }

        private static async Task<string> ReadUntilTagAsync(StreamReader reader, string tag)
        {
            var sb = new StringBuilder();
            string? line;
            while ((line = await reader.ReadLineAsync()) != null)
            {
                sb.AppendLine(line);
                if (line.StartsWith(tag, StringComparison.OrdinalIgnoreCase))
                    break;
            }
            return sb.ToString();
        }

        private async Task LogToDatabaseAsync(string sender, string subject, string? jobLink, double? matchScore, bool notificationSent, string status)
        {
            try
            {
                using var db = new OAIDbContext();
                db.EmailMonitorLogs.Add(new EmailMonitorLog
                {
                    Sender = Truncate(sender, 200),
                    Subject = Truncate(subject, 300),
                    ExtractedJobLink = jobLink,
                    MatchScore = matchScore,
                    NotificationTriggered = notificationSent,
                    Status = Truncate(status, 50),
                    ReceivedAt = DateTime.UtcNow,
                    LogDetails = $"MatchScore: {matchScore:F1}%, ToastSent: {notificationSent}"
                });
                await db.SaveChangesAsync();
            }
            catch (Exception ex)
            {
                Log($"فشل كتابة سجل البريد: {ex.Message}");
            }
        }

        private static string Truncate(string value, int max)
        {
            if (string.IsNullOrEmpty(value)) return string.Empty;
            return value.Length <= max ? value : value[..max];
        }

        public static List<string> GetEnabledSearchSourcesUrls()
        {
            try
            {
                using var db = new OAIDbContext();
                return db.SearchSources.Where(s => s.IsEnabled && !string.IsNullOrEmpty(s.TargetUrl)).Select(s => s.TargetUrl).ToList();
            }
            catch
            {
                return new List<string>();
            }
        }

        public static bool IsUrlFromEnabledSource(string url)
        {
            if (string.IsNullOrWhiteSpace(url)) return false;

            try
            {
                using var db = new OAIDbContext();
                var activeSources = db.SearchSources.Where(s => s.IsEnabled).ToList();
                if (!activeSources.Any()) return true;

                foreach (var source in activeSources)
                {
                    if (string.IsNullOrWhiteSpace(source.TargetUrl)) continue;

                    if (Uri.TryCreate(source.TargetUrl, UriKind.Absolute, out var sourceUri) &&
                        Uri.TryCreate(url, UriKind.Absolute, out var targetUri))
                    {
                        if (targetUri.Host.EndsWith(sourceUri.Host, StringComparison.OrdinalIgnoreCase) ||
                            sourceUri.Host.EndsWith(targetUri.Host, StringComparison.OrdinalIgnoreCase))
                            return true;
                    }
                    else if (!string.IsNullOrWhiteSpace(source.PlatformName) &&
                             url.Contains(source.PlatformName, StringComparison.OrdinalIgnoreCase))
                    {
                        return true;
                    }
                }
            }
            catch
            {
            }
            return false;
        }

        private void Log(string message)
        {
            OnLogEmitted?.Invoke($"[EmailMonitor] {DateTime.Now:HH:mm:ss} - {message}");
        }
    }
}
