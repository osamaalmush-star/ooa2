using System;
using System.Linq;
using System.Threading.Tasks;
using System.Windows.Threading;
using OAI.Data;
using OAI.Models;

namespace OAI.Services
{
    public sealed class MonitoringScheduler
    {
        private readonly DispatcherTimer _timer = new();
        private bool _running;

        public event Action? TickCompleted;

        public MonitoringScheduler()
        {
            _timer.Tick += (_, _) => _ = RunOnceAsync();
        }

        public static MonitoringScheduler? Current =>
            System.Windows.Application.Current?.Properties["Monitor"] as MonitoringScheduler;

        public void Start()
        {
            if (!SetupStatusService.GetStatus().IsComplete)
            {
                _timer.Stop();
                return;
            }

            var minutes = 60;
            try
            {
                using var db = new OAIDbContext();
                minutes = db.AppSettings.FirstOrDefault()?.MonitoringIntervalMinutes ?? 60;
            }
            catch
            {
            }

            if (minutes < 15) minutes = 15;
            _timer.Interval = TimeSpan.FromMinutes(minutes);
            _timer.Stop();
            _timer.Start();
        }

        public void Stop() => _timer.Stop();

        public async Task RunOnceAsync()
        {
            if (_running) return;
            if (!SetupStatusService.GetStatus().IsComplete) return;
            _running = true;
            try
            {
                await Task.Run(async () =>
                {
                    using var db = new OAIDbContext();
                    var email = db.EmailSettings.FirstOrDefault();
                    var profile = db.UserProfiles.FirstOrDefault() ?? new UserProfile();
                    if (email != null && email.IsEnabled)
                    {
                        var monitor = new EmailMonitorService(new JobMatchingEngine(new GeminiApiService()));
                        await monitor.PollJobEmailsAsync(email, profile).ConfigureAwait(false);
                    }

                    var discovery = new JobDiscoveryService();
                    await discovery.DiscoverAsync(true).ConfigureAwait(false);
                }).ConfigureAwait(true);

                TickCompleted?.Invoke();
            }
            catch
            {
            }
            finally
            {
                _running = false;
            }
        }
    }
}
