using System;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Input;
using Microsoft.EntityFrameworkCore;
using Microsoft.Win32;
using OAI.Data;
using OAI.Models;
using OAI.Services;
using OAI.ViewModels.Base;

namespace OAI.ViewModels
{
    public class SourcesAndRulesViewModel : ViewModelBase
    {
        private readonly GeminiApiService _geminiService;
        private readonly AtsResumeParserService _resumeParserService;

        private UserProfile _currentProfile = new UserProfile();
        public UserProfile CurrentProfile
        {
            get => _currentProfile;
            set
            {
                SetProperty(ref _currentProfile, value);
                OnPropertyChanged(nameof(HasSavedResume));
                OnPropertyChanged(nameof(SavedResumeFileName));
            }
        }

        public bool HasSavedResume => !string.IsNullOrEmpty(CurrentProfile?.ResumeFilePath) && File.Exists(CurrentProfile.ResumeFilePath);
        public string SavedResumeFileName => (HasSavedResume && !string.IsNullOrEmpty(CurrentProfile?.ResumeFilePath)) ? Path.GetFileName(CurrentProfile.ResumeFilePath) : string.Empty;

        private ObservableCollection<SearchSource> _searchSources = new ObservableCollection<SearchSource>();
        public ObservableCollection<SearchSource> SearchSources
        {
            get => _searchSources;
            set
            {
                SetProperty(ref _searchSources, value);
                OnPropertyChanged(nameof(EnabledSourcesCount));
                OnPropertyChanged(nameof(TotalSourcesCount));
            }
        }

        /// <summary>
        /// Count of enabled sources for display in the stats bar
        /// </summary>
        public int EnabledSourcesCount => SearchSources?.Count(s => s.IsEnabled) ?? 0;

        /// <summary>
        /// Total count of all sources
        /// </summary>
        public int TotalSourcesCount => SearchSources?.Count ?? 0;

        // Email Settings Properties
        private bool _isEmailMonitorEnabled;
        public bool IsEmailMonitorEnabled
        {
            get => _isEmailMonitorEnabled;
            set => SetProperty(ref _isEmailMonitorEnabled, value);
        }

        private string _emailAddress = string.Empty;
        public string EmailAddress
        {
            get => _emailAddress;
            set => SetProperty(ref _emailAddress, value);
        }

        private string _imapServer = "imap.gmail.com";
        public string ImapServer
        {
            get => _imapServer;
            set => SetProperty(ref _imapServer, value);
        }

        private int _imapPort = 993;
        public int ImapPort
        {
            get => _imapPort;
            set => SetProperty(ref _imapPort, value);
        }

        private string _appPassword = string.Empty;
        public string AppPassword
        {
            get => _appPassword;
            set => SetProperty(ref _appPassword, value);
        }

        private string _geminiApiKey = string.Empty;
        public string GeminiApiKey
        {
            get => _geminiApiKey;
            set => SetProperty(ref _geminiApiKey, value);
        }

        private string _geminiModelName = "gemini-2.0-flash";
        public string GeminiModelName
        {
            get => _geminiModelName;
            set => SetProperty(ref _geminiModelName, value);
        }

        private string _statusMessage = "جاهز";
        public string StatusMessage
        {
            get => _statusMessage;
            set => SetProperty(ref _statusMessage, value);
        }

        private bool _isBusy;
        public bool IsBusy
        {
            get => _isBusy;
            set => SetProperty(ref _isBusy, value);
        }

        public ICommand UploadResumeCommand { get; }
        public ICommand PreviewCvCommand { get; }
        public ICommand SaveProfileCommand { get; }
        public ICommand SaveSourcesCommand { get; }
        public ICommand SaveEmailSettingsCommand { get; }
        public ICommand SaveGeminiSettingsCommand { get; }

        public SourcesAndRulesViewModel()
        {
            _geminiService = new GeminiApiService();
            _resumeParserService = new AtsResumeParserService(_geminiService);

            UploadResumeCommand = new RelayCommand(async () => await UploadAndParseResumeAsync());
            PreviewCvCommand = new RelayCommand(PreviewCv);
            SaveProfileCommand = new RelayCommand(async () => await SaveProfileAsync());
            SaveSourcesCommand = new RelayCommand(async () => await SaveSourcesAsync());
            SaveEmailSettingsCommand = new RelayCommand(SaveEmailSettings);
            SaveGeminiSettingsCommand = new RelayCommand(SaveGeminiSettings);

            LoadDataFromDatabase();
        }

        public void Reload() => LoadDataFromDatabase();

        private void LoadDataFromDatabase()
        {
            try
            {
                using var db = new OAIDbContext();
                var profile = db.UserProfiles.FirstOrDefault();
                if (profile != null)
                {
                    CurrentProfile = profile;
                }
                else
                {
                    CurrentProfile = new UserProfile();
                }

                var sources = db.SearchSources.ToList();
                SearchSources = new ObservableCollection<SearchSource>(sources);

                var email = db.EmailSettings.FirstOrDefault();
                if (email != null)
                {
                    IsEmailMonitorEnabled = email.IsEnabled;
                    EmailAddress = email.EmailAddress;
                    ImapServer = email.ImapServer;
                    ImapPort = email.ImapPort;
                }

                var app = db.AppSettings.FirstOrDefault();
                if (app != null)
                {
                    GeminiApiKey = app.GeminiApiKey;
                    GeminiModelName = string.IsNullOrWhiteSpace(app.GeminiModelName) ? "gemini-2.0-flash" : app.GeminiModelName;
                    if (!string.IsNullOrWhiteSpace(GeminiApiKey))
                        _geminiService.SetApiKey(GeminiApiKey);
                    _geminiService.SetModelName(GeminiModelName);
                }
            }
            catch (Exception ex)
            {
                var errorMsg = ex.InnerException != null ? $"{ex.Message} -> Inner: {ex.InnerException.Message}" : ex.Message;
                StatusMessage = $"خطأ في تحميل البيانات: {errorMsg}";
            }
        }

        /// <summary>
        /// Add a new SearchSource to the database and refresh the collection
        /// </summary>
        public void AddNewSource(SearchSource newSource)
        {
            try
            {
                using var db = new OAIDbContext();
                db.SearchSources.Add(newSource);
                db.SaveChanges();

                SearchSources.Add(newSource);
                OnPropertyChanged(nameof(EnabledSourcesCount));
                OnPropertyChanged(nameof(TotalSourcesCount));

                StatusMessage = $"تمت إضافة المصدر \"{newSource.PlatformName}\" بنجاح.";
            }
            catch (Exception ex)
            {
                var detailedError = ex.InnerException != null ? $"{ex.Message}\n\n{ex.InnerException.Message}" : ex.Message;
                StatusMessage = $"خطأ الإضافة: {ex.Message}";
                MessageBox.Show($"خطأ في إضافة المصدر إلى قاعدة البيانات:\n\n{detailedError}", "خطأ قاعدة البيانات", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        /// <summary>
        /// Delete a SearchSource by ID from the database and refresh the collection
        /// </summary>
        public void DeleteSource(int sourceId)
        {
            try
            {
                using var db = new OAIDbContext();
                var sourceToDelete = db.SearchSources.FirstOrDefault(s => s.Id == sourceId);
                if (sourceToDelete != null)
                {
                    db.SearchSources.Remove(sourceToDelete);
                    db.SaveChanges();

                    var localItem = SearchSources.FirstOrDefault(s => s.Id == sourceId);
                    if (localItem != null)
                    {
                        SearchSources.Remove(localItem);
                    }

                    OnPropertyChanged(nameof(EnabledSourcesCount));
                    OnPropertyChanged(nameof(TotalSourcesCount));

                    StatusMessage = $"تم حذف المصدر \"{sourceToDelete.PlatformName}\" بنجاح.";
                }
            }
            catch (Exception ex)
            {
                var detailedError = ex.InnerException != null ? $"{ex.Message}\n\n{ex.InnerException.Message}" : ex.Message;
                StatusMessage = $"خطأ الحذف: {ex.Message}";
                MessageBox.Show($"خطأ في حذف المصدر من قاعدة البيانات:\n\n{detailedError}", "خطأ قاعدة البيانات", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private async Task UploadAndParseResumeAsync()
        {
            var openFileDialog = new OpenFileDialog
            {
                Filter = "السيرة الذاتية|*.pdf;*.docx;*.txt|PDF|*.pdf|Word|*.docx|نص|*.txt",
                Title = "اختر ملف السيرة الذاتية"
            };

            if (openFileDialog.ShowDialog() == true)
            {
                IsBusy = true;
                StatusMessage = "جاري قراءة الملف وملء الحقول...";
                try
                {
                    var sourceFilePath = openFileDialog.FileName;
                    if (!DocumentTextExtractor.IsSupported(sourceFilePath))
                    {
                        MessageBox.Show("يدعم البرنامج PDF و Word (.docx) فقط.", "صيغة غير مدعومة", MessageBoxButton.OK, MessageBoxImage.Warning);
                        return;
                    }

                    var localFolder = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "UserFiles", "Resumes");
                    Directory.CreateDirectory(localFolder);
                    var localFileName = $"CV_{DateTime.Now:yyyyMMdd_HHmmss}_{Path.GetFileName(sourceFilePath)}";
                    var localDestinationPath = Path.Combine(localFolder, localFileName);
                    File.Copy(sourceFilePath, localDestinationPath, overwrite: true);

                    var fileText = DocumentTextExtractor.Extract(localDestinationPath);
                    if (string.IsNullOrWhiteSpace(fileText))
                    {
                        MessageBox.Show("تعذر قراءة الملف. استخدم PDF نصي أو Word docx.", "قراءة الملف", MessageBoxButton.OK, MessageBoxImage.Warning);
                        return;
                    }

                    var parsedProfile = await _resumeParserService.ParseAndCreateUserProfileAsync(fileText);
                    parsedProfile.ResumeFilePath = localDestinationPath;
                    
                    using var db = new OAIDbContext();
                    var existing = db.UserProfiles.FirstOrDefault();
                    if (existing != null)
                    {
                        existing.FullName = parsedProfile.FullName;
                        existing.Email = parsedProfile.Email;
                        existing.Phone = parsedProfile.Phone;
                        existing.Location = parsedProfile.Location;
                        existing.Skills = parsedProfile.Skills;
                        existing.TargetJobTitles = parsedProfile.TargetJobTitles;
                        existing.Education = parsedProfile.Education;
                        existing.Keywords = parsedProfile.Keywords;
                        existing.ExperienceSummary = parsedProfile.ExperienceSummary;
                        existing.RawResumeContent = parsedProfile.RawResumeContent;
                        existing.ParsedProfileJson = parsedProfile.ParsedProfileJson;
                        existing.ResumeFilePath = localDestinationPath;
                        existing.UpdatedAt = DateTime.UtcNow;
                        CurrentProfile = existing;
                    }
                    else
                    {
                        db.UserProfiles.Add(parsedProfile);
                        CurrentProfile = parsedProfile;
                    }
                    await db.SaveChangesAsync();

                    OnPropertyChanged(nameof(HasSavedResume));
                    OnPropertyChanged(nameof(SavedResumeFileName));

                    StatusMessage = "تم حفظ وتحليل السيرة الذاتية بنجاح عبر Gemini ATS!";
                    MessageBox.Show("تم حفظ ملف السيرة الذاتية PDF وتحليله بنجاح!", "نجاح العملية", MessageBoxButton.OK, MessageBoxImage.Information);
                }
                catch (Exception ex)
                {
                    var detailedError = ex.InnerException != null ? $"{ex.Message}\n\n التفاصيل الداخلية (Inner Exception):\n{ex.InnerException.Message}" : ex.Message;
                    StatusMessage = $"خطأ في التحليل: {ex.Message}";
                    MessageBox.Show($"فشل حفظ وتطوير السيرة الذاتية:\n\n{detailedError}", "خطأ في حفظ السيرة الذاتية", MessageBoxButton.OK, MessageBoxImage.Error);
                }
                finally
                {
                    IsBusy = false;
                }
            }
        }

        private void PreviewCv()
        {
            try
            {
                if (HasSavedResume && !string.IsNullOrEmpty(CurrentProfile.ResumeFilePath))
                {
                    Process.Start(new ProcessStartInfo
                    {
                        FileName = CurrentProfile.ResumeFilePath,
                        UseShellExecute = true
                    });
                }
                else
                {
                    MessageBox.Show("عذراً، ملف السيرة الذاتية غير موجود.", "تنبيه", MessageBoxButton.OK, MessageBoxImage.Warning);
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"حدث خطأ أثناء فتح الملف: {ex.Message}", "خطأ", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private async Task SaveProfileAsync()
        {
            try
            {
                using var db = new OAIDbContext();
                if (CurrentProfile.Id > 0)
                {
                    db.UserProfiles.Update(CurrentProfile);
                }
                else
                {
                    db.UserProfiles.Add(CurrentProfile);
                }
                await db.SaveChangesAsync();

                OnPropertyChanged(nameof(HasSavedResume));
                OnPropertyChanged(nameof(SavedResumeFileName));

                StatusMessage = "تم حفظ بيانات الملف الشخصي بنجاح.";
                MessageBox.Show("تم حفظ بيانات الملف الشخصي بنجاح في قاعدة البيانات!", "نجاح الحفظ", MessageBoxButton.OK, MessageBoxImage.Information);
            }
            catch (Exception ex)
            {
                var detailedError = ex.InnerException != null ? $"{ex.Message}\n\n التفاصيل الداخلية:\n{ex.InnerException.Message}" : ex.Message;
                StatusMessage = $"خطأ الحفظ: {ex.Message}";
                MessageBox.Show($"خطأ عند حفظ الملف الشخصي:\n\n{detailedError}", "خطأ حفظ البيانات", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private async Task SaveSourcesAsync()
        {
            try
            {
                using var db = new OAIDbContext();
                foreach (var source in SearchSources)
                {
                    if (source.Id > 0)
                    {
                        db.SearchSources.Update(source);
                    }
                    else
                    {
                        db.SearchSources.Add(source);
                    }
                }
                await db.SaveChangesAsync();

                OnPropertyChanged(nameof(EnabledSourcesCount));
                OnPropertyChanged(nameof(TotalSourcesCount));

                StatusMessage = "تم حفظ المصادر بنجاح.";
                MessageBox.Show("تم تحديث وحفظ مصادر التوظيف بنجاح!", "إعدادات المصادر", MessageBoxButton.OK, MessageBoxImage.Information);
            }
            catch (Exception ex)
            {
                var detailedError = ex.InnerException != null ? $"{ex.Message}\n\n التفاصيل الداخلية:\n{ex.InnerException.Message}" : ex.Message;
                StatusMessage = $"خطأ المصادر: {ex.Message}";
                MessageBox.Show($"خطأ عند حفظ المصادر:\n\n{detailedError}", "خطأ حفظ المصادر", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void SaveEmailSettings()
        {
            try
            {
                using var db = new OAIDbContext();
                var settings = db.EmailSettings.FirstOrDefault();
                if (settings == null)
                {
                    settings = new EmailSettings();
                    db.EmailSettings.Add(settings);
                }

                settings.IsEnabled = IsEmailMonitorEnabled;
                settings.EmailAddress = EmailAddress?.Trim() ?? string.Empty;
                settings.ImapServer = string.IsNullOrWhiteSpace(ImapServer) ? "imap.gmail.com" : ImapServer.Trim();
                settings.ImapPort = ImapPort <= 0 ? 993 : ImapPort;

                if (!string.IsNullOrWhiteSpace(AppPassword))
                    settings.EncryptedAppPassword = EmailSecurityService.ProtectPassword(AppPassword);

                db.SaveChanges();
                AppPassword = string.Empty;
                StatusMessage = "تم حفظ إعدادات البريد.";
                MessageBox.Show("تم حفظ إعدادات البريد وتشفير كلمة المرور.", "البريد", MessageBoxButton.OK, MessageBoxImage.Information);
            }
            catch (Exception ex)
            {
                var detailedError = ex.InnerException != null ? $"{ex.Message}\n\n{ex.InnerException.Message}" : ex.Message;
                StatusMessage = $"خطأ البريد: {ex.Message}";
                MessageBox.Show($"خطأ عند حفظ إعدادات البريد:\n\n{detailedError}", "خطأ البريد", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void SaveGeminiSettings()
        {
            try
            {
                using var db = new OAIDbContext();
                var settings = db.AppSettings.FirstOrDefault();
                if (settings == null)
                {
                    settings = new AppSetting();
                    db.AppSettings.Add(settings);
                }

                settings.GeminiApiKey = GeminiApiKey?.Trim() ?? string.Empty;
                settings.GeminiModelName = string.IsNullOrWhiteSpace(GeminiModelName) ? "gemini-2.0-flash" : GeminiModelName.Trim();
                db.SaveChanges();

                _geminiService.SetApiKey(settings.GeminiApiKey);
                _geminiService.SetModelName(settings.GeminiModelName);
                StatusMessage = "تم حفظ إعدادات Gemini.";
                MessageBox.Show("تم حفظ مفتاح Gemini والنموذج.", "Gemini", MessageBoxButton.OK, MessageBoxImage.Information);
            }
            catch (Exception ex)
            {
                StatusMessage = $"خطأ Gemini: {ex.Message}";
                MessageBox.Show($"خطأ عند حفظ إعدادات Gemini:\n{ex.Message}", "خطأ", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }
    }
}
