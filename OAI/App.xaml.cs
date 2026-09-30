using System;
using System.Windows;
using Microsoft.EntityFrameworkCore;
using OAI.Data;

namespace OAI
{
    /// <summary>
    /// Interaction logic for App.xaml
    /// </summary>
    public partial class App : Application
    {
        protected override void OnStartup(StartupEventArgs e)
        {
            base.OnStartup(e);

            // Global exception handlers to prevent silent crashes
            this.DispatcherUnhandledException += (s, args) =>
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
