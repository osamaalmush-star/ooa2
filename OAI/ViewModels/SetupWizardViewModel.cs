using System;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Input;
using System.Windows.Media;
using Microsoft.Win32;
using OAI.Data;
using OAI.Models;
using OAI.Services;
using OAI.ViewModels.Base;

namespace OAI.ViewModels
{
    public class SetupWizardViewModel : ViewModelBase
    {
        private readonly GeminiApiService _gemini = new();
        public event Action? SetupCompleted;

        private int _step = 1;
        public int Step
        {
            get => _step;
            set
            {
                if (SetProperty(ref _step, value))
                {
                    OnPropertyChanged(nameof(IsWelcome));
                    OnPropertyChanged(nameof(IsResume));
                    OnPropertyChanged(nameof(IsEmail));
                    OnPropertyChanged(nameof(IsSources));
                    OnPropertyChanged(nameof(IsSchedule));
                    OnPropertyChanged(nameof(IsReady));
                    OnPropertyChanged(nameof(CanGoBack));
                    OnPropertyChanged(nameof(PrimaryLabel));
                    OnPropertyChanged(nameof(StepTitle));
                    OnPropertyChanged(nameof(StepHint));
                    OnPropertyChanged(nameof(Step1Bg));
                    OnPropertyChanged(nameof(Step2Bg));
                    OnPropertyChanged(nameof(Step3Bg));
                    OnPropertyChanged(nameof(Step4Bg));
                    OnPropertyChanged(nameof(Step5Bg));
                    OnPropertyChanged(nameof(Step6Bg));
                    (NextCommand as RelayCommand)?.RaiseCanExecuteChanged();
                }
            }
        }

        public bool IsWelcome => Step == 1;
        public bool IsResume => Step == 2;
        public bool IsEmail => Step == 3;
        public bool IsSources => Step == 4;
        public bool IsSchedule => Step == 5;
        public bool IsReady => Step == 6;
        public bool CanGoBack => Step > 1;

        public string StepTitle => Step switch
        {
            1 => "مرحباً",
            2 => "السيرة الذاتية",
            3 => "حساب البريد",
            4 => "مصادر الوظائف",
            5 => "جدول المراقبة",
            _ => "جاهز"
        };

        public string StepHint => Step switch
        {
            1 => "أدخل مفتاح Gemini الخاص بك. البرنامج لا يستخدم مفتاحاً جاهزاً.",
             2 => "أنشئ سيرة ATS كاملة. زر التالي يبقى معطّلاً حتى تصبح الجاهزية 100%.",
            3 => "أدخل بريد IMAP وكلمة مرور التطبيق ثم احفظ.",
            4 => "أضف رابط منصة واحدة على الأقل وفعّلها.",
            5 => "حدد فترة فحص المصادر بالدقائق.",
            _ => "اكتملت الإعدادات الأولية. يمكنك بدء البحث."
        };

        public string PrimaryLabel => Step == 6 ? "بدء الاستخدام" : "التالي";

        public Brush Step1Bg => Tone(1);
        public Brush Step2Bg => Tone(2);
        public Brush Step3Bg => Tone(3);
        public Brush Step4Bg => Tone(4);
        public Brush Step5Bg => Tone(5);
        public Brush Step6Bg => Tone(6);

        private static readonly Brush ActiveStep = BrushFrom("#2563EB");
        private static readonly Brush DoneStep = BrushFrom("#166534");
        private static readonly Brush IdleStep = BrushFrom("#1C2740");

        private Brush Tone(int n) => Step == n ? ActiveStep : (Step > n ? DoneStep : IdleStep);

        private static Brush BrushFrom(string hex)
        {
            var brush = new SolidColorBrush((Color)ColorConverter.ConvertFromString(hex));
            brush.Freeze();
            return brush;
        }

        private string _error = string.Empty;
        public string Error
        {
            get => _error;
            set => SetProperty(ref _error, value);
        }

        private bool _isBusy;
        public bool IsBusy
        {
            get => _isBusy;
            set
            {
                if (SetProperty(ref _isBusy, value))
                    (NextCommand as RelayCommand)?.RaiseCanExecuteChanged();
            }
        }

        public string GeminiApiKey { get; set; } = string.Empty;
        public string GeminiModelName { get; set; } = "gemini-2.0-flash";

        public string FullName { get; set; } = string.Empty;
        public string Email { get; set; } = string.Empty;
        public string Phone { get; set; } = string.Empty;
        public string Age { get; set; } = string.Empty;
        public string Gender { get; set; } = string.Empty;
        public string Location { get; set; } = string.Empty;
        public string TargetTitle { get; set; } = string.Empty;
        public string Experience { get; set; } = string.Empty;
        public string Education { get; set; } = string.Empty;
        public string Skills { get; set; } = string.Empty;
        public string Keywords { get; set; } = string.Empty;
        public string ResumePath { get; set; } = string.Empty;
        public string ResumeFileName => string.IsNullOrEmpty(ResumePath) ? "لم يُرفع ملف" : Path.GetFileName(ResumePath);

        public bool IsEmailEnabled { get; set; } = true;
        public string EmailAddress { get; set; } = string.Empty;
        public string ImapServer { get; set; } = "imap.gmail.com";
        public int ImapPort { get; set; } = 993;
        public string AppPassword { get; set; } = string.Empty;

        public string NewPlatformName { get; set; } = string.Empty;
        public string NewTargetUrl { get; set; } = string.Empty;
        public ObservableCollection<SearchSource> Sources { get; } = new();

        public int MonitoringMinutes { get; set; } = 60;

        public CreateCvWizardViewModel CvWizard { get; } = new();

        public ICommand NextCommand { get; }
        public ICommand BackCommand { get; }
        public ICommand UploadResumeCommand { get; }
        public ICommand AddSourceCommand { get; }
        public ICommand DeleteSourceCommand { get; }

        public SetupWizardViewModel()
        {
            NextCommand = new RelayCommand(() => Next(), CanGoNext);
            BackCommand = new RelayCommand(() => { if (CanGoBack) Step--; Error = string.Empty; });
            UploadResumeCommand = new RelayCommand(async () => await UploadResumeAsync());
            AddSourceCommand = new RelayCommand(AddSource);
            DeleteSourceCommand = new RelayCommand(p => DeleteSource(p as SearchSource));
            CvWizard.AtsStateChanged += () => (NextCommand as RelayCommand)?.RaiseCanExecuteChanged();
            LoadExisting();
        }

        private bool CanGoNext()
        {
            if (IsBusy) return false;
            if (Step == 2) return CvWizard.CanProceed && !CvWizard.IsBusy;
            return true;
        }

        private void LoadExisting()
        {
            using var db = new OAIDbContext();
            var app = db.AppSettings.FirstOrDefault();
            if (app != null)
            {
                GeminiApiKey = app.GeminiApiKey;
                GeminiModelName = string.IsNullOrWhiteSpace(app.GeminiModelName) ? "gemini-2.0-flash" : app.GeminiModelName;
                MonitoringMinutes = app.MonitoringIntervalMinutes <= 0 ? 60 : app.MonitoringIntervalMinutes;
            }

            var profile = db.UserProfiles.FirstOrDefault();
            if (profile != null)
            {
                FullName = profile.FullName;
                Email = profile.Email;
                Phone = profile.PhoneNumber;
                Age = profile.Age;
                Gender = profile.Gender;
                Location = profile.Location;
                TargetTitle = profile.TargetJobTitle;
                Experience = profile.YearsOfExperience;
                Education = profile.Education;
                Skills = profile.SkillsList;
                Keywords = profile.Keywords;
                ResumePath = profile.ResumeFilePath ?? string.Empty;
            }

            var email = db.EmailSettings.FirstOrDefault();
            if (email != null)
            {
                IsEmailEnabled = email.IsEnabled;
                EmailAddress = email.EmailAddress;
                ImapServer = email.ImapServer;
                ImapPort = email.ImapPort;
            }

            foreach (var s in db.SearchSources.ToList())
                Sources.Add(s);

            OnPropertyChanged(nameof(ResumeFileName));
            CvWizard.SetGeminiKey(GeminiApiKey, GeminiModelName);
        }

        private void Next()
        {
            Error = string.Empty;
            if (!ValidateCurrent()) return;

            if (Step == 1) SaveGemini();
            if (Step == 2) SaveProfile();
            if (Step == 3) SaveEmail();
            if (Step == 4) SaveSourceToggles();
            if (Step == 5) SaveSchedule();

            if (Step < 6)
            {
                Step++;
                return;
            }

            MarkComplete();
            SetupCompleted?.Invoke();
        }

        private bool ValidateCurrent()
        {
            switch (Step)
            {
                case 1:
                    if (string.IsNullOrWhiteSpace(GeminiApiKey))
                    {
                        Error = "أدخل مفتاح Gemini الخاص بك قبل المتابعة.";
                        return false;
                    }
                    return true;
                case 2:
                    CvWizard.RefreshAts();
                    if (!CvWizard.CanProceed)
                    {
                        Error = CvWizard.MissingRequirements.FirstOrDefault() ?? "أكمل متطلبات ATS بنسبة 100% قبل المتابعة.";
                        return false;
                    }
                    return true;
                case 3:
                    if (IsEmailEnabled && (string.IsNullOrWhiteSpace(EmailAddress) || string.IsNullOrWhiteSpace(AppPassword) && string.IsNullOrWhiteSpace(LoadEncryptedPassword())))
                    {
                        Error = "أدخل البريد وكلمة مرور التطبيق، أو أوقف المراقبة.";
                        return false;
                    }
                    return true;
                case 4:
                    if (!Sources.Any(s => s.IsEnabled && !string.IsNullOrWhiteSpace(s.TargetUrl)))
                    {
                        Error = "أضف مصدراً مفعّلاً واحداً على الأقل.";
                        return false;
                    }
                    return true;
                case 5:
                    if (MonitoringMinutes < 15)
                    {
                        Error = "أقل فترة مراقبة 15 دقيقة.";
                        return false;
                    }
                    return true;
                default:
                    return true;
            }
        }

        private string? LoadEncryptedPassword()
        {
            using var db = new OAIDbContext();
            return db.EmailSettings.FirstOrDefault()?.EncryptedAppPassword;
        }

        private async Task UploadResumeAsync()
        {
            var dialog = new OpenFileDialog
            {
                Filter = "السيرة الذاتية|*.pdf;*.docx;*.txt|PDF|*.pdf|Word|*.docx|نص|*.txt",
                Title = "اختر ملف السيرة الذاتية"
            };
            if (dialog.ShowDialog() != true) return;

            if (!DocumentTextExtractor.IsSupported(dialog.FileName))
            {
                Error = "يدعم البرنامج PDF و Word (.docx) فقط.";
                return;
            }

            IsBusy = true;
            Error = string.Empty;
            try
            {
                var folder = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "UserFiles", "Resumes");
                Directory.CreateDirectory(folder);
                var dest = Path.Combine(folder, $"CV_{DateTime.Now:yyyyMMdd_HHmmss}_{Path.GetFileName(dialog.FileName)}");
                File.Copy(dialog.FileName, dest, true);
                ResumePath = dest;
                OnPropertyChanged(nameof(ResumeFileName));

                var text = DocumentTextExtractor.Extract(dest);
                if (string.IsNullOrWhiteSpace(text))
                {
                    Error = "تعذر قراءة الملف. استخدم PDF نصي أو Word docx.";
                    return;
                }

                if (!string.IsNullOrWhiteSpace(GeminiApiKey))
                    _gemini.SetApiKey(GeminiApiKey.Trim());

                var parsed = await _gemini.ParseResumeAsync(text);
                FullName = parsed.FullName;
                Email = parsed.Email;
                Phone = parsed.Phone;
                Age = parsed.Age;
                Gender = parsed.Gender;
                Location = parsed.Location;
                TargetTitle = parsed.TargetTitle;
                Experience = parsed.Experience;
                Education = parsed.Education;
                Skills = parsed.Skills;
                Keywords = parsed.Keywords;
                NotifyProfile();
            }
            catch (Exception ex)
            {
                Error = ex.Message;
            }
            finally
            {
                IsBusy = false;
            }
        }

        private void NotifyProfile()
        {
            OnPropertyChanged(nameof(FullName));
            OnPropertyChanged(nameof(Email));
            OnPropertyChanged(nameof(Phone));
            OnPropertyChanged(nameof(Age));
            OnPropertyChanged(nameof(Gender));
            OnPropertyChanged(nameof(Location));
            OnPropertyChanged(nameof(TargetTitle));
            OnPropertyChanged(nameof(Experience));
            OnPropertyChanged(nameof(Education));
            OnPropertyChanged(nameof(Skills));
            OnPropertyChanged(nameof(Keywords));
        }

        private void AddSource()
        {
            var name = NewPlatformName?.Trim() ?? string.Empty;
            var url = NewTargetUrl?.Trim() ?? string.Empty;
            if (string.IsNullOrWhiteSpace(name) || string.IsNullOrWhiteSpace(url))
            {
                Error = "أدخل اسم المنصة والرابط.";
                return;
            }
            if (!url.StartsWith("http", StringComparison.OrdinalIgnoreCase))
                url = "https://" + url;
            if (!Uri.TryCreate(url, UriKind.Absolute, out _))
            {
                Error = "الرابط غير صالح.";
                return;
            }

            using var db = new OAIDbContext();
            var source = new SearchSource { PlatformName = name, TargetUrl = url, IsEnabled = true, CreatedAt = DateTime.UtcNow };
            db.SearchSources.Add(source);
            db.SaveChanges();
            Sources.Add(source);
            NewPlatformName = string.Empty;
            NewTargetUrl = string.Empty;
            OnPropertyChanged(nameof(NewPlatformName));
            OnPropertyChanged(nameof(NewTargetUrl));
            Error = string.Empty;
        }

        private void DeleteSource(SearchSource? source)
        {
            if (source == null) return;
            using var db = new OAIDbContext();
            var item = db.SearchSources.FirstOrDefault(s => s.Id == source.Id);
            if (item != null)
            {
                db.SearchSources.Remove(item);
                db.SaveChanges();
            }
            Sources.Remove(source);
        }

        private void SaveGemini()
        {
            using var db = new OAIDbContext();
            var settings = db.AppSettings.FirstOrDefault() ?? db.AppSettings.Add(new AppSetting()).Entity;
            settings.GeminiApiKey = GeminiApiKey.Trim();
            settings.GeminiModelName = string.IsNullOrWhiteSpace(GeminiModelName) ? "gemini-2.0-flash" : GeminiModelName.Trim();
            db.SaveChanges();
            _gemini.SetApiKey(settings.GeminiApiKey);
            _gemini.SetModelName(settings.GeminiModelName);
            CvWizard.SetGeminiKey(settings.GeminiApiKey, settings.GeminiModelName);
        }

        private void SaveProfile()
        {
            if (!CvWizard.TryCommit())
            {
                Error = "تعذر حفظ السيرة. أكمل متطلبات ATS أولاً.";
                return;
            }

            FullName = CvWizard.FullName;
            Email = CvWizard.Email;
            Phone = CvWizard.Phone;
            Location = CvWizard.Location;
            TargetTitle = CvWizard.TargetJobTitle;
            ResumePath = CvWizard.ResumePath;
            OnPropertyChanged(nameof(ResumeFileName));
        }

        private void SaveEmail()
        {
            using var db = new OAIDbContext();
            var settings = db.EmailSettings.FirstOrDefault() ?? db.EmailSettings.Add(new EmailSettings()).Entity;
            settings.IsEnabled = IsEmailEnabled;
            settings.EmailAddress = EmailAddress.Trim();
            settings.ImapServer = string.IsNullOrWhiteSpace(ImapServer) ? "imap.gmail.com" : ImapServer.Trim();
            settings.ImapPort = ImapPort <= 0 ? 993 : ImapPort;
            if (!string.IsNullOrWhiteSpace(AppPassword))
                settings.EncryptedAppPassword = EmailSecurityService.ProtectPassword(AppPassword);
            db.SaveChanges();
        }

        private void SaveSourceToggles()
        {
            using var db = new OAIDbContext();
            foreach (var source in Sources)
            {
                var item = db.SearchSources.FirstOrDefault(s => s.Id == source.Id);
                if (item != null) item.IsEnabled = source.IsEnabled;
            }
            db.SaveChanges();
        }

        private void SaveSchedule()
        {
            using var db = new OAIDbContext();
            var settings = db.AppSettings.FirstOrDefault() ?? db.AppSettings.Add(new AppSetting()).Entity;
            settings.MonitoringIntervalMinutes = MonitoringMinutes;
            db.SaveChanges();
        }

        private void MarkComplete()
        {
            using var db = new OAIDbContext();
            var settings = db.AppSettings.FirstOrDefault() ?? db.AppSettings.Add(new AppSetting()).Entity;
            settings.IsSetupComplete = true;
            db.SaveChanges();
        }
    }
}
