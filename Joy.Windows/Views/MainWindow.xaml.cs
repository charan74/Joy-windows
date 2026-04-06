// MainWindow.xaml.cs — Joy Windows — MASTER VERSION
//
// Full orchestration:
//   - Eye iris tracking → robot eyes follow your real eyes
//   - All 7 emotions triggered correctly
//   - Sounds on every phase transition
//   - Timer freeze/unfreeze
//   - Red eyes only on CRITICAL
//   - Shake text on CRITICAL

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
    // ─── Services ─────────────────────────────────────────────────────────────
    private readonly TimerState            _timer   = new();
    private readonly CameraService         _camera  = new();
    private readonly FocusDetectorService? _detect;
    private readonly FocusMoodController   _mood    = new();
    private readonly SoundService          _sound   = new();
    private readonly NotificationService   _notify  = new();

    // ─── Timers ───────────────────────────────────────────────────────────────
    private readonly DispatcherTimer _focusLoop;
    private readonly DispatcherTimer _shakeTimer;

    // ─── Change tracking (only act on transitions) ────────────────────────────
    private Mood             _lastMood  = Mood.Normal;
    private DistractionPhase _lastPhase = DistractionPhase.None;
    private bool             _shakeOn;

    // ─── Init ─────────────────────────────────────────────────────────────────
    public MainWindow()
    {
        InitializeComponent();

        // Load cascade files for eye detection
        try
        {
            string bd  = AppDomain.CurrentDomain.BaseDirectory;
            string pd  = Path.GetFullPath(Path.Combine(bd, @"..\..\.."));
            _detect = new FocusDetectorService(
                FindFile("haarcascade_frontalface_default.xml", bd, pd),
                FindFile("haarcascade_eye.xml",                 bd, pd));
            _camera.FrameCaptured += frame => _detect.ProcessFrame(frame);
        }
        catch { _detect = null; }

        // Wire distraction callbacks
        _mood.OnTimerReset = () =>
        {
            _sound.PlayTimerReset();
            _timer.ResetTimer();
        };
        _mood.OnUserReturned = () =>
        {
            _timer.Frozen           = false;
            _timer.DistractionPhase = DistractionPhase.None;
            _sound.PlayFocusGained();
        };

        // Wire timer completion
        _timer.OnComplete = () =>
        {
            _sound.PlayCelebration();
            _sound.PlayTimerEnd();
            EyesControl.SetMood(Mood.Happy);
            _notify.SendTimerComplete(_timer.TotalSeconds / 60);
        };

        // 10 Hz focus + emotion loop
        _focusLoop = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(100) };
        _focusLoop.Tick += OnFocusTick;

        // Shake timer for CRITICAL text
        bool shakeRight = false;
        _shakeTimer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(70) };
        _shakeTimer.Tick += (_, _) =>
        {
            shakeRight = !shakeRight;
            TxtTimer.RenderTransform = new TranslateTransform(shakeRight ? 3.5 : -3.5, 0);
        };

        Loaded += (_, _) =>
        {
            _camera.Start();
            _focusLoop.Start();
            _sound.PlayStartup();
            EyesControl.SetMood(Mood.Normal);
        };
        Closed += (_, _) => Cleanup();

        // Position bottom-right of work area
        Left = SystemParameters.WorkArea.Right  - Width  - 24;
        Top  = SystemParameters.WorkArea.Bottom - Height - 24;
    }

    // ─── Main focus/mood loop ─────────────────────────────────────────────────
    private void OnFocusTick(object? sender, EventArgs e)
    {
        bool focused = _detect?.IsFocused ?? true;

        // Feed real iris position into robot eyes (they follow your eyes)
        if (_detect != null)
        {
            var (gx, gy) = _detect.GazePoint;
            EyesControl.SetGazeTarget(gx, gy);
        }

        // Run mood state machine
        _mood.IsTimerActive = _timer.IsRunning;
        _mood.Update(focused);

        var newMood  = _mood.CurrentMood;
        var newPhase = _mood.CurrentPhase;

        // ── Mood changed → update eyes ────────────────────────────────────────
        if (newMood != _lastMood)
        {
            EyesControl.SetMood(newMood);
            _lastMood = newMood;
        }

        // ── Phase changed → sound + red eyes + shake ──────────────────────────
        if (newPhase != _lastPhase)
        {
            HandlePhaseChange(_lastPhase, newPhase);
            _lastPhase = newPhase;
        }

        // ── Timer freeze/display ──────────────────────────────────────────────
        if (_timer.IsRunning)
        {
            _timer.Frozen           = newPhase != DistractionPhase.None;
            _timer.DistractionPhase = newPhase;
            RefreshTimer();
        }
    }

    private void HandlePhaseChange(DistractionPhase from, DistractionPhase to)
    {
        // Play sound on transition
        switch (to)
        {
            case DistractionPhase.Searching:
                // Subtle — no sound yet, just curious eyes
                break;
            case DistractionPhase.Angry:
                _sound.PlayDistracted();
                break;
            case DistractionPhase.Critical:
                _sound.PlayDistracted();
                break;
            case DistractionPhase.Reset:
                // Sound handled by OnTimerReset callback
                break;
            case DistractionPhase.None when from != DistractionPhase.None:
                // User returned — sound handled by OnUserReturned callback
                break;
        }

        // Red eyes only during critical
        bool showRed = to == DistractionPhase.Critical;
        EyesControl.SetRedEyes(showRed);

        // Shake text during critical
        bool shouldShake = to == DistractionPhase.Critical;
        if (shouldShake && !_shakeOn)
        {
            _shakeTimer.Start();
            _shakeOn = true;
        }
        else if (!shouldShake && _shakeOn)
        {
            _shakeTimer.Stop();
            TxtTimer.RenderTransform = null;
            _shakeOn = false;
        }
    }

    // ─── Timer UI ─────────────────────────────────────────────────────────────
    private void RefreshTimer()
    {
        TxtTimer.Text = _timer.DisplayText;

        TxtTimer.Foreground = _timer.DistractionPhase == DistractionPhase.Critical
            ? new SolidColorBrush(Color.FromRgb(255, 55, 55))
            : Brushes.White;
    }

    // ─── Button handlers ──────────────────────────────────────────────────────
    private void BtnStart_Click(object sender, RoutedEventArgs e)
    {
        int mins = GetSelectedMinutes();
        if (mins > 0) _timer.Start(mins);
        else          _timer.StartNoTimer();

        SetupPanel.Visibility   = Visibility.Collapsed;
        RunningPanel.Visibility = Visibility.Visible;
        RefreshTimer();

        // Reset state
        _lastMood  = Mood.Normal;
        _lastPhase = DistractionPhase.None;
        EyesControl.SetMood(Mood.Normal);
        EyesControl.SetRedEyes(false);
        _sound.PlayFocusGained();
    }

    private void BtnStop_Click(object sender, RoutedEventArgs e)
    {
        _timer.Stop();
        StopShake();
        RunningPanel.Visibility = Visibility.Collapsed;
        SetupPanel.Visibility   = Visibility.Visible;
        EyesControl.SetMood(Mood.Normal);
        EyesControl.SetRedEyes(false);
        _lastMood  = Mood.Normal;
        _lastPhase = DistractionPhase.None;
    }

    private void BtnMinimize_Click(object sender, RoutedEventArgs e) => WindowState = WindowState.Minimized;
    private void BtnClose_Click(object sender, RoutedEventArgs e)    => Application.Current.Shutdown();
    private void Window_MouseLeftButtonDown(object sender, MouseButtonEventArgs e) => DragMove();

    // ─── Helpers ──────────────────────────────────────────────────────────────
    private void StopShake()
    {
        _shakeTimer.Stop();
        TxtTimer.RenderTransform = null;
        _shakeOn = false;
    }

    private int GetSelectedMinutes()
    {
        foreach (var rb in new[] { Rb00, Rb15, Rb25, Rb45, Rb60 })
            if (rb.IsChecked == true && int.TryParse(rb.Tag?.ToString(), out int m)) return m;
        return 0;
    }

    private static string FindFile(string file, params string[] dirs)
    {
        foreach (var d in dirs)
        {
            var path = Path.Combine(d, file);
            if (File.Exists(path)) return path;
        }
        return file;
    }

    private void Cleanup()
    {
        _focusLoop.Stop();
        StopShake();
        _camera.Dispose();
        _detect?.Dispose();
        _sound.Dispose();
    }
}
