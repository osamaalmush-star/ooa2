using System.Windows.Input;
using OAI.Services;
using OAI.ViewModels.Base;

namespace OAI.ViewModels
{
    public class MainViewModel : ViewModelBase
    {
        private ViewModelBase _currentView = null!;
        public ViewModelBase CurrentView
        {
            get => _currentView;
            set
            {
                if (SetProperty(ref _currentView, value))
                {
                    OnPropertyChanged(nameof(IsDashboard));
                    OnPropertyChanged(nameof(IsSources));
                    OnPropertyChanged(nameof(ShowNavigation));
                }
            }
        }

        public bool ShowNavigation => CurrentView is not SetupWizardViewModel;

        public bool IsDashboard
        {
            get => CurrentView == DashboardVM;
            set { if (value && ShowNavigation) CurrentView = DashboardVM; }
        }

        public bool IsSources
        {
            get => CurrentView == SourcesAndRulesVM;
            set { if (value && ShowNavigation) CurrentView = SourcesAndRulesVM; }
        }

        public DashboardViewModel DashboardVM { get; }
        public SourcesAndRulesViewModel SourcesAndRulesVM { get; }
        public SetupWizardViewModel? WizardVM { get; private set; }

        public ICommand NavigateDashboardCommand { get; }
        public ICommand NavigateSourcesAndRulesCommand { get; }

        public MainViewModel()
        {
            DashboardVM = new DashboardViewModel();
            SourcesAndRulesVM = new SourcesAndRulesViewModel();

            NavigateDashboardCommand = new RelayCommand(() =>
            {
                DashboardVM.LoadDataFromDatabase();
                CurrentView = DashboardVM;
            });
            NavigateSourcesAndRulesCommand = new RelayCommand(() => CurrentView = SourcesAndRulesVM);

            var status = SetupStatusService.GetStatus();
            if (!status.IsComplete)
            {
                WizardVM = new SetupWizardViewModel();
                WizardVM.SetupCompleted += OnSetupCompleted;
                _currentView = WizardVM;
            }
            else
            {
                _currentView = DashboardVM;
                DashboardVM.AttachMonitor();
                try { MonitoringScheduler.Current?.Start(); } catch { }
            }
        }

        private void OnSetupCompleted()
        {
            DashboardVM.LoadDataFromDatabase();
            SourcesAndRulesVM.Reload();
            CurrentView = DashboardVM;
            DashboardVM.AttachMonitor();
            try { MonitoringScheduler.Current?.Start(); } catch { }
        }
    }
}
