// RobotEyesControl.cs — Joy Windows
// MASTER VERSION — All 7 emotions fully rendered and animated.
//
// Emotions:
//   Normal  — open eyes, idle look, random blink
//   Happy   — bottom half of eyes covered (squinting upward)
//   Curious — one eye grows taller when looking sideways
//   Angry   — furrowed brow (inward triangles) + pulsing horizontal flicker
//   Tired   — drooping eyelids (outward triangles)
//   Frozen  — continuous fast horizontal flicker
//   Scary   — vertical flicker + red eyes (used for critical distraction)
//
// Animation engine: 15 FPS integer-lerp (matches web reference exactly)

using Joy.Windows.Models;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Threading;

namespace Joy.Windows.Controls;

public sealed class RobotEyesControl : FrameworkElement
{
    // ─── Virtual canvas (128×64 — matches web/macOS exactly) ─────────────────
    private const double CW  = 128, CH  = 64;
    private const double EW  = 38,  DEH = 34;
    private const double ESP = 10,  ER  = 8;
    private const double CURIOUS_BOOST = 10;

    private double ConstraintX => CW - EW * 2 - ESP;
    private double ConstraintY => CH - DEH;

    // ─── Dependency property ──────────────────────────────────────────────────
    public static readonly DependencyProperty CurrentMoodProperty =
        DependencyProperty.Register(nameof(CurrentMood), typeof(Mood), typeof(RobotEyesControl),
            new FrameworkPropertyMetadata(Mood.Normal,
                FrameworkPropertyMetadataOptions.AffectsRender,
                (d, e) => ((RobotEyesControl)d).ApplyMood((Mood)e.NewValue)));

    public Mood CurrentMood
    {
        get => (Mood)GetValue(CurrentMoodProperty);
        set => SetValue(CurrentMoodProperty, value);
    }

    // ─── Animation state ──────────────────────────────────────────────────────

    // Eye positions (canvas space)
    private double _eyeX, _eyeY;
    private double _targetX, _targetY;

    // Eye heights (per eye, for curious asymmetry)
    private double _leftH  = DEH, _rightH  = DEH;
    private double _tLeftH = DEH, _tRightH = DEH;

    // Eyelid amounts (lerped each frame)
    private double _tiredH,   _tiredHTarget;
    private double _angryH,   _angryHTarget;
    private double _happyOff, _happyOffTarget;

    // Flicker
    private double _flickX, _flickY;
    private bool   _hAlt, _vAlt;

    // Mood flags
    private bool _isTired, _isAngry, _isHappy, _isCurious;
    private bool _hFlicker, _vFlicker, _angryBurst;
    private double _hAmp = 2.5, _vAmp = 2.5;

    // Angry burst timing
    private DateTime _nextBurst  = DateTime.MaxValue;
    private DateTime _burstEnd   = DateTime.MinValue;

    // Red eyes (set externally for critical phase)
    private bool _redEyes;

    // Boot animation
    private bool   _bootDone;
    private int    _bootFrame;
    private static readonly string[] Braille = ["⠋","⠙","⠹","⠸","⠼","⠴","⠦","⠧","⠇","⠏"];

    // Blink
    private bool _isBlinking;

    // Timers
    private DispatcherTimer? _loopTimer;
    private DispatcherTimer? _bootTimer;
    private DispatcherTimer? _blinkTimer;
    private DispatcherTimer? _idleTimer;

    // Brushes (frozen = only create once)
    private static readonly SolidColorBrush BrushBlack = new(Colors.Black);
    private static readonly SolidColorBrush BrushWhite = new(Colors.White);
    private static readonly SolidColorBrush BrushRed   = new(Color.FromRgb(255, 70, 40));
    private static readonly SolidColorBrush BrushScan  = new(Color.FromArgb(22, 0, 0, 0));
    private static readonly Typeface        BootFont    = new("Courier New");

    static RobotEyesControl()
    {
        BrushBlack.Freeze(); BrushWhite.Freeze();
        BrushRed.Freeze();   BrushScan.Freeze();
    }

    // ─── Lifecycle ────────────────────────────────────────────────────────────
    public RobotEyesControl()
    {
        _targetX = ConstraintX / 2;
        _targetY = ConstraintY / 2;
        _eyeX    = _targetX;
        _eyeY    = _targetY;

        Loaded   += (_, _) => Boot();
        Unloaded += (_, _) => Teardown();
    }

    private void Boot()
    {
        // Boot spinner — 12 frames at 80ms each (~1 second)
        _bootTimer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(80) };
        _bootTimer.Tick += (_, _) =>
        {
            _bootFrame++;
            if (_bootFrame >= 12)
            {
                _bootTimer!.Stop();
                _bootDone = true;
                StartIdleLook();
                StartAutoBlink();
            }
            InvalidateVisual();
        };
        _bootTimer.Start();

        // Main animation loop — 15 FPS
        _loopTimer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(67) };
        _loopTimer.Tick += (_, _) => { AnimTick(); InvalidateVisual(); };
        _loopTimer.Start();
    }

    private void Teardown()
    {
        _loopTimer?.Stop();
        _bootTimer?.Stop();
        _blinkTimer?.Stop();
        _idleTimer?.Stop();
    }

    // ─── Animation tick ───────────────────────────────────────────────────────
    private void AnimTick()
    {
        if (!_bootDone) return;

        // Integer-averaged lerp — matches web reference exactly
        _eyeX   = Math.Floor((_eyeX   + _targetX)  / 2);
        _eyeY   = Math.Floor((_eyeY   + _targetY)   / 2);
        _leftH  = Math.Floor((_leftH  + _tLeftH)    / 2);
        _rightH = Math.Floor((_rightH + _tRightH)   / 2);

        // Curious: grow the outer eye when looking far sideways
        if (_isCurious)
        {
            if      (_targetX <= 8)                  { _tLeftH  = DEH + CURIOUS_BOOST; _tRightH = DEH; }
            else if (_targetX >= ConstraintX - 8)    { _tRightH = DEH + CURIOUS_BOOST; _tLeftH  = DEH; }
            else                                     { _tLeftH  = DEH; _tRightH = DEH; }
        }

        // Eyelid targets
        _tiredHTarget    = _isTired  ? Math.Floor(_leftH / 2) : 0;
        _angryHTarget    = _isAngry  ? Math.Floor(_leftH / 2) : 0;
        _happyOffTarget  = _isHappy  ? Math.Floor(_leftH / 2) : 0;
        if (_isTired) _angryHTarget   = 0;
        if (_isAngry) _tiredHTarget   = 0;

        // Smooth eyelid transitions
        _tiredH   = Math.Floor((_tiredH   + _tiredHTarget)   / 2);
        _angryH   = Math.Floor((_angryH   + _angryHTarget)   / 2);
        _happyOff = Math.Floor((_happyOff + _happyOffTarget) / 2);

        // Angry pulsing burst flicker
        if (_angryBurst)
        {
            var now = DateTime.Now;
            if (now >= _nextBurst)
            {
                _burstEnd  = now.AddSeconds(0.28);
                _nextBurst = now.AddSeconds(0.55 + Random.Shared.NextDouble() * 0.35);
            }
            _hFlicker = now < _burstEnd;
        }

        // Flicker offsets
        if (_hFlicker) { _flickX = _hAlt ? _hAmp : -_hAmp; _hAlt = !_hAlt; } else _flickX = 0;
        if (_vFlicker) { _flickY = _vAlt ? _vAmp : -_vAmp; _vAlt = !_vAlt; } else _flickY = 0;
    }

    // ─── Render ───────────────────────────────────────────────────────────────
    protected override void OnRender(DrawingContext dc)
    {
        double w = ActualWidth, h = ActualHeight;
        if (w <= 0 || h <= 0) return;

        double sx = w / CW, sy = h / CH;

        // Background — always black
        dc.DrawRectangle(BrushBlack, null, new Rect(0, 0, w, h));

        // Boot spinner
        if (!_bootDone)
        {
            var ft = new FormattedText(
                Braille[_bootFrame % Braille.Length],
                System.Globalization.CultureInfo.InvariantCulture,
                FlowDirection.LeftToRight,
                BootFont,
                h * 0.48,
                BrushWhite,
                VisualTreeHelper.GetDpi(this).PixelsPerDip
            );
            dc.DrawText(ft, new Point((w - ft.Width) / 2, (h - ft.Height) / 2));
            return;
        }

        // Eye geometry
        double ew  = EW  * sx;
        double sp  = ESP * sx;
        double rad = ER  * sx;
        double px  = (_eyeX + _flickX) * sx;
        double py  = (_eyeY + _flickY) * sy;
        double lh  = _leftH  * sy;
        double rh  = _rightH * sy;

        var lRect = new Rect(px,        py, ew, lh);
        var rRect = new Rect(px+ew+sp,  py, ew, rh);

        // Eye colour — white normally, red in critical phase
        var eyeBrush = _redEyes ? BrushRed : BrushWhite;

        // ── Draw eye whites ───────────────────────────────────────────────────
        DrawRounded(dc, lRect, rad, eyeBrush);
        DrawRounded(dc, rRect, rad, eyeBrush);

        // ── TIRED eyelids — triangles drooping OUTWARD ────────────────────────
        // Left: top-left → top-right → bottom-left (droop left)
        // Right: top-left → top-right → bottom-right (droop right)
        double th = _tiredH * sy;
        if (th > 0.8)
        {
            FillTri(dc,
                lRect.TopLeft, lRect.TopRight,
                new Point(lRect.Left, lRect.Top + th));
            FillTri(dc,
                rRect.TopLeft, rRect.TopRight,
                new Point(rRect.Right, rRect.Top + th));
        }

        // ── ANGRY eyelids — triangles angling INWARD (furrowed brow) ──────────
        // Left: top-left → top-right → bottom-right (slant inward/right)
        // Right: top-left → top-right → bottom-left (slant inward/left)
        double ah = _angryH * sy;
        if (ah > 0.8)
        {
            FillTri(dc,
                lRect.TopLeft, lRect.TopRight,
                new Point(lRect.Right, lRect.Top + ah));
            FillTri(dc,
                rRect.TopLeft, rRect.TopRight,
                new Point(rRect.Left,  rRect.Top + ah));
        }

        // ── HAPPY eyelids — rounded rect covering bottom half ─────────────────
        double hof = _happyOff * sy;
        if (hof > 0.8)
        {
            DrawRounded(dc, new Rect(lRect.Left - 1, lRect.Bottom - hof + 1, lRect.Width + 2, hof + 5), rad, BrushBlack);
            DrawRounded(dc, new Rect(rRect.Left - 1, rRect.Bottom - hof + 1, rRect.Width + 2, hof + 5), rad, BrushBlack);
        }

        // ── CRT scanlines ─────────────────────────────────────────────────────
        for (double y = 0; y < h; y += 3)
            dc.DrawRectangle(BrushScan, null, new Rect(0, y, w, 1));
    }

    // ─── Drawing helpers ──────────────────────────────────────────────────────
    private static void DrawRounded(DrawingContext dc, Rect r, double rad, Brush b)
    {
        if (r.Width <= 0 || r.Height <= 0) return;
        double cr = Math.Min(rad, Math.Min(r.Width / 2, r.Height / 2));
        dc.DrawRoundedRectangle(b, null, r, cr, cr);
    }

    private static void FillTri(DrawingContext dc, Point a, Point b, Point c)
    {
        var geo = new StreamGeometry();
        using (var ctx = geo.Open())
        {
            ctx.BeginFigure(a, true, true);
            ctx.LineTo(b, false, false);
            ctx.LineTo(c, false, false);
        }
        geo.Freeze();
        dc.DrawGeometry(BrushBlack, null, geo);
    }

    // ─── Idle look ────────────────────────────────────────────────────────────
    private void StartIdleLook() => ScheduleNextLook();

    private void ScheduleNextLook()
    {
        double delay = 1.0 + Random.Shared.NextDouble() * 2.5;
        _idleTimer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(delay) };
        _idleTimer.Tick += (_, _) =>
        {
            _idleTimer!.Stop();
            if (!_isBlinking)
            {
                _targetX = Random.Shared.NextDouble() * ConstraintX;
                _targetY = Random.Shared.NextDouble() * ConstraintY;
            }
            ScheduleNextLook();
        };
        _idleTimer.Start();
    }

    // ─── Auto blink ───────────────────────────────────────────────────────────
    private void StartAutoBlink() => ScheduleNextBlink();

    private void ScheduleNextBlink()
    {
        double delay = 3.0 + Random.Shared.NextDouble() * 7.0;
        _blinkTimer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(delay) };
        _blinkTimer.Tick += (_, _) =>
        {
            _blinkTimer!.Stop();
            DoBlink();
            ScheduleNextBlink();
        };
        _blinkTimer.Start();
    }

    private void DoBlink()
    {
        if (_isBlinking) return;
        _isBlinking = true;
        _tLeftH     = 1;
        _tRightH    = 1;
        var restore = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(140) };
        restore.Tick += (_, _) =>
        {
            restore.Stop();
            _tLeftH     = DEH;
            _tRightH    = DEH;
            _isBlinking = false;
        };
        restore.Start();
    }

    // ─── Public API ───────────────────────────────────────────────────────────

    /// <summary>Set gaze target from iris tracker (0..1 normalized).</summary>
    public void SetGazeTarget(double normX, double normY)
    {
        // Mirror X (camera is flipped)
        _targetX = (1.0 - normX) * ConstraintX;
        _targetY = normY * ConstraintY;
    }

    /// <summary>Control red-eye tint independently (critical distraction phase).</summary>
    public void SetRedEyes(bool on)
    {
        _redEyes = on;
        InvalidateVisual();
    }

    /// <summary>Transition to a new mood, setting all eyelid/flicker states correctly.</summary>
    public void SetMood(Mood mood)
    {
        // Stop flicker from previous mood if leaving flickery moods
        bool wasFlickery = CurrentMood is Mood.Angry or Mood.Frozen or Mood.Scary;
        bool willFlickery = mood is Mood.Angry or Mood.Frozen or Mood.Scary;
        if (wasFlickery && !willFlickery)
        {
            _hFlicker    = false;
            _vFlicker    = false;
            _angryBurst  = false;
            _flickX      = 0;
            _flickY      = 0;
        }

        // Leave curious mode → reset eye heights
        if (_isCurious && mood != Mood.Curious)
        {
            _isCurious = false;
            _tLeftH    = DEH;
            _tRightH   = DEH;
        }

        // Clear all flags
        _isTired  = false;
        _isAngry  = false;
        _isHappy  = false;
        _isCurious = false;

        switch (mood)
        {
            // ── Normal ─────────────────────────────────────────────────────────
            case Mood.Normal:
                _redEyes = false;
                break;

            // ── Happy — bottom half of eyes covered ────────────────────────────
            case Mood.Happy:
                _isHappy = true;
                _redEyes = false;
                break;

            // ── Curious — outer eye taller when looking sideways ───────────────
            case Mood.Curious:
                _isCurious = true;
                _redEyes   = false;
                break;

            // ── Angry — furrowed brow + pulsing flicker ────────────────────────
            case Mood.Angry:
                _isAngry    = true;
                _angryBurst = true;
                _hAmp       = 2.5;
                _vFlicker   = false;
                // Red eyes controlled externally by SetRedEyes()
                // Schedule first burst immediately
                _nextBurst = DateTime.Now;
                break;

            // ── Tired — drooping eyelids outward ───────────────────────────────
            case Mood.Tired:
                _isTired = true;
                _redEyes  = false;
                break;

            // ── Frozen — rapid horizontal flicker ──────────────────────────────
            case Mood.Frozen:
                _hFlicker = true;
                _hAmp     = 3;
                _vFlicker = false;
                _redEyes  = false;
                break;

            // ── Scary — vertical flicker + red eyes ────────────────────────────
            case Mood.Scary:
                _isTired  = true;   // drooping lids + vertical shake = alarmed look
                _vFlicker = true;
                _vAmp     = 3;
                _hFlicker = false;
                _redEyes  = true;
                break;
        }

        // If not curious, reset eye heights to default
        if (!_isCurious)
        {
            _tLeftH  = DEH;
            _tRightH = DEH;
        }

        SetValue(CurrentMoodProperty, mood);
        InvalidateVisual();
    }

    // Override so setting CurrentMood directly also calls SetMood
    private void ApplyMood(Mood mood) => SetMood(mood);
}
