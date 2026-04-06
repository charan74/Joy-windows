// TimerState.cs
// Joy — Windows
//
// Observable countdown / stopwatch model.
// The timer is frozen externally by FocusMoodController while distracted.

using CommunityToolkit.Mvvm.ComponentModel;
using System.Windows.Threading;

namespace Joy.Windows.Models;

/// <summary>Manages the focus session timer and associated display state.</summary>
public partial class TimerState : ObservableObject
{
    // ─── Timer mode ──────────────────────────────────────────────────────────

    public enum TimerMode { Idle, Running, Completed }

    [ObservableProperty] private TimerMode _mode = TimerMode.Idle;
    [ObservableProperty] private int _totalSeconds;
    [ObservableProperty] private int _remainingSeconds;
    [ObservableProperty] private Mood _mood = Mood.Normal;
    [ObservableProperty] private bool _isFocused;
    [ObservableProperty] private bool _showSetup = true;
    [ObservableProperty] private bool _cameraAuthorized;
    [ObservableProperty] private DistractionPhase _distractionPhase = DistractionPhase.None;
    [ObservableProperty] private bool _frozen;

    /// <summary>True while a session is actively counting.</summary>
    public bool IsRunning => Mode == TimerMode.Running;

    /// <summary>Formatted MM:SS display string.</summary>
    public string FormattedTime =>
        $"{RemainingSeconds / 60:D2}:{RemainingSeconds % 60:D2}";

    /// <summary>Label shown in the timer area (may be "DISTRACTED").</summary>
    public string DisplayText => DistractionPhase is DistractionPhase.Angry or DistractionPhase.Critical
        ? "DISTRACTED"
        : FormattedTime;

    /// <summary>Progress 0..1 for a countdown session; 0 for stopwatch.</summary>
    public double Progress => TotalSeconds > 0
        ? (double)(TotalSeconds - RemainingSeconds) / TotalSeconds
        : 0.0;

    public Action? OnComplete;

    // ─── Ticking ─────────────────────────────────────────────────────────────

    private DispatcherTimer? _ticker;

    /// <summary>Start a countdown session.</summary>
    public void Start(int minutes)
    {
        TotalSeconds     = minutes * 60;
        RemainingSeconds = TotalSeconds;
        Mode             = TimerMode.Running;
        ShowSetup        = false;
        Frozen           = false;
        DistractionPhase = DistractionPhase.None;
        StartTicking();
    }

    /// <summary>Start a free-running stopwatch (no time limit).</summary>
    public void StartNoTimer()
    {
        TotalSeconds     = 0;
        RemainingSeconds = 0;
        Mode             = TimerMode.Running;
        ShowSetup        = false;
        Frozen           = false;
        DistractionPhase = DistractionPhase.None;
        StartTicking();
    }

    /// <summary>Stop the session and return to the setup screen.</summary>
    public void Stop()
    {
        _ticker?.Stop();
        _ticker           = null;
        Mode              = TimerMode.Idle;
        RemainingSeconds  = 0;
        TotalSeconds      = 0;
        Frozen            = false;
        DistractionPhase  = DistractionPhase.None;
        ShowSetup         = true;
    }

    /// <summary>Reset remaining time back to total (called after distraction timeout).</summary>
    public void ResetTimer()
    {
        RemainingSeconds = TotalSeconds > 0 ? TotalSeconds : 0;
        Frozen           = false;
        DistractionPhase = DistractionPhase.None;
    }

    private void StartTicking()
    {
        _ticker?.Stop();
        _ticker = new DispatcherTimer { Interval = TimeSpan.FromSeconds(1) };
        _ticker.Tick += (_, _) => Tick();
        _ticker.Start();
    }

    private void Tick()
    {
        if (Frozen) return;

        if (TotalSeconds > 0)
        {
            if (RemainingSeconds > 0) RemainingSeconds--;
            if (RemainingSeconds == 0) Complete();
        }
        else
        {
            RemainingSeconds++;
        }

        // Notify derived display properties
        OnPropertyChanged(nameof(FormattedTime));
        OnPropertyChanged(nameof(DisplayText));
        OnPropertyChanged(nameof(Progress));
    }

    private void Complete()
    {
        _ticker?.Stop();
        _ticker          = null;
        Mode             = TimerMode.Completed;
        Mood             = Mood.Happy;
        Frozen           = false;
        DistractionPhase = DistractionPhase.None;
        OnComplete?.Invoke();

        // Return to idle after 5 s
        var returnTimer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(5) };
        returnTimer.Tick += (_, _) =>
        {
            returnTimer.Stop();
            Mood      = Mood.Normal;
            Mode      = TimerMode.Idle;
            ShowSetup = true;
        };
        returnTimer.Start();
    }
}
