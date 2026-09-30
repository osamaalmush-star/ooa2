using System;
using System.Diagnostics;
using System.Windows;

namespace OAI.Services
{
    public static class NotificationService
    {
        public static void ShowJobAlertNotification(string jobTitle, string companyName, double matchScore, string jobUrl)
        {
            try
            {
                // Fire native Windows PowerShell Toast Notification
                var title = $"تنبيه وظيفة ({matchScore:F0}% توافق)";
                var body = $"{jobTitle} at {companyName}";

                ExecutePowerShellToast(title, body);
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Notification Error: {ex.Message}");
            }
        }

        public static void ShowEmailMonitorStatus(string title, string message)
        {
            try
            {
                ExecutePowerShellToast(title, message);
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Notification Error: {ex.Message}");
            }
        }

        private static void ExecutePowerShellToast(string title, string message)
        {
            try
            {
                var safeTitle = title.Replace("'", "''").Replace("\"", "`\"");
                var safeMessage = message.Replace("'", "''").Replace("\"", "`\"");
                var script = $"Add-Type -AssemblyName System.Windows.Forms; $icon = New-Object System.Windows.Forms.NotifyIcon; $icon.Icon = [System.Drawing.SystemIcons]::Information; $icon.Visible = $true; $icon.ShowBalloonTip(4000, '{safeTitle}', '{safeMessage}', [System.Windows.Forms.ToolTipIcon]::Info); Start-Sleep -Seconds 5; $icon.Dispose()";
                
                var psi = new ProcessStartInfo("powershell", $"-NoProfile -Command \"{script}\"")
                {
                    CreateNoWindow = true,
                    UseShellExecute = false
                };
                Process.Start(psi);
            }
            catch
            {
                // Fallback to WPF MessageBox
                Application.Current?.Dispatcher?.Invoke(() =>
                {
                    MessageBox.Show(message, title, MessageBoxButton.OK, MessageBoxImage.Information);
                });
            }
        }
    }
}
