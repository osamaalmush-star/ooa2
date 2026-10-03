using System;
using System.Windows;
using OAI.Data;
using OAI.Services;

namespace OAI
{
    /// <summary>
    /// Interaction logic for App.xaml
    /// </summary>
    public partial class App : Application
    {
        protected override void OnStartup(StartupEventArgs e)
        {
            DispatcherUnhandledException += (s, args) =>
            {
                MessageBox.Show($"حدث خطأ غير متوقع في التطبيق:\n\n{args.Exception.Message}\n\n{args.Exception.InnerException?.Message}", 
                                "خطأ في التشغيل", MessageBoxButton.OK, MessageBoxImage.Error);
                args.Handled = true;
            };

            AppDomain.CurrentDomain.UnhandledException += (s, args) =>
            {
                if (args.ExceptionObject is Exception ex)
                {
                    MessageBox.Show($"خطأ عام حرج:\n\n{ex.Message}\n\n{ex.InnerException?.Message}", 
                                    "خطأ حرج", MessageBoxButton.OK, MessageBoxImage.Error);
                }
            };

            InitializeDatabaseSafely();
            StartMonitoring();
            base.OnStartup(e);
        }

        protected override void OnExit(ExitEventArgs e)
        {
            try
            {
                MonitoringScheduler.Current?.Stop();
            }
            catch
            {
            }
            base.OnExit(e);
        }

        private static void StartMonitoring()
        {
            try
            {
                var scheduler = new MonitoringScheduler();
                Current.Properties["Monitor"] = scheduler;
                scheduler.Start();
            }
            catch
            {
            }
        }

        private static void InitializeDatabaseSafely()
        {
            try
            {
                using var db = new OAIDbContext();
                db.Database.EnsureCreated();
                DbInitializer.Initialize(db);
            }
            catch (Exception ex)
            {
                MessageBox.Show(
                    $"خطأ أثناء تهيئة قاعدة البيانات LocalDB (OAIDatabase):\n{ex.Message}\n\n{ex.InnerException?.Message}",
                    "خطأ قاعدة البيانات",
                    MessageBoxButton.OK,
                    MessageBoxImage.Error);
            }
        }
    }
}
