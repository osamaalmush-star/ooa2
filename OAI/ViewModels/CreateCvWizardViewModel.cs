using System;
using System.Collections.ObjectModel;
using System.Collections.Specialized;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Input;
using Microsoft.Win32;
using OAI.Core.Models;
using OAI.Core.Services;
using OAI.Data;
using OAI.Models;
using OAI.Services;
using OAI.ViewModels.Base;

namespace OAI.ViewModels
{
    public class CreateCvWizardViewModel : ViewModelBase
    {
        private readonly AtsQualityChecker _checker = new();
        private readonly MasterProfileStore _store = new();
        private readonly GeminiApiService _gemini = new();

        public event Action? AtsStateChanged;

        private bool _showPathChoice = true;
        public bool ShowPathChoice
        {
            get => _showPathChoice;
            set
            {
                if (SetProperty(ref _showPathChoice, value))
                    OnPropertyChanged(nameof(ShowEditor));
            }
        }

        public bool ShowEditor => !ShowPathChoice;

        private string _editorMode = "manual";
        public string EditorMode
        {
            get => _editorMode;
            set => SetProperty(ref _editorMode, value);
        }

        private int _section = 1;
        public int Section
        {
            get => _section;
            set
            {
                if (SetProperty(ref _section, value))
                {
                    OnPropertyChanged(nameof(IsHeader));
                    OnPropertyChanged(nameof(IsExperience));
                    OnPropertyChanged(nameof(IsEducation));
                    OnPropertyChanged(nameof(IsSkills));
                }
            }
        }

        public bool IsHeader => Section == 1;
        public bool IsExperience => Section == 2;
        public bool IsEducation => Section == 3;
        public bool IsSkills => Section == 4;

        private string _fullName = string.Empty;
        public string FullName { get => _fullName; set { if (SetProperty(ref _fullName, value)) RefreshAts(); } }
        private string _targetJobTitle = string.Empty;
        public string TargetJobTitle { get => _targetJobTitle; set { if (SetProperty(ref _targetJobTitle, value)) RefreshAts(); } }
        private string _email = string.Empty;
        public string Email { get => _email; set { if (SetProperty(ref _email, value)) RefreshAts(); } }
        private string _phone = string.Empty;
        public string Phone { get => _phone; set { if (SetProperty(ref _phone, value)) RefreshAts(); } }
        private string _location = string.Empty;
        public string Location { get => _location; set { if (SetProperty(ref _location, value)) RefreshAts(); } }
        private string _linkedInUrl = string.Empty;
        public string LinkedInUrl { get => _linkedInUrl; set { if (SetProperty(ref _linkedInUrl, value)) RefreshAts(); } }
        private string _gitHubUrl = string.Empty;
        public string GitHubUrl { get => _gitHubUrl; set { if (SetProperty(ref _gitHubUrl, value)) RefreshAts(); } }

        private string _technicalSkillsText = string.Empty;
        public string TechnicalSkillsText { get => _technicalSkillsText; set { if (SetProperty(ref _technicalSkillsText, value)) RefreshAts(); } }
        private string _toolsText = string.Empty;
        public string ToolsText { get => _toolsText; set { if (SetProperty(ref _toolsText, value)) RefreshAts(); } }
        private string _softSkillsText = string.Empty;
        public string SoftSkillsText { get => _softSkillsText; set { if (SetProperty(ref _softSkillsText, value)) RefreshAts(); } }

        public ObservableCollection<ExperienceDraft> Experiences { get; } = new();
        public ObservableCollection<EducationDraft> EducationItems { get; } = new();
        public ObservableCollection<CertificationDraft> Certifications { get; } = new();
        public ObservableCollection<string> MissingRequirements { get; } = new();

        private int _atsScore;
        public int AtsScore { get => _atsScore; set => SetProperty(ref _atsScore, value); }

        private bool _canProceed;
        public bool CanProceed
        {
            get => _canProceed;
            private set
            {
                if (SetProperty(ref _canProceed, value))
                    AtsStateChanged?.Invoke();
            }
        }

        public string AtsBadge => CanProceed
            ? "السيرة الذاتية مكتملة ومتوافقة مع معايير ATS بنسبة 100%"
            : $"جاهزية ATS: {AtsScore}% — أكمل المتطلبات قبل التالي";

        private string _status = string.Empty;
        public string Status { get => _status; set => SetProperty(ref _status, value); }

        private string _resumePath = string.Empty;
        public string ResumePath { get => _resumePath; set => SetProperty(ref _resumePath, value); }

        private bool _isBusy;
        public bool IsBusy
        {
            get => _isBusy;
            set
            {
                if (SetProperty(ref _isBusy, value))
                    AtsStateChanged?.Invoke();
            }
        }

        public ICommand ChooseUploadCommand { get; }
        public ICommand ChooseManualCommand { get; }
        public ICommand BackToPathsCommand { get; }
        public ICommand UploadParseCommand { get; }
        public ICommand AddExperienceCommand { get; }
        public ICommand RemoveExperienceCommand { get; }
        public ICommand EnhanceBulletCommand { get; }
        public ICommand AddEducationCommand { get; }
        public ICommand RemoveEducationCommand { get; }
        public ICommand AddCertificationCommand { get; }
        public ICommand RemoveCertificationCommand { get; }
        public ICommand GoSectionCommand { get; }
        public ICommand ExportPdfCommand { get; }

        public CreateCvWizardViewModel()
        {
            ChooseUploadCommand = new RelayCommand(async () => await StartUploadAsync());
            ChooseManualCommand = new RelayCommand(StartManual);
            BackToPathsCommand = new RelayCommand(() => { ShowPathChoice = true; });
            UploadParseCommand = new RelayCommand(async () => await StartUploadAsync());
            AddExperienceCommand = new RelayCommand(() => { Experiences.Add(BindExperience(new ExperienceDraft())); RefreshAts(); });
            RemoveExperienceCommand = new RelayCommand(p =>
            {
                if (p is ExperienceDraft d) Experiences.Remove(d);
                RefreshAts();
            });
            EnhanceBulletCommand = new RelayCommand(p =>
            {
                if (p is ExperienceDraft d)
                    d.AchievementsText = string.Join(Environment.NewLine, SplitLines(d.AchievementsText).Select(BulletEnhancer.Enhance));
            });
            AddEducationCommand = new RelayCommand(() => { EducationItems.Add(BindEducation(new EducationDraft())); RefreshAts(); });
            RemoveEducationCommand = new RelayCommand(p =>
            {
                if (p is EducationDraft d) EducationItems.Remove(d);
                RefreshAts();
            });
            AddCertificationCommand = new RelayCommand(() => Certifications.Add(new CertificationDraft()));
            RemoveCertificationCommand = new RelayCommand(p =>
            {
                if (p is CertificationDraft d) Certifications.Remove(d);
            });
            GoSectionCommand = new RelayCommand(p =>
            {
                if (int.TryParse(p?.ToString(), out var n)) Section = n;
            });
            ExportPdfCommand = new RelayCommand(ExportPdf, () => CanProceed);

            Experiences.CollectionChanged += OnCollectionChanged;
            EducationItems.CollectionChanged += OnCollectionChanged;
            LoadStored();
            RefreshAts();
        }

        private void OnCollectionChanged(object? sender, NotifyCollectionChangedEventArgs e) => RefreshAts();

        private ExperienceDraft BindExperience(ExperienceDraft draft)
        {
            draft.Changed = RefreshAts;
            return draft;
        }

        private EducationDraft BindEducation(EducationDraft draft)
        {
            draft.Changed = RefreshAts;
            return draft;
        }

        private void StartManual()
        {
            EditorMode = "manual";
            ShowPathChoice = false;
            if (Experiences.Count == 0)
                Experiences.Add(BindExperience(new ExperienceDraft()));
            if (EducationItems.Count == 0)
                EducationItems.Add(BindEducation(new EducationDraft()));
            Section = 1;
            RefreshAts();
        }

        private async Task StartUploadAsync()
        {
            var dialog = new OpenFileDialog
            {
                Filter = "السيرة الذاتية|*.pdf;*.docx|PDF|*.pdf|Word|*.docx",
                Title = "اختر ملف السيرة الذاتية"
            };
            if (dialog.ShowDialog() != true) return;

            if (!DocumentTextExtractor.IsSupported(dialog.FileName) || Path.GetExtension(dialog.FileName).Equals(".txt", StringComparison.OrdinalIgnoreCase))
            {
                Status = "يدعم المعالج PDF و Word (.docx) فقط.";
                return;
            }

            IsBusy = true;
            Status = "جاري قراءة الملف...";
            try
            {
                var folder = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "UserFiles", "Resumes");
                Directory.CreateDirectory(folder);
                var dest = Path.Combine(folder, $"CV_{DateTime.Now:yyyyMMdd_HHmmss}_{Path.GetFileName(dialog.FileName)}");
                File.Copy(dialog.FileName, dest, true);
                ResumePath = dest;

                var text = DocumentTextExtractor.Extract(dest);
                if (string.IsNullOrWhiteSpace(text))
                {
                    Status = "تعذر قراءة الملف. استخدم PDF نصي أو Word docx.";
                    return;
                }

                var parsed = MasterProfileParser.Parse(text);
                var fromLlm = await TryParseWithGeminiAsync(text);
                ApplyProfile(Merge(parsed, fromLlm));
                EditorMode = "upload";
                ShowPathChoice = false;
                Section = 1;
                Status = "راجع البيانات المستخرجة وأكمل النواقص حتى تصبح جاهزية ATS 100%.";
            }
            catch (Exception ex)
            {
                Status = ex.Message;
            }
            finally
            {
                IsBusy = false;
                RefreshAts();
            }
        }

        private async Task<MasterUserProfile?> TryParseWithGeminiAsync(string text)
        {
            try
            {
                const string system = @"Extract ONLY facts written in the CV. Never invent missing values. Use empty strings/arrays when unknown.
Return JSON matching:
{""identity"":{""fullName"":"""",""targetJobTitle"":"""",""email"":"""",""phone"":"""",""location"":"""",""linkedInUrl"":"""",""gitHubUrl"":""""},""experiences"":[{""companyName"":"""",""roleTitle"":"""",""location"":"""",""isCurrentRole"":false,""responsibilitiesAndAchievements"":[],""associatedSkills"":[]}],""education"":[{""institution"":"""",""degree"":"""",""fieldOfStudy"":"""",""location"":"""",""graduationYear"":""""}],""certifications"":[],""skills"":{""technicalSkills"":[],""conceptsAndTools"":[],""softSkills"":[]}}
Output ONLY JSON.";
                var json = await _gemini.GenerateTextAsync("Parse this CV:\n" + text, system);
                return MasterProfileJsonMapper.FromJson(json);
            }
            catch
            {
                return null;
            }
        }

        private static MasterUserProfile Merge(MasterUserProfile offline, MasterUserProfile? llm)
        {
            if (llm == null) return offline;
            if (string.IsNullOrWhiteSpace(llm.Identity.FullName)) llm.Identity.FullName = offline.Identity.FullName;
            if (string.IsNullOrWhiteSpace(llm.Identity.Email)) llm.Identity.Email = offline.Identity.Email;
            if (string.IsNullOrWhiteSpace(llm.Identity.Phone)) llm.Identity.Phone = offline.Identity.Phone;
            if (string.IsNullOrWhiteSpace(llm.Identity.TargetJobTitle)) llm.Identity.TargetJobTitle = offline.Identity.TargetJobTitle;
            if (string.IsNullOrWhiteSpace(llm.Identity.Location)) llm.Identity.Location = offline.Identity.Location;
            if (llm.Experiences.Count == 0) llm.Experiences = offline.Experiences;
            if (llm.Education.Count == 0) llm.Education = offline.Education;
            if ((llm.Skills.TechnicalSkills.Count + llm.Skills.ConceptsAndTools.Count) == 0) llm.Skills = offline.Skills;
            return llm;
        }

        private void ApplyProfile(MasterUserProfile profile)
        {
            FullName = profile.Identity.FullName;
            TargetJobTitle = profile.Identity.TargetJobTitle;
            Email = profile.Identity.Email;
            Phone = profile.Identity.Phone;
            Location = profile.Identity.Location;
            LinkedInUrl = profile.Identity.LinkedInUrl;
            GitHubUrl = profile.Identity.GitHubUrl;
            TechnicalSkillsText = string.Join(", ", profile.Skills.TechnicalSkills);
            ToolsText = string.Join(", ", profile.Skills.ConceptsAndTools);
            SoftSkillsText = string.Join(", ", profile.Skills.SoftSkills);

            Experiences.Clear();
            foreach (var exp in profile.Experiences)
            {
                Experiences.Add(BindExperience(new ExperienceDraft
                {
                    CompanyName = exp.CompanyName,
                    RoleTitle = exp.RoleTitle,
                    Location = exp.Location,
                    IsCurrentRole = exp.IsCurrentRole,
                    StartDateText = exp.StartDate == default ? string.Empty : exp.StartDate.ToString("yyyy-MM"),
                    EndDateText = exp.EndDate?.ToString("yyyy-MM") ?? string.Empty,
                    AchievementsText = string.Join(Environment.NewLine, exp.ResponsibilitiesAndAchievements),
                    SkillsText = string.Join(", ", exp.AssociatedSkills)
                }));
            }
            if (Experiences.Count == 0)
                Experiences.Add(BindExperience(new ExperienceDraft()));

            EducationItems.Clear();
            foreach (var edu in profile.Education)
            {
                EducationItems.Add(BindEducation(new EducationDraft
                {
                    Institution = edu.Institution,
                    Degree = edu.Degree,
                    FieldOfStudy = edu.FieldOfStudy,
                    Location = edu.Location,
                    GraduationYear = edu.GraduationYear
                }));
            }
            if (EducationItems.Count == 0)
                EducationItems.Add(BindEducation(new EducationDraft()));

            Certifications.Clear();
            foreach (var cert in profile.Certifications)
            {
                Certifications.Add(new CertificationDraft
                {
                    Name = cert.Name,
                    IssuingOrganization = cert.IssuingOrganization,
                    IssueYear = cert.IssueYear,
                    CredentialUrl = cert.CredentialUrl
                });
            }
        }

        public MasterUserProfile BuildProfile()
        {
            return new MasterUserProfile
            {
                Identity = new PersonalInfo
                {
                    FullName = FullName.Trim(),
                    TargetJobTitle = TargetJobTitle.Trim(),
                    Email = Email.Trim(),
                    Phone = Phone.Trim(),
                    Location = Location.Trim(),
                    LinkedInUrl = LinkedInUrl.Trim(),
                    GitHubUrl = GitHubUrl.Trim()
                },
                Experiences = Experiences.Select(e => e.ToModel()).Where(e => !string.IsNullOrWhiteSpace(e.CompanyName) || !string.IsNullOrWhiteSpace(e.RoleTitle) || e.ResponsibilitiesAndAchievements.Count > 0).ToList(),
                Education = EducationItems.Select(e => e.ToModel()).Where(e => !string.IsNullOrWhiteSpace(e.Institution) || !string.IsNullOrWhiteSpace(e.Degree)).ToList(),
                Certifications = Certifications.Select(c => c.ToModel()).Where(c => !string.IsNullOrWhiteSpace(c.Name)).ToList(),
                TargetRoles = string.IsNullOrWhiteSpace(TargetJobTitle) ? new System.Collections.Generic.List<string>() : new System.Collections.Generic.List<string> { TargetJobTitle.Trim() },
                Skills = new SkillSet
                {
                    TechnicalSkills = SplitCsv(TechnicalSkillsText),
                    ConceptsAndTools = SplitCsv(ToolsText),
                    SoftSkills = SplitCsv(SoftSkillsText)
                },
                LastUpdated = DateTime.UtcNow
            };
        }

        public void SetGeminiKey(string apiKey, string modelName)
        {
            if (!string.IsNullOrWhiteSpace(apiKey))
                _gemini.SetApiKey(apiKey.Trim());
            if (!string.IsNullOrWhiteSpace(modelName))
                _gemini.SetModelName(modelName.Trim());
        }

        public void RefreshAts()
        {
            var result = _checker.ValidateProfile(BuildProfile());
            AtsScore = result.Score;
            CanProceed = result.IsValid;
            MissingRequirements.Clear();
            foreach (var item in result.MissingRequirements)
                MissingRequirements.Add(item);
            OnPropertyChanged(nameof(AtsBadge));
            (ExportPdfCommand as RelayCommand)?.RaiseCanExecuteChanged();
            AtsStateChanged?.Invoke();
        }

        public bool TryCommit()
        {
            RefreshAts();
            if (!CanProceed) return false;
            var profile = BuildProfile();
            _store.Save(profile);
            SaveToSqlProfile(profile);
            ExportPdfSilent(profile);
            return true;
        }

        private void SaveToSqlProfile(MasterUserProfile profile)
        {
            using var db = new OAIDbContext();
            var row = db.UserProfiles.FirstOrDefault() ?? db.UserProfiles.Add(new UserProfile()).Entity;
            row.FullName = profile.Identity.FullName;
            row.Email = profile.Identity.Email;
            row.PhoneNumber = profile.Identity.Phone;
            row.Location = profile.Identity.Location;
            row.TargetJobTitle = profile.Identity.TargetJobTitle;
            row.LinkedInUrl = profile.Identity.LinkedInUrl;
            row.GitHubUrl = profile.Identity.GitHubUrl;
            row.Education = string.Join("; ", profile.Education.Select(e => $"{e.Degree} {e.FieldOfStudy} {e.Institution}".Trim()));
            row.SkillsList = string.Join(", ", profile.Skills.TechnicalSkills.Concat(profile.Skills.ConceptsAndTools));
            row.Keywords = string.Join(", ", profile.Skills.TechnicalSkills);
            row.ExperienceSummary = string.Join(" | ", profile.Experiences.Select(e => $"{e.RoleTitle} @ {e.CompanyName}"));
            row.YearsOfExperience = profile.Experiences.Count.ToString();
            if (!string.IsNullOrWhiteSpace(ResumePath))
                row.ResumeFilePath = ResumePath;
            else if (string.IsNullOrWhiteSpace(row.ResumeFilePath) || !File.Exists(row.ResumeFilePath))
                row.ResumeFilePath = LastExportedPdfPath();
            row.UpdatedAt = DateTime.UtcNow;
            db.SaveChanges();
        }

        private void ExportPdf()
        {
            if (!CanProceed) return;
            var path = LastExportedPdfPath();
            AtsPdfExporter.Export(BuildProfile(), path);
            ResumePath = path;
            Status = "تم تصدير ملف ATS PDF.";
            MessageBox.Show("تم حفظ السيرة بصيغة ATS PDF.", "تصدير", MessageBoxButton.OK, MessageBoxImage.Information);
        }

        private void ExportPdfSilent(MasterUserProfile profile)
        {
            try
            {
                var path = LastExportedPdfPath();
                AtsPdfExporter.Export(profile, path);
                if (string.IsNullOrWhiteSpace(ResumePath) || !File.Exists(ResumePath))
                    ResumePath = path;
            }
            catch
            {
            }
        }

        private static string LastExportedPdfPath()
        {
            var folder = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "UserFiles", "Resumes");
            Directory.CreateDirectory(folder);
            return Path.Combine(folder, "MasterCV_ATS.pdf");
        }

        private void LoadStored()
        {
            try
            {
                var stored = _store.Load();
                if (stored != null && !string.IsNullOrWhiteSpace(stored.Identity.FullName))
                    ApplyProfile(stored);
            }
            catch
            {
            }
            ShowPathChoice = true;
        }

        private static System.Collections.Generic.List<string> SplitCsv(string text) =>
            (text ?? string.Empty).Split(new[] { ',', '،', ';', '\n' }, StringSplitOptions.RemoveEmptyEntries)
                .Select(s => s.Trim()).Where(s => s.Length > 0).Distinct(StringComparer.OrdinalIgnoreCase).ToList();

        private static System.Collections.Generic.List<string> SplitLines(string text) =>
            (text ?? string.Empty).Replace("\r", "").Split('\n').Select(s => s.Trim()).Where(s => s.Length > 0).ToList();
    }

    public class ExperienceDraft : ViewModelBase
    {
        public Action? Changed;
        private string _companyName = string.Empty;
        public string CompanyName { get => _companyName; set { if (SetProperty(ref _companyName, value)) Changed?.Invoke(); } }
        private string _roleTitle = string.Empty;
        public string RoleTitle { get => _roleTitle; set { if (SetProperty(ref _roleTitle, value)) Changed?.Invoke(); } }
        private string _location = string.Empty;
        public string Location { get => _location; set { if (SetProperty(ref _location, value)) Changed?.Invoke(); } }
        private string _startDateText = string.Empty;
        public string StartDateText { get => _startDateText; set => SetProperty(ref _startDateText, value); }
        private string _endDateText = string.Empty;
        public string EndDateText { get => _endDateText; set => SetProperty(ref _endDateText, value); }
        private bool _isCurrentRole;
        public bool IsCurrentRole { get => _isCurrentRole; set => SetProperty(ref _isCurrentRole, value); }
        private string _achievementsText = string.Empty;
        public string AchievementsText { get => _achievementsText; set { if (SetProperty(ref _achievementsText, value)) Changed?.Invoke(); } }
        private string _skillsText = string.Empty;
        public string SkillsText { get => _skillsText; set => SetProperty(ref _skillsText, value); }

        public WorkExperience ToModel()
        {
            DateTime.TryParse(_startDateText, out var start);
            DateTime? end = DateTime.TryParse(_endDateText, out var e) ? e : null;
            return new WorkExperience
            {
                CompanyName = CompanyName.Trim(),
                RoleTitle = RoleTitle.Trim(),
                Location = Location.Trim(),
                StartDate = start,
                EndDate = IsCurrentRole ? null : end,
                IsCurrentRole = IsCurrentRole,
                ResponsibilitiesAndAchievements = (AchievementsText ?? string.Empty).Replace("\r", "").Split('\n').Select(s => s.Trim()).Where(s => s.Length > 0).ToList(),
                AssociatedSkills = (SkillsText ?? string.Empty).Split(new[] { ',', '،' }, StringSplitOptions.RemoveEmptyEntries).Select(s => s.Trim()).ToList()
            };
        }
    }

    public class EducationDraft : ViewModelBase
    {
        public Action? Changed;
        private string _institution = string.Empty;
        public string Institution { get => _institution; set { if (SetProperty(ref _institution, value)) Changed?.Invoke(); } }
        private string _degree = string.Empty;
        public string Degree { get => _degree; set { if (SetProperty(ref _degree, value)) Changed?.Invoke(); } }
        private string _fieldOfStudy = string.Empty;
        public string FieldOfStudy { get => _fieldOfStudy; set { if (SetProperty(ref _fieldOfStudy, value)) Changed?.Invoke(); } }
        private string _location = string.Empty;
        public string Location { get => _location; set => SetProperty(ref _location, value); }
        private string _graduationYear = string.Empty;
        public string GraduationYear { get => _graduationYear; set => SetProperty(ref _graduationYear, value); }

        public EducationItem ToModel() => new()
        {
            Institution = Institution.Trim(),
            Degree = Degree.Trim(),
            FieldOfStudy = FieldOfStudy.Trim(),
            Location = Location.Trim(),
            GraduationYear = GraduationYear.Trim()
        };
    }

    public class CertificationDraft : ViewModelBase
    {
        private string _name = string.Empty;
        public string Name { get => _name; set => SetProperty(ref _name, value); }
        private string _issuingOrganization = string.Empty;
        public string IssuingOrganization { get => _issuingOrganization; set => SetProperty(ref _issuingOrganization, value); }
        private string _issueYear = string.Empty;
        public string IssueYear { get => _issueYear; set => SetProperty(ref _issueYear, value); }
        private string _credentialUrl = string.Empty;
        public string CredentialUrl { get => _credentialUrl; set => SetProperty(ref _credentialUrl, value); }

        public CertificationItem ToModel() => new()
        {
            Name = Name.Trim(),
            IssuingOrganization = IssuingOrganization.Trim(),
            IssueYear = IssueYear.Trim(),
            CredentialUrl = CredentialUrl.Trim()
        };
    }
}
