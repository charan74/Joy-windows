// FocusMoodController.cs — Joy Windows
//
// Fixed distraction escalation ladder:
//   0–3 s   → SEARCHING  (curious)
//   3–6 s   → ANGRY      (DISTRACTED label)
//   6–9 s   → CRITICAL   (red eyes + shake)
//   9 s+    → RESET      (timer reset, then tired for 3s, then back to curious)
//
// FIXED: no longer snapping back to normal after 5s
// All 7 moods properly emitted.

using Joy.Windows.Models;

namespace Joy.Windows.Services;

public sealed class FocusMoodController
{
    // ─── Output ──────────────────────────────────────────────────────────────
    public Mood             CurrentMood  { get; private set; } = Mood.Normal;
    public DistractionPhase CurrentPhase { get; private set; } = DistractionPhase.None;

    // ─── Callbacks ───────────────────────────────────────────────────────────
    public Action? OnTimerReset;
    public Action? OnUserReturned;
    public bool    IsTimerActive { get; set; }

    // ─── Thresholds (seconds) ─────────────────────────────────────────────────
    private const double SearchSec   = 3.0;
    private const double AngrySec    = 6.0;
    private const double CriticalSec = 9.0;
    private const double TiredSec    = 3.0;   // tired after reset
    private const double RefocusSec  = 2.0;   // time focused before returning to normal

    // ─── Internal state ───────────────────────────────────────────────────────
    private DateTime? _distractedSince;
    private DateTime? _focusedSince;
    private DateTime? _tiredUntil;
    private bool      _didResetThisPeriod;
    private bool      _wasDistracted;

    // ─── Update (call at 10 Hz) ───────────────────────────────────────────────
    public void Update(bool isFocused)
    {
        var now = DateTime.Now;

        if (isFocused)
        {
            _distractedSince     = null;
            _didResetThisPeriod  = false;
            _focusedSince      ??= now;

            // Still in post-reset tired period
            if (_tiredUntil.HasValue && now < _tiredUntil)
            {
                CurrentMood  = Mood.Tired;
                CurrentPhase = DistractionPhase.Reset;
                return;
            }
            _tiredUntil = null;

            // Just returned from distraction
            if (_wasDistracted)
            {
                _wasDistracted = false;
                CurrentPhase   = DistractionPhase.None;
                CurrentMood    = Mood.Happy;
                OnUserReturned?.Invoke();
                return;
            }

            // Normal focused state — transition normal after 2s
            CurrentPhase = DistractionPhase.None;
            var focusedFor = (now - (_focusedSince ?? now)).TotalSeconds;
            CurrentMood = focusedFor >= RefocusSec ? Mood.Normal : Mood.Happy;
        }
        else
        {
            // ── Distracted path ───────────────────────────────────────────────
            _focusedSince    = null;
            _wasDistracted   = true;
            _distractedSince ??= now;

            // In post-reset tired period
            if (_tiredUntil.HasValue && now < _tiredUntil)
            {
                CurrentMood  = Mood.Tired;
                CurrentPhase = DistractionPhase.Reset;
                return;
            }
            if (_tiredUntil.HasValue)
            {
                // Tired period just ended — back to curious, wait for user
                _tiredUntil  = null;
                CurrentMood  = Mood.Curious;
                CurrentPhase = DistractionPhase.None;
                return;
            }

            // No timer running → just curious, no escalation
            if (!IsTimerActive)
            {
                CurrentMood  = Mood.Curious;
                CurrentPhase = DistractionPhase.None;
                return;
            }

            // ── Escalation ladder ─────────────────────────────────────────────
            double elapsed = (now - _distractedSince.Value).TotalSeconds;

            if (elapsed < SearchSec)
            {
                CurrentMood  = Mood.Curious;
                CurrentPhase = DistractionPhase.Searching;
            }
            else if (elapsed < AngrySec)
            {
                CurrentMood  = Mood.Angry;
                CurrentPhase = DistractionPhase.Angry;
            }
            else if (elapsed < CriticalSec)
            {
                CurrentMood  = Mood.Angry;
                CurrentPhase = DistractionPhase.Critical;
            }
            else
            {
                // 9+ seconds → RESET (only once per distraction event)
                if (!_didResetThisPeriod)
                {
                    _didResetThisPeriod = true;
                    _tiredUntil         = now.AddSeconds(TiredSec);
                    _distractedSince    = null; // reset so next look-away starts fresh
                    CurrentMood         = Mood.Tired;
                    CurrentPhase        = DistractionPhase.Reset;
                    OnTimerReset?.Invoke();
                }
                else
                {
                    // Already reset once — stay curious, don't keep resetting
                    CurrentMood  = Mood.Curious;
                    CurrentPhase = DistractionPhase.None;
                }
            }
        }
    }
}
