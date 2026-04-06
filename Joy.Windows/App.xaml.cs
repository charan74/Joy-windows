// App.xaml.cs
// Joy — Windows
//
// Application entry point. Manages the system tray icon lifecycle
// and shows/hides the main floating window.

using Joy.Windows.Views;
using System.Windows;
using System.Windows.Threading;

namespace Joy.Windows;

public partial class App : Application
{
    private MainWindow? _mainWindow;

    protected override void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);

        // Catch any unhandled exceptions and show them instead of silent crash
        DispatcherUnhandledException += (s, ex) =>
        {
            MessageBox.Show(
                $"Joy crashed:\n\n{ex.Exception.Message}\n\n{ex.Exception.StackTrace}",
                "Joy Error",
                MessageBoxButton.OK,
                MessageBoxImage.Error
            );
            ex.Handled = true;
        };

        try
        {
            _mainWindow = new MainWindow();
            _mainWindow.Show();
        }
        catch (Exception ex)
        {
            MessageBox.Show(
                $"Failed to start Joy:\n\n{ex.Message}\n\n{ex.StackTrace}",
                "Joy Startup Error",
                MessageBoxButton.OK,
                MessageBoxImage.Error
            );
            Shutdown();
        }
    }

    private void TrayShow_Click(object sender, RoutedEventArgs e)
    {
        _mainWindow ??= new MainWindow();
        _mainWindow.Show();
        _mainWindow.WindowState = WindowState.Normal;
        _mainWindow.Activate();
    }

    private void TrayQuit_Click(object sender, RoutedEventArgs e)
    {
        Shutdown();
    }
}
