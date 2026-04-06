// RobotEyesControl.cs — Joy Windows
//
// FIXED: All 7 moods now properly rendered and animated.
// Moods: Normal, Tired, Angry, Happy, Frozen, Scary, Curious
// Each mood has distinct eyelid shapes, flicker, red eyes.

using Joy.Windows.Models;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Threading;

namespace Joy.Windows.Controls;

public sealed class RobotEyesControl : FrameworkElement
{
    // ─── Canvas constants (128×64 virtual space) ─────────────────────────────
    private const double CW  = 128, CH  = 64;
    private const double EW  = 38,  DEH = 34;
    private const double ESP = 10,  ER  = 8;
    private const double CB  = 8;   // curious boost

    private double CX => CW - EW * 2 - ESP;
    private double CY => CH - DEH;

    // ─── Dependency properties ────────────────────────────────────────────────
    public static readonly DependencyProperty CurrentMoodProperty =
        DependencyProperty.Register(nameof(CurrentMood), typeof(Mood), typeof(RobotEyesControl),
            new FrameworkPropertyMetadata(Mood.Normal, FrameworkPropertyMetadataOptions.AffectsRender,
                (d, _) => ((RobotEyesControl)d).OnMoodChanged()));

    public Mood CurrentMood
    {
        get => (Mood)GetValue(CurrentMoodProperty);
        set => SetValue(CurrentMoodProperty, value);
    }

    // ─── Animation state ──────────────────────────────────────────────────────
    private double _ex, _ey, _lh = DEH, _rh = DEH;
    private double _tx, _ty, _tlh = DEH, _trh = DEH;
    private double _tiredH, _angryH, _happyOff;
    private double _tiredHN, _angryHN, _happyOffN;
    private double _fx, _fy;

    private bool _tired, _angry, _happy, _curious;
    private bool _showRed;
    private bool _hFlick, _vFlick, _hAlt, _vAlt;
    private double _hAmp = 2, _vAmp = 2;
    private bool _angryBurst;
    private DateTime _nextBurst = DateTime.MaxValue;
    private DateTime _burstEnd  = DateTime.MinValue;

    // Boot
    private bool   _booting = true;
    private int    _bootF;
    private static readonly string[] Braille = ["⠋","⠙","⠹","⠸","⠼","⠴","⠦","⠧","⠇","⠏"];

    // Blink
    private bool   _blinking;
    private DispatcherTimer? _blinkTimer;

    // Timers
    private DispatcherTimer? _displayLink;
    private DispatcherTimer? _bootTimer;
    private DispatcherTimer? _idleTimer;

    private static readonly SolidColorBrush WB  = Brushes.White;
    private static readonly SolidColorBrush BB  = Brushes.Black;
    private static readonly SolidColorBrush RB  = new(Color.FromRgb(255, 80, 50));
    private static readonly Typeface        TF  = new("Courier New");

    // ─── Init ─────────────────────────────────────────────────────────────────
    public RobotEyesControl()
    {
        _tx = CX / 2; _ty = CY / 2; _ex = _tx; _ey = _ty;
        Loaded   += (_, _) => StartAll();
        Unloaded += (_, _) => StopAll();
    }

    private void StartAll()
    {
        // Boot spinner — 12 frames × 80ms
        _bootTimer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(80) };
        _bootTimer.Tick += (_, _) =>
        {
            _bootF++;
            if (_bootF >= 12) { _bootTimer.Stop(); _booting = false; StartIdle(); StartBlink(); }
            InvalidateVisual();
        };
        _bootTimer.Start();

        // 15 FPS display link
        _displayLink = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(67) };
        _displayLink.Tick += (_, _) => { Tick(); InvalidateVisual(); };
        _displayLink.Start();
    }

    private void StopAll()
    {
        _bootTimer?.Stop(); _displayLink?.Stop();
        _blinkTimer?.Stop(); _idleTimer?.Stop();
    }

    // ─── Animation tick ───────────────────────────────────────────────────────
    private void Tick()
    {
        // Integer-averaged lerp (matches web/mac reference)
        _ex  = Math.Floor((_ex  + _tx)  / 2);
        _ey  = Math.Floor((_ey  + _ty)  / 2);
        _lh  = Math.Floor((_lh  + _tlh) / 2);
        _rh  = Math.Floor((_rh  + _trh) / 2);

        // Curious: outer eye grows when looking far sideways
        if (_curious)
        {
            if      (_tx <= 10)       { _tlh = DEH + CB; _trh = DEH; }
            else if (_tx >= CX - 10)  { _trh = DEH + CB; _tlh = DEH; }
            else                      { _tlh = DEH;      _trh = DEH; }
        }

        // Eyelid targets
        _tiredHN   = _tired ? Math.Floor(_lh / 2) : 0;
        _angryHN   = _angry ? Math.Floor(_lh / 2) : 0;
        _happyOffN = _happy ? Math.Floor(_lh / 2) : 0;
        if (_tired) _angryHN = 0;
        if (_angry) _tiredHN = 0;

        // Smooth eyelids
        _tiredH   = Math.Floor((_tiredH   + _tiredHN)   / 2);
        _angryH   = Math.Floor((_angryH   + _angryHN)   / 2);
        _happyOff = Math.Floor((_happyOff + _happyOffN) / 2);

        // Angry burst flicker
        if (_angryBurst)
        {
            var now = DateTime.Now;
            if (now >= _nextBurst) { _burstEnd = now.AddSeconds(.25); _nextBurst = now.AddSeconds(.6 + Random.Shared.NextDouble() * .3); }
            _hFlick = now < _burstEnd;
        }

        // Apply flicker
        _fx = _hFlick ? (_hAlt ? _hAmp : -_hAmp) : 0; if (_hFlick) _hAlt = !_hAlt;
        _fy = _vFlick ? (_vAlt ? _vAmp : -_vAmp) : 0; if (_vFlick) _vAlt = !_vAlt;
    }

    // ─── Render ───────────────────────────────────────────────────────────────
    protected override void OnRender(DrawingContext dc)
    {
        double w = ActualWidth, h = ActualHeight;
        if (w <= 0 || h <= 0) return;
        double sx = w / CW, sy = h / CH;

        dc.DrawRectangle(BB, null, new Rect(0, 0, w, h));

        // Boot spinner
        if (_booting)
        {
            var ft = new FormattedText(Braille[_bootF % Braille.Length],
                System.Globalization.CultureInfo.InvariantCulture,
                FlowDirection.LeftToRight, TF, h * 0.45, WB, 1.0);
            dc.DrawText(ft, new Point((w - ft.Width) / 2, (h - ft.Height) / 2));
            return;
        }

        double ew  = EW  * sx, sp = ESP * sx, r = ER * sx;
        double px  = (_ex + _fx) * sx, py = (_ey + _fy) * sy;
        double lh  = _lh * sy,         rh = _rh * sy;

        var lr = new Rect(px,        py, ew, lh);
        var rr = new Rect(px+ew+sp,  py, ew, rh);

        var eyeBrush = _showRed ? RB : WB;
        RRect(dc, lr, r, eyeBrush);
        RRect(dc, rr, r, eyeBrush);

        // ── Tired eyelids (droop outward) ──────────────────────────────────
        double th = _tiredH * sy;
        if (th > 0.5)
        {
            Tri(dc, lr.TopLeft, lr.TopRight, new Point(lr.Left,  lr.Top + th));
            Tri(dc, rr.TopLeft, rr.TopRight, new Point(rr.Right, rr.Top + th));
        }

        // ── Angry eyelids (furrow inward) ──────────────────────────────────
        double ah = _angryH * sy;
        if (ah > 0.5)
        {
            Tri(dc, lr.TopLeft, lr.TopRight, new Point(lr.Right, lr.Top + ah));
            Tri(dc, rr.TopLeft, rr.TopRight, new Point(rr.Left,  rr.Top + ah));
        }

        // ── Happy eyelids (bottom cover) ───────────────────────────────────
        double hof = _happyOff * sy;
        if (hof > 0.5)
        {
            RRect(dc, new Rect(lr.Left-1, lr.Bottom-hof+1, lr.Width+2, hof+4), r, BB);
            RRect(dc, new Rect(rr.Left-1, rr.Bottom-hof+1, rr.Width+2, hof+4), r, BB);
        }

        // ── Scanlines ──────────────────────────────────────────────────────
        var scanBrush = new SolidColorBrush(Color.FromArgb(28, 0, 0, 0));
        for (double y = 0; y < h; y += 3)
            dc.DrawRectangle(scanBrush, null, new Rect(0, y, w, 1));
    }

    private static void RRect(DrawingContext dc, Rect r, double rad, Brush b)
    {
        if (r.Width <= 0 || r.Height <= 0) return;
        dc.DrawRoundedRectangle(b, null, r, rad, rad);
    }

    private static void Tri(DrawingContext dc, Point a, Point b, Point c)
    {
        var g = new StreamGeometry();
        using (var ctx = g.Open()) { ctx.BeginFigure(a, true, true); ctx.LineTo(b, false, false); ctx.LineTo(c, false, false); }
        g.Freeze();
        dc.DrawGeometry(BB, null, g);
    }

    // ─── Idle look & blink ────────────────────────────────────────────────────
    private void StartIdle()
    {
        void Schedule()
        {
            _idleTimer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(1 + Random.Shared.NextDouble() * 2) };
            _idleTimer.Tick += (_, _) => { _idleTimer.Stop(); if (!_blinking) { _tx = Random.Shared.NextDouble() * CX; _ty = Random.Shared.NextDouble() * CY; } Schedule(); };
            _idleTimer.Start();
        }
        Schedule();
    }

    private void StartBlink()
    {
        void Schedule()
        {
            _blinkTimer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(3 + Random.Shared.NextDouble() * 6) };
            _blinkTimer.Tick += (_, _) =>
            {
                _blinkTimer.Stop();
                if (!_blinking) { _blinking = true; _tlh = 1; _trh = 1; var rt = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(150) }; rt.Tick += (_, _) => { rt.Stop(); _tlh = DEH; _trh = DEH; _blinking = false; }; rt.Start(); }
                Schedule();
            };
            _blinkTimer.Start();
        }
        Schedule();
    }

    // ─── Public mood API ──────────────────────────────────────────────────────
    private void OnMoodChanged() => SetMood(CurrentMood);

    // ─── Gaze target (set from iris tracker) ──────────────────────────────────
    /// <summary>
    /// Feed the normalized gaze point (0..1 each axis) from iris detection.
    /// The robot eyes will smoothly follow this point in real time.
    /// Called at ~10 Hz from the focus loop.
    /// </summary>
    public void SetGazeTarget(double normX, double normY)
    {
        // Map normalized iris position (0..1) to eye canvas constraint space
        // Invert X because camera is mirrored
        _tx = (1.0 - normX) * CX;
        _ty = normY * CY;
    }

        public void SetMood(Mood mood)
    {
        // Clear previous mood flags
        bool wasFlickery = CurrentMood is Mood.Scary or Mood.Frozen or Mood.Angry;
        bool willFlickery = mood is Mood.Scary or Mood.Frozen or Mood.Angry;
        if (wasFlickery && !willFlickery) { _hFlick = false; _vFlick = false; _angryBurst = false; }
        if (_curious && mood != Mood.Curious) { _curious = false; _tlh = DEH; _trh = DEH; }

        _tired = false; _angry = false; _happy = false; _showRed = false;

        switch (mood)
        {
            case Mood.Tired:
                _tired = true;
                break;

            case Mood.Angry:
                _angry      = true;
                _angryBurst = true;
                _hAmp       = 2;
                _vFlick     = false;
                // Red eyes controlled externally by SetRedEyes()
                break;

            case Mood.Happy:
                _happy = true;
                break;

            case Mood.Frozen:
                _hFlick = true; _hAmp = 2; _vFlick = false;
                break;

            case Mood.Scary:
                _tired  = true;
                _vFlick = true; _vAmp = 2; _hFlick = false;
                _showRed = true;
                break;

            case Mood.Curious:
                _curious = true;
                break;

            case Mood.Normal:
            default:
                break;
        }

        if (!_curious) { _tlh = DEH; _trh = DEH; }
        CurrentMood = mood;
        InvalidateVisual();
    }

    public void SetRedEyes(bool on) { _showRed = on; InvalidateVisual(); }
}
