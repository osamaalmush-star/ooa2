using System;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Input;
using OAI.Data;
using OAI.Models;
using OAI.Services;
using OAI.ViewModels.Base;

namespace OAI.ViewModels
{
    public class DashboardViewModel : ViewModelBase
    {
        private readonly PlaywrightAutomationService _playwrightService;
        private readonly GeminiApiService _geminiService;

        // KPI Counters
        private int _totalJobsFound;
        public int TotalJobsFound
        {
            get => _totalJobsFound;
            set => SetProperty(ref _totalJobsFound, value);
        }

        private int _totalAppliedHardStop;
        public int TotalAppliedHardStop
        {
            get => _totalAppliedHardStop;
            set => SetProperty(ref _totalAppliedHardStop, value);
        }

        private int _interviewsCount;
        public int InterviewsCount
        {
            get => _interviewsCount;
            set => SetProperty(ref _interviewsCount, value);
        }

        private int _pendingReviewCount;
        public int PendingReviewCount
        {
            get => _pendingReviewCount;
            set => SetProperty(ref _pendingReviewCount, value);
        }

        // Filters
        private DateTime? _fromDate = DateTime.Now.AddDays(-30);
        public DateTime? FromDate
        {
            get => _fromDate;
            set { SetProperty(ref _fromDate, value); FilterApplications(); }
        }

        private DateTime? _toDate = DateTime.Now;
        public DateTime? ToDate
        {
            get => _toDate;
            set { SetProperty(ref _toDate, value); FilterApplications(); }
        }

        private string _selectedStatusFilter = "All";
        public string SelectedStatusFilter
        {
            get => _selectedStatusFilter;
            set { SetProperty(ref _selectedStatusFilter, value); FilterApplications(); }
        }

        private ObservableCollection<JobApplication> _allApplications = new ObservableCollection<JobApplication>();
        private ObservableCollection<JobApplication> _filteredApplications = new ObservableCollection<JobApplication>();
        public ObservableCollection<JobApplication> FilteredApplications
        {
            get => _filteredApplications;
            set => SetProperty(ref _filteredApplications, value);
        }

        private JobApplication? _selectedApplication;
        public JobApplication? SelectedApplication
        {
            get => _selectedApplication;
            set { SetProperty(ref _selectedApplication, value); UpdateCopilotAnalysis(); }
        }

        // AI Career Copilot Panel Properties
        private string _copilotSkillGapAnalysis = "اختر وظيفة لعرض تحليل المطابقة.";
        public string CopilotSkillGapAnalysis
        {
            get => _copilotSkillGapAnalysis;
            set => SetProperty(ref _copilotSkillGapAnalysis, value);
        }

        private string _copilotColdOutreachMessage = string.Empty;
        public string CopilotColdOutreachMessage
        {
            get => _copilotColdOutreachMessage;
            set => SetProperty(ref _copilotColdOutreachMessage, value);
        }

        private bool _isBusy;
        public bool IsBusy
        {
            get => _isBusy;
            set => SetProperty(ref _isBusy, value);
        }

        private readonly JobMatchingEngine _jobMatchingEngine;

        public ICommand RefreshCommand { get; }
        public ICommand DiscoverJobsCommand { get; }
        public ICommand ResumeApplicationCommand { get; }
        public ICommand GenerateOutreachCommand { get; }

        public DashboardViewModel()
        {
            _playwrightService = new PlaywrightAutomationService();
            _geminiService = new GeminiApiService();
            _jobMatchingEngine = new JobMatchingEngine(_geminiService);

            RefreshCommand = new RelayCommand(LoadDataFromDatabase);
            DiscoverJobsCommand = new RelayCommand(async () => await DiscoverJobsAsync());
            ResumeApplicationCommand = new RelayCommand(async (param) => await ResumeApplicationAsync(param as JobApplication));
            GenerateOutreachCommand = new RelayCommand(async () => await GenerateOutreachAsync());

            LoadDataFromDatabase();
        }

        public void LoadDataFromDatabase()
        {
            try
            {
                using var db = new OAIDbContext();

                var list = db.JobApplications.OrderByDescending(j => j.MatchScore).ThenByDescending(j => j.CreatedAt).ToList();
                _allApplications = new ObservableCollection<JobApplication>(list);

                // Update KPI Statistics
                TotalJobsFound = _allApplications.Count;
                TotalAppliedHardStop = _allApplications.Count(j => j.Status == "PendingSubmit" || j.Status == "Submitted");
                InterviewsCount = _allApplications.Count(j => j.Status == "Interviewing");
                PendingReviewCount = _allApplications.Count(j => j.Status == "Discovered" || j.Status == "PendingSubmit");

                FilterApplications();
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Error loading dashboard data: {ex.Message}", "OAI Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void FilterApplications()
        {
            var filtered = _allApplications.AsEnumerable().Where(j => j.IsOpen);

            if (FromDate.HasValue)
            {
                filtered = filtered.Where(j => j.CreatedAt >= FromDate.Value.Date);
            }

            if (ToDate.HasValue)
            {
                filtered = filtered.Where(j => j.CreatedAt <= ToDate.Value.Date.AddDays(1));
            }

            if (!string.IsNullOrEmpty(SelectedStatusFilter) && SelectedStatusFilter != "All" && SelectedStatusFilter != "الكل")
            {
                filtered = filtered.Where(j =>
                    j.Status.Equals(SelectedStatusFilter, StringComparison.OrdinalIgnoreCase) ||
                    (SelectedStatusFilter == "جاهز للمراجعة (Hard Stop)" && j.Status == "PendingSubmit") ||
                    (SelectedStatusFilter == "تم التقديم" && j.Status == "Submitted") ||
                    (SelectedStatusFilter == "مقابلات" && j.Status == "Interviewing") ||
                    (SelectedStatusFilter == "وظائف مكتشفة" && (j.Status == "Discovered" || j.Status == "شاغرة")));
            }

            FilteredApplications = new ObservableCollection<JobApplication>(filtered.OrderByDescending(j => j.MatchScore));
        }

        public async Task DiscoverJobsAsync()
        {
            IsBusy = true;
            try
            {
                using var db = new OAIDbContext();
                var enabledSources = db.SearchSources.Where(s => s.IsEnabled).ToList();
                if (!enabledSources.Any())
                {
                    MessageBox.Show("لا توجد مصادر بحث مفعلة حالياً. يرجى تفعيل مصادر من شاشة 'المصادر والقواعد'.", "تنبيه OAI", MessageBoxButton.OK, MessageBoxImage.Warning);
                    return;
                }

                var profile = db.UserProfiles.FirstOrDefault();
                if (profile == null || string.IsNullOrWhiteSpace(profile.ResumeFilePath) || !File.Exists(profile.ResumeFilePath))
                {
                    MessageBox.Show("ارفع السيرة الذاتية أولاً من شاشة المصادر والملف.", "السيرة مطلوبة", MessageBoxButton.OK, MessageBoxImage.Warning);
                    return;
                }

                int newJobsAdded = 0;
                var scanner = new JobSourceScanner();

                foreach (var source in enabledSources)
                {
                    var listings = await scanner.ScanAsync(source);
                    foreach (var listing in listings)
                    {
                        if (!listing.IsOpen || listing.RequiresNationality)
                            continue;

                        bool exists = db.JobApplications.Any(j => j.JobUrl == listing.Url);
                        if (exists) continue;

                        var matchResult = await _jobMatchingEngine.EvaluateJobMatchAsync(profile, listing.Title, listing.Company, listing.Description);
                        var advice = matchResult.Rationale;
                        if (string.IsNullOrWhiteSpace(advice))
                            advice = "راجع الوصف قبل التقديم. الزر يفتح صفحة التقديم الحقيقية ويتوقف قبل الإرسال.";

                        var newApp = new JobApplication
                        {
                            JobTitle = listing.Title,
                            CompanyName = listing.Company,
                            Location = listing.Location,
                            Platform = listing.Platform,
                            MatchScore = matchResult.MatchScore,
                            Status = listing.IsOpen ? "شاغرة" : "منتهية",
                            JobUrl = listing.Url,
                            ApplyUrl = string.IsNullOrWhiteSpace(listing.ApplyUrl) ? listing.Url : listing.ApplyUrl,
                            JobDescription = listing.Description,
                            Salary = listing.Salary,
                            IsOpen = listing.IsOpen,
                            HrEmail = listing.HrEmail,
                            Advice = advice,
                            MatchAnalysisJson = matchResult.RawJson,
                            CreatedAt = DateTime.UtcNow
                        };

                        db.JobApplications.Add(newApp);
                        await db.SaveChangesAsync();
                        newJobsAdded++;

                        if (newApp.MatchScore >= 80.0)
                            NotificationService.ShowJobAlertNotification(newApp.JobTitle, newApp.CompanyName, newApp.MatchScore, newApp.ApplyUrl);
                    }
                }

                LoadDataFromDatabase();

                if (newJobsAdded > 0)
                {
                    MessageBox.Show($"أُضيفت {newJobsAdded} وظيفة شاغرة من المصادر المفعّلة، مرتبة حسب التقييم.", "نتائج البحث", MessageBoxButton.OK, MessageBoxImage.Information);
                }
                else
                {
                    MessageBox.Show("لم تُستخرج وظائف شاغرة من صفحات المصادر. استخدم روابط صفحات وظائف HTML مباشرة، وليس صفحات تعتمد على تسجيل الدخول.", "نتائج البحث", MessageBoxButton.OK, MessageBoxImage.Information);
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"خطأ أثناء اكتشاف الوظائف: {ex.Message}", "خطأ في الاكتشاف", MessageBoxButton.OK, MessageBoxImage.Error);
            }
            finally
            {
                IsBusy = false;
            }
        }

        private static string CleanJson(string input)
        {
            if (string.IsNullOrWhiteSpace(input)) return "{}";
            var trimmed = input.Trim();
            if (trimmed.StartsWith("```json", StringComparison.OrdinalIgnoreCase)) trimmed = trimmed.Substring(7);
            else if (trimmed.StartsWith("```")) trimmed = trimmed.Substring(3);
            if (trimmed.EndsWith("```")) trimmed = trimmed.Substring(0, trimmed.Length - 3);
            return trimmed.Trim();
        }

        private async Task ResumeApplicationAsync(JobApplication? application)
        {
            var target = application ?? SelectedApplication;
            if (target == null)
            {
                MessageBox.Show("يرجى اختيار وظيفة من القائمة لبدء التعبئة التلقائية بـ Playwright.", "تنبيه OAI", MessageBoxButton.OK, MessageBoxImage.Information);
                return;
            }

            IsBusy = true;
            try
            {
                using var db = new OAIDbContext();
                var profile = db.UserProfiles.FirstOrDefault() ?? new UserProfile { FullName = "Candidate", Email = "candidate@example.com" };

                // Navigate and Auto-Fill form using Playwright Non-headless browser context with saved PDF CV
                var applyUrl = string.IsNullOrWhiteSpace(target.ApplyUrl) ? target.JobUrl : target.ApplyUrl;
                await _playwrightService.NavigateToUrlAsync(applyUrl);
                var result = await _playwrightService.AutoFillJobApplicationFormAsync(profile, profile.ResumeFilePath);

                // Update application status to PendingSubmit
                target.Status = "PendingSubmit";
                db.JobApplications.Update(target);
                await db.SaveChangesAsync();

                MessageBox.Show($"{result.Message}", "OAI Playwright Auto-Fill", MessageBoxButton.OK, MessageBoxImage.Information);
                LoadDataFromDatabase();
            }
            catch (Exception ex)
            {
                MessageBox.Show($"خطأ أثناء التعبئة التلقائية بـ Playwright:\n{ex.Message}", "خطأ Playwright", MessageBoxButton.OK, MessageBoxImage.Error);
            }
            finally
            {
                IsBusy = false;
            }
        }

        private void UpdateCopilotAnalysis()
        {
            if (SelectedApplication == null)
            {
                CopilotSkillGapAnalysis = "اختر وظيفة من الجدول لعرض تحليل المطابقة.";
                CopilotColdOutreachMessage = string.Empty;
                return;
            }

            CopilotSkillGapAnalysis = $@"التوافق: {SelectedApplication.MatchScore:F0}%
الشركة: {SelectedApplication.CompanyName}
المسمى: {SelectedApplication.JobTitle}
الموقع: {SelectedApplication.Location}
الراتب: {(string.IsNullOrWhiteSpace(SelectedApplication.Salary) ? "غير مذكور" : SelectedApplication.Salary)}
الحالة: {(SelectedApplication.IsOpen ? "شاغرة" : "منتهية")}
البريد: {(string.IsNullOrWhiteSpace(SelectedApplication.HrEmail) ? "غير مذكور" : SelectedApplication.HrEmail)}

{SelectedApplication.Advice}";
        }

        private async Task GenerateOutreachAsync()
        {
            if (SelectedApplication == null)
            {
                MessageBox.Show("يرجى اختيار وظيفة أولاً لإنشاء رسالة تواصل مخصصة.", "تنبيه OAI", MessageBoxButton.OK, MessageBoxImage.Information);
                return;
            }

            IsBusy = true;
            try
            {
                using var db = new OAIDbContext();
                var profile = db.UserProfiles.FirstOrDefault();
                var candidateName = profile?.FullName ?? "المرشح";

                var prompt = $@"Write a highly professional, polite, and persuasive cold outreach message for LinkedIn/Email in Arabic and English to the hiring team at '{SelectedApplication.CompanyName}' regarding the position of '{SelectedApplication.JobTitle}'.
Candidate Name: {candidateName}
Mention that candidate profile matches key technical skills required and look forward to discussing the role.";

                CopilotColdOutreachMessage = await _geminiService.GenerateTextAsync(prompt);
            }
            catch (Exception ex)
            {
                CopilotColdOutreachMessage = $"فشل إنشاء الرسالة: {ex.Message}";
            }
            finally
            {
                IsBusy = false;
            }
        }
    }
}
