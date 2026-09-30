using System;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using Microsoft.Win32;
using OAI.Data;
using OAI.Models;
using OAI.Services;
using OAI.ViewModels;

namespace OAI.Views
{
    public partial class SourcesAndRulesView : UserControl
    {
        private readonly GeminiApiService _geminiService;
        private string? _currentResumeFilePath;

        public SourcesAndRulesView()
        {
            InitializeComponent();
            _geminiService = new GeminiApiService();
            this.Loaded += SourcesAndRulesView_Loaded;
        }

        private void SourcesAndRulesView_Loaded(object sender, RoutedEventArgs e)
        {
            LoadProfileDataIntoForm();
        }

        private void LoadProfileDataIntoForm()
        {
            try
            {
                using var db = new OAIDbContext();
                var profile = db.UserProfiles.FirstOrDefault();
                if (profile != null)
                {
                    txtFullName.Text = profile.FullName;
                    txtEmail.Text = profile.Email;
                    txtPhone.Text = !string.IsNullOrEmpty(profile.PhoneNumber) ? profile.PhoneNumber : profile.Phone;
                    txtAge.Text = profile.Age;
                    txtGender.Text = profile.Gender;
                    txtTargetTitle.Text = !string.IsNullOrEmpty(profile.TargetJobTitle) ? profile.TargetJobTitle : profile.TargetJobTitles;
                    txtExperience.Text = profile.YearsOfExperience;
                    txtEducation.Text = profile.Education;
                    txtLocation.Text = profile.Location;
                    txtSkills.Text = !string.IsNullOrEmpty(profile.SkillsList) ? profile.SkillsList : profile.Skills;

                    _currentResumeFilePath = profile.ResumeFilePath;
                    UpdateResumeStatusBadge(profile.ResumeFilePath);
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Error loading profile: {ex.Message}");
            }
        }

        private void UpdateResumeStatusBadge(string? resumePath)
        {
            if (!string.IsNullOrEmpty(resumePath) && File.Exists(resumePath))
            {
                txtSavedResumeName.Text = Path.GetFileName(resumePath);
                pnlResumeBadge.Visibility = Visibility.Visible;
                btnPreviewCv.IsEnabled = true;
            }
            else
            {
                txtSavedResumeName.Text = "لم يتم إدراج سيرة ذاتية بعد";
                pnlResumeBadge.Visibility = Visibility.Visible;
                btnPreviewCv.IsEnabled = false;
            }
        }

        private async void btnUploadPdf_Click(object sender, RoutedEventArgs e)
        {
            var openFileDialog = new OpenFileDialog
            {
                Filter = "السيرة الذاتية|*.pdf;*.docx;*.txt|PDF|*.pdf|Word|*.docx|نص|*.txt",
                Title = "اختر ملف السيرة الذاتية"
            };

            if (openFileDialog.ShowDialog() == true)
            {
                var sourceFilePath = openFileDialog.FileName;

                try
                {
                    Mouse.OverrideCursor = Cursors.Wait;

                    // 1. Save PDF locally into UserFiles/Resumes/ and update ResumeFilePath
                    var localFolder = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "UserFiles", "Resumes");
                    Directory.CreateDirectory(localFolder);

                    var localFileName = $"CV_{DateTime.Now:yyyyMMdd_HHmmss}_{Path.GetFileName(sourceFilePath)}";
                    var localDestinationPath = Path.Combine(localFolder, localFileName);
                    File.Copy(sourceFilePath, localDestinationPath, overwrite: true);
                    _currentResumeFilePath = localDestinationPath;

                    // Extract text using PdfPig or File.ReadAllText
                    if (!DocumentTextExtractor.IsSupported(sourceFilePath))
                    {
                        MessageBox.Show("يدعم البرنامج PDF و Word (.docx) فقط.", "صيغة غير مدعومة", MessageBoxButton.OK, MessageBoxImage.Warning);
                        return;
                    }

                    var cvText = DocumentTextExtractor.Extract(localDestinationPath);
                    if (string.IsNullOrWhiteSpace(cvText))
                    {
                        MessageBox.Show("تعذر قراءة الملف. استخدم PDF نصي أو Word docx.", "قراءة الملف", MessageBoxButton.OK, MessageBoxImage.Warning);
                        return;
                    }

                    // 2. Send PDF content to GeminiApiService.ParseResumeAsync()
                    // 3. Parse JSON response from Gemini
                    var parsedData = await _geminiService.ParseResumeAsync(cvText);

                    // 4. AUTO-FILL all TextBoxes immediately in the UI
                    txtFullName.Text = parsedData.FullName;
                    txtEmail.Text = parsedData.Email;
                    txtPhone.Text = parsedData.Phone;
                    txtAge.Text = parsedData.Age;
                    txtGender.Text = parsedData.Gender;
                    txtTargetTitle.Text = parsedData.TargetTitle;
                    txtExperience.Text = parsedData.Experience;
                    txtEducation.Text = parsedData.Education;
                    txtLocation.Text = parsedData.Location;
                    txtSkills.Text = parsedData.Skills;

                    // Update UI status badge
                    UpdateResumeStatusBadge(localDestinationPath);

                    // 5. Save the populated UserProfile entity to OAIDbContext
                    using var db = new OAIDbContext();
                    var existingProfile = db.UserProfiles.FirstOrDefault();
                    if (existingProfile == null)
                    {
                        existingProfile = new UserProfile();
                        db.UserProfiles.Add(existingProfile);
                    }

                    existingProfile.FullName = parsedData.FullName;
                    existingProfile.Email = parsedData.Email;
                    existingProfile.PhoneNumber = parsedData.Phone;
                    existingProfile.Age = parsedData.Age;
                    existingProfile.Gender = parsedData.Gender;
                    existingProfile.TargetJobTitle = parsedData.TargetTitle;
                    existingProfile.YearsOfExperience = parsedData.Experience;
                    existingProfile.SkillsList = parsedData.Skills;
                    existingProfile.Education = parsedData.Education;
                    existingProfile.Location = parsedData.Location;
                    existingProfile.Keywords = parsedData.Keywords;
                    existingProfile.ResumeFilePath = localDestinationPath;
                    existingProfile.RawResumeContent = cvText;
                    existingProfile.UpdatedAt = DateTime.UtcNow;

                    await db.SaveChangesAsync();

                    if (DataContext is SourcesAndRulesViewModel vm)
                    {
                        vm.CurrentProfile = existingProfile;
                        vm.StatusMessage = "تم استخراج البيانات وملء الملف الشخصي بنجاح عبر محرك الذكاء الاصطناعي!";
                    }

                    MessageBox.Show("تم تحليل السيرة الذاتية وملء بيانات الملف الشخصي تلقائياً بنجاح!", "اكتمال التحليل التلقائي", MessageBoxButton.OK, MessageBoxImage.Information);
                }
                catch (Exception ex)
                {
                    var detailed = ex.InnerException != null ? $"{ex.Message}\n\n{ex.InnerException.Message}" : ex.Message;
                    MessageBox.Show($"خطأ أثناء معالجة السيرة الذاتية:\n\n{detailed}", "خطأ", MessageBoxButton.OK, MessageBoxImage.Error);
                }
                finally
                {
                    Mouse.OverrideCursor = null;
                }
            }
        }

        private void btnSaveProfile_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                using var db = new OAIDbContext();
                var profile = db.UserProfiles.FirstOrDefault();
                if (profile == null)
                {
                    profile = new UserProfile();
                    db.UserProfiles.Add(profile);
                }

                profile.FullName = txtFullName.Text?.Trim() ?? string.Empty;
                profile.Email = txtEmail.Text?.Trim() ?? string.Empty;
                profile.PhoneNumber = txtPhone.Text?.Trim() ?? string.Empty;
                profile.Age = txtAge.Text?.Trim() ?? string.Empty;
                profile.Gender = txtGender.Text?.Trim() ?? string.Empty;
                profile.TargetJobTitle = txtTargetTitle.Text?.Trim() ?? string.Empty;
                profile.YearsOfExperience = txtExperience.Text?.Trim() ?? string.Empty;
                profile.SkillsList = txtSkills.Text?.Trim() ?? string.Empty;
                profile.Education = txtEducation.Text?.Trim() ?? string.Empty;
                profile.Location = txtLocation.Text?.Trim() ?? string.Empty;
                if (!string.IsNullOrEmpty(_currentResumeFilePath))
                {
                    profile.ResumeFilePath = _currentResumeFilePath;
                }
                profile.UpdatedAt = DateTime.UtcNow;

                db.SaveChanges();

                if (DataContext is SourcesAndRulesViewModel vm)
                {
                    vm.CurrentProfile = profile;
                    vm.StatusMessage = "تم حفظ بيانات الملف الشخصي بنجاح.";
                }

                MessageBox.Show("تم حفظ بيانات الملف الشخصي بنجاح!", "تم الحفظ", MessageBoxButton.OK, MessageBoxImage.Information);
            }
            catch (Exception ex)
            {
                MessageBox.Show($"خطأ في حفظ البيانات: {ex.Message}", "خطأ", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void btnPreviewCv_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                string? targetPath = _currentResumeFilePath;
                if (string.IsNullOrEmpty(targetPath) || !File.Exists(targetPath))
                {
                    using var db = new OAIDbContext();
                    targetPath = db.UserProfiles.FirstOrDefault()?.ResumeFilePath;
                }

                if (!string.IsNullOrEmpty(targetPath) && File.Exists(targetPath))
                {
                    Process.Start(new ProcessStartInfo
                    {
                        FileName = targetPath,
                        UseShellExecute = true
                    });
                }
                else
                {
                    MessageBox.Show("عذراً، ملف السيرة الذاتية غير موجود. يرجى إدراج ملف PDF أولاً.", "تنبيه", MessageBoxButton.OK, MessageBoxImage.Warning);
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"حدث خطأ أثناء فتح الملف: {ex.Message}", "خطأ", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void btnAddSource_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                string platformName = txtNewPlatformName.Text?.Trim() ?? string.Empty;
                string targetUrl = txtNewTargetUrl.Text?.Trim() ?? string.Empty;
                string accountUsername = txtNewAccountUsername.Text?.Trim() ?? string.Empty;

                if (string.IsNullOrWhiteSpace(platformName))
                {
                    MessageBox.Show("يرجى إدخال اسم المنصة أو الشركة.", "حقل مطلوب", MessageBoxButton.OK, MessageBoxImage.Warning);
                    txtNewPlatformName.Focus();
                    return;
                }

                if (string.IsNullOrWhiteSpace(targetUrl))
                {
                    MessageBox.Show("يرجى إدخال رابط الموقع أو صفحة الوظائف (URL).", "حقل مطلوب", MessageBoxButton.OK, MessageBoxImage.Warning);
                    txtNewTargetUrl.Focus();
                    return;
                }

                // Auto prepend https:// if missing
                if (!targetUrl.StartsWith("http://", StringComparison.OrdinalIgnoreCase) &&
                    !targetUrl.StartsWith("https://", StringComparison.OrdinalIgnoreCase))
                {
                    targetUrl = "https://" + targetUrl;
                }

                if (!Uri.TryCreate(targetUrl, UriKind.Absolute, out _))
                {
                    MessageBox.Show("صيغة الرابط الإلكتروني غير صالحة. يرجى إدخال رابط صحيح.", "رابط غير صالح", MessageBoxButton.OK, MessageBoxImage.Warning);
                    txtNewTargetUrl.Focus();
                    return;
                }

                using var db = new OAIDbContext();
                var newSource = new SearchSource
                {
                    PlatformName = platformName,
                    TargetUrl = targetUrl,
                    AccountUsername = string.IsNullOrWhiteSpace(accountUsername) ? null : accountUsername,
                    IsEnabled = true,
                    CreatedAt = DateTime.UtcNow
                };

                db.SearchSources.Add(newSource);
                db.SaveChanges();

                if (DataContext is SourcesAndRulesViewModel vm)
                {
                    vm.SearchSources.Add(newSource);
                    vm.StatusMessage = $"تمت إضافة منصة \"{newSource.PlatformName}\" بنجاح.";
                }

                // Clear input fields
                txtNewPlatformName.Text = string.Empty;
                txtNewTargetUrl.Text = string.Empty;
                txtNewAccountUsername.Text = string.Empty;

                MessageBox.Show($"تمت إضافة منصة \"{platformName}\" بنجاح!", "إضافة ناجحة", MessageBoxButton.OK, MessageBoxImage.Information);
            }
            catch (Exception ex)
            {
                var detailedError = ex.InnerException != null ? $"{ex.Message}\n\n{ex.InnerException.Message}" : ex.Message;
                MessageBox.Show($"خطأ في إضافة المصدر:\n\n{detailedError}", "خطأ قاعدة البيانات", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void btnDeleteSource_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                if (sender is Button btn && btn.Tag is int sourceId)
                {
                    var result = MessageBox.Show("هل أنت متأكد من حذف منصة التوظيف هذه من قاعدة البيانات؟", "تأكيد الحذف", MessageBoxButton.YesNo, MessageBoxImage.Question);
                    if (result == MessageBoxResult.Yes)
                    {
                        using var db = new OAIDbContext();
                        var sourceToDelete = db.SearchSources.FirstOrDefault(s => s.Id == sourceId);
                        if (sourceToDelete != null)
                        {
                            db.SearchSources.Remove(sourceToDelete);
                            db.SaveChanges();
                        }

                        if (DataContext is SourcesAndRulesViewModel vm)
                        {
                            var localItem = vm.SearchSources.FirstOrDefault(s => s.Id == sourceId);
                            if (localItem != null)
                            {
                                vm.SearchSources.Remove(localItem);
                            }
                            vm.StatusMessage = "تم حذف المصدر بنجاح.";
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                var detailedError = ex.InnerException != null ? $"{ex.Message}\n\n{ex.InnerException.Message}" : ex.Message;
                MessageBox.Show($"خطأ في حذف المصدر:\n\n{detailedError}", "خطأ", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void SourceToggle_Checked(object sender, RoutedEventArgs e)
        {
            UpdateSourceEnabledStatus(sender, true);
        }

        private void SourceToggle_Unchecked(object sender, RoutedEventArgs e)
        {
            UpdateSourceEnabledStatus(sender, false);
        }

        private void SourceToggle_Click(object sender, RoutedEventArgs e)
        {
            if (sender is CheckBox cb)
            {
                UpdateSourceEnabledStatus(sender, cb.IsChecked == true);
            }
        }

        private void UpdateSourceEnabledStatus(object sender, bool isEnabled)
        {
            try
            {
                if (sender is CheckBox cb && cb.Tag is int sourceId)
                {
                    using var db = new OAIDbContext();
                    var source = db.SearchSources.FirstOrDefault(s => s.Id == sourceId);
                    if (source != null)
                    {
                        source.IsEnabled = isEnabled;
                        db.SaveChanges();
                    }

                    if (DataContext is SourcesAndRulesViewModel vm)
                    {
                        var localSource = vm.SearchSources.FirstOrDefault(s => s.Id == sourceId);
                        if (localSource != null)
                        {
                            localSource.IsEnabled = isEnabled;
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"خطأ في تحديث حالة التفعيل: {ex.Message}", "خطأ", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }
    }
}
