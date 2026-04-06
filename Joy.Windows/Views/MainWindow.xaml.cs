// MainWindow.xaml.cs — Joy Windows
// Wires iris gaze tracking → robot eye movement + all moods + sounds.

using Joy.Windows.Controls;
using Joy.Windows.Models;
using Joy.Windows.Services;
using System.IO;
using System.Windows;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Threading;

namespace Joy.Windows.Views;

public partial class MainWindow : Window
{
    private readonly TimerState            _timer  = new();
    private readonly CameraService         _camera = new();
    private readonly FocusDetectorService? _detect;
    private readonly FocusMoodController   _mood   = new();
    private readonly SoundService          _sound  = new();
    private readonly NotificationService   _notify = new();

    private readonly DispatcherTimer _focusLoop;
    private readonly DispatcherTimer _shakeTimer;
    private bool _shakeRight;

    private DistractionPhase _prevPhase = DistractionPhase.None;
    private Mood             _prevMood  = Mood.Normal;

    public MainWindow()
    {
        InitializeComponent();

        try
        {
            string bd = AppDomain.CurrentDomain.BaseDirectory;
            string pd = Path.GetFullPath(Path.Combine(bd, @"..\..\.."));
            _detect = new FocusDetectorService(
                Find("haarcascade_frontalface_default.xml", bd, pd),
                Find("haarcascade_eye.xml", bd, pd));
            _camera.FrameCaptured += f => _detect.ProcessFrame(f);
        }
        catch { _detect = null; }

        _mood.OnTimerReset   = () => { _sound.PlayTimerReset(); _timer.ResetTimer(); };
        _mood.OnUserReturned = () => { _timer.Frozen = false; _timer.DistractionPhase = DistractionPhase.None; };

        _timer.OnComplete = () =>
        {
            _sound.PlayCelebration();
            _sound.PlayTimerEnd();
            EyesControl.SetMood(Mood.Happy);
            _notify.SendTimerComplete(_timer.TotalSeconds / 60);
        };

        // 10 Hz focus + gaze loop
        _focusLoop = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(100) };
        _focusLoop.Tick += FocusTick;

        _shakeTimer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(75) };
        _shakeTimer.Tick += (_, _) =>
        {
            _shakeRight = !_shakeRight;
            TxtTimer.RenderTransform = new TranslateTransform(_shakeRight ? 3 : -3, 0);
        };

        Loaded += (_, _) =>
        {
            _camera.Start();
            _focusLoop.Start();
            _sound.PlayStartup();
            EyesControl.SetMood(Mood.Normal);
        };
        Closed += (_, _) => Cleanup();

        Left = SystemParameters.WorkArea.Right  - Width  - 20;
        Top  = SystemParameters.WorkArea.Bottom - Height - 20;
    }

    // ─── Main loop ────────────────────────────────────────────────────────────
    private void FocusTick(object? s, EventArgs e)
    {
        bool focused = _detect?.IsFocused ?? true;

        // ── Feed gaze point into robot eyes ───────────────────────────────────
        // GazePoint: (0,0)=top-left, (1,1)=bottom-right, (0.5,0.5)=center
        // Map to eye constraint space so eyes follow your actual iris movement
        if (_detect != null)
        {
            var (gx, gy) = _detect.GazePoint;
            EyesControl.SetGazeTarget(gx, gy);
        }

        // ── Mood controller ───────────────────────────────────────────────────
        _mood.IsTimerActive = _timer.IsRunning;
        _mood.Update(focused);

        var newMood  = _mood.CurrentMood;
        var newPhase = _mood.CurrentPhase;

        // Mood changed → update eyes
        if (newMood != _prevMood)
        {
            EyesControl.SetMood(newMood);
            _prevMood = newMood;
        }

        // Phase changed → sound + red eyes + shake
        if (newPhase != _prevPhase)
        {
            switch (newPhase)
            {
                case DistractionPhase.Angry:
                    _sound.PlayDistracted();
                    break;
                case DistractionPhase.Critical:
                    _sound.PlayDistracted();
                    break;
                case DistractionPhase.None when _prevPhase != DistractionPhase.None:
                    _sound.PlayFocusGained();
                    break;
            }

            EyesControl.SetRedEyes(newPhase == DistractionPhase.Critical);

            if (newPhase == DistractionPhase.Critical && !_shakeTimer.IsEnabled)
                _shakeTimer.Start();
            else if (newPhase != DistractionPhase.Critical && _shakeTimer.IsEnabled)
            { _shakeTimer.Stop(); TxtTimer.RenderTransform = null; }

            _prevPhase = newPhase;
        }

        // Timer freeze + display
        if (_timer.IsRunning)
        {
            _timer.Frozen           = newPhase != DistractionPhase.None;
            _timer.DistractionPhase = newPhase;
            TxtTimer.Text = _timer.DisplayText;
            TxtTimer.Foreground = newPhase == DistractionPhase.Critical
                ? new SolidColorBrush(Color.FromRgb(255, 55, 55))
                : Brushes.White;
        }
    }

    // ─── UI ───────────────────────────────────────────────────────────────────
    private void BtnStart_Click(object sender, RoutedEventArgs e)
    {
        int mins = GetMins();
        if (mins > 0) _timer.Start(mins); else _timer.StartNoTimer();
        SetupPanel.Visibility   = Visibility.Collapsed;
        RunningPanel.Visibility = Visibility.Visible;
        TxtTimer.Text = _timer.DisplayText;
        _prevPhase = DistractionPhase.None; _prevMood = Mood.Normal;
        EyesControl.SetMood(Mood.Normal);
        EyesControl.SetRedEyes(false);
        _sound.PlayFocusGained();
    }

    private void BtnStop_Click(object sender, RoutedEventArgs e)
    {
        _timer.Stop(); _shakeTimer.Stop();
        TxtTimer.RenderTransform = null;
        RunningPanel.Visibility = Visibility.Collapsed;
        SetupPanel.Visibility   = Visibility.Visible;
        EyesControl.SetMood(Mood.Normal);
        EyesControl.SetRedEyes(false);
        _prevPhase = DistractionPhase.None; _prevMood = Mood.Normal;
    }

    private void BtnMinimize_Click(object sender, RoutedEventArgs e) => WindowState = WindowState.Minimized;
    private void BtnClose_Click(object sender, RoutedEventArgs e)    => Application.Current.Shutdown();
    private void Window_MouseLeftButtonDown(object sender, MouseButtonEventArgs e) => DragMove();

    private int GetMins()
    {
        foreach (var rb in new[] { Rb00, Rb15, Rb25, Rb45, Rb60 })
            if (rb.IsChecked == true && int.TryParse(rb.Tag?.ToString(), out int m)) return m;
        return 0;
    }

    private static string Find(string file, params string[] dirs)
    {
        foreach (var d in dirs) { var p = Path.Combine(d, file); if (File.Exists(p)) return p; }
        return file;
    }

    private void Cleanup()
    {
        _focusLoop.Stop(); _shakeTimer.Stop();
        _camera.Dispose(); _detect?.Dispose(); _sound.Dispose();
    }
}
