// NotificationService.cs
// Joy — Windows
//
// Sends Windows toast / balloon notifications when a focus session completes.
// Uses the Hardcodet.NotifyIcon.Wpf tray icon for balloon tips on older Windows,
// and falls back gracefully on Windows 10/11.

using System.Windows;

namespace Joy.Windows.Services;

/// <summary>Delivers system notifications for Joy events.</summary>
public sealed class NotificationService
{
    /// <summary>
    /// Shows a balloon tip or toast when the focus session ends.
    /// </summary>
    /// <param name="minutes">Session duration in minutes.</param>
    public void SendTimerComplete(int minutes)
    {
        // Retrieve the TaskbarIcon defined in App.xaml resources
        if (Application.Current.Resources["TrayIcon"] is Hardcodet.Wpf.TaskbarNotification.TaskbarIcon icon)
        {
            icon.ShowBalloonTip(
                title:   "Joy",
                message: $"Focus session complete! {minutes} min of great work. 🎉",
                symbol:  Hardcodet.Wpf.TaskbarNotification.BalloonIcon.Info
            );
        }
    }
}
