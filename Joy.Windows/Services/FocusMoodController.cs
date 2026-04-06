// FocusMoodController.cs — Joy Windows — MASTER VERSION
//
// Distraction escalation:
//   focused          → Happy (immediate) → Normal (after 2s)
//   0–3s distracted  → Curious  [SEARCHING]
//   3–6s distracted  → Angry    [ANGRY]
//   6–9s distracted  → Angry    [CRITICAL] + red eyes
//   9s+  distracted  → Tired    [RESET] + timer reset (once per event)
//                    → Curious  (stays here until user returns)
//
// On return:         → Happy    (instant reward)
//                    → Normal   (after 2s of continuous focus)
//
// Post-reset:        → Tired    (3s recovery period)
//                    → Curious  (waiting for user to return)

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

    // ─── Thresholds ───────────────────────────────────────────────────────────
    private static readonly TimeSpan SearchDur   = TimeSpan.FromSeconds(3);
    private static readonly TimeSpan AngryDur    = TimeSpan.FromSeconds(6);
    private static readonly TimeSpan CriticalDur = TimeSpan.FromSeconds(9);
    private static readonly TimeSpan TiredDur    = TimeSpan.FromSeconds(3);
    private static readonly TimeSpan NormalDur   = TimeSpan.FromSeconds(2);

    // ─── Internal state ───────────────────────────────────────────────────────
    private DateTime? _distractedAt;
    private DateTime? _focusedAt;
    private DateTime? _tiredUntil;
    private bool      _resetFired;
    private bool      _wasDistracted;

    // ─── Update — call at 10 Hz ───────────────────────────────────────────────
    public void Update(bool isFocused)
    {
        var now = DateTime.Now;

        if (isFocused)
        {
            _distractedAt = null;
            _resetFired   = false;
            _focusedAt  ??= now;

            // ── Post-reset tired recovery ──────────────────────────────────
            if (_tiredUntil.HasValue && now < _tiredUntil)
            {
                Set(Mood.Tired, DistractionPhase.Reset);
                return;
            }
            _tiredUntil = null;

            // ── User just came back ────────────────────────────────────────
            if (_wasDistracted)
            {
                _wasDistracted = false;
                Set(Mood.Happy, DistractionPhase.None);
                OnUserReturned?.Invoke();
                return;
            }

            // ── Settled focus: happy → normal after 2s ─────────────────────
            var focusedFor = now - (_focusedAt ?? now);
            Set(
                focusedFor >= NormalDur ? Mood.Normal : Mood.Happy,
                DistractionPhase.None
            );
        }
        else
        {
            // ── Distracted path ────────────────────────────────────────────
            _focusedAt     = null;
            _wasDistracted = true;
            _distractedAt ??= now;

            // Post-reset tired period — show tired even while distracted
            if (_tiredUntil.HasValue && now < _tiredUntil)
            {
                Set(Mood.Tired, DistractionPhase.Reset);
                return;
            }
            if (_tiredUntil.HasValue)
            {
                _tiredUntil = null;
                // After tired period ends, look curious waiting for user
                Set(Mood.Curious, DistractionPhase.None);
                return;
            }

            // No timer = just look curious, no escalation
            if (!IsTimerActive)
            {
                Set(Mood.Curious, DistractionPhase.None);
                return;
            }

            var elapsed = now - _distractedAt!.Value;

            if (elapsed < SearchDur)
            {
                // 0–3s: just looking around
                Set(Mood.Curious, DistractionPhase.Searching);
            }
            else if (elapsed < AngryDur)
            {
                // 3–6s: getting annoyed
                Set(Mood.Angry, DistractionPhase.Angry);
            }
            else if (elapsed < CriticalDur)
            {
                // 6–9s: critical — angry + red eyes
                Set(Mood.Angry, DistractionPhase.Critical);
            }
            else
            {
                // 9s+: timer reset (once per distraction event)
                if (!_resetFired)
                {
                    _resetFired    = true;
                    _distractedAt  = null; // fresh start after reset
                    _tiredUntil    = now + TiredDur;
                    Set(Mood.Tired, DistractionPhase.Reset);
                    OnTimerReset?.Invoke();
                }
                else
                {
                    // Already reset — just wait curiously
                    Set(Mood.Curious, DistractionPhase.None);
                }
            }
        }
    }

    private void Set(Mood mood, DistractionPhase phase)
    {
        CurrentMood  = mood;
        CurrentPhase = phase;
    }
}
