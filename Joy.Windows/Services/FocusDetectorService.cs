// FocusDetectorService.cs — Joy Windows
//
// TRUE EYE GAZE TRACKING:
//   1. Detect face → locate eye regions
//   2. For each eye, find the IRIS/PUPIL center using:
//      - Grayscale threshold → darkest blob = pupil
//      - Connected components to find iris center
//   3. Compare iris position relative to eye bounding box
//      → iris near CENTER = looking at screen = FOCUSED
//      → iris near EDGE   = looking away      = DISTRACTED
//   4. Temporal smoothing with hysteresis

using OpenCvSharp;
using System.IO;

namespace Joy.Windows.Services;

public sealed class FocusDetectorService : IDisposable
{
    // ── Public state ──────────────────────────────────────────────────────────
    public bool IsFocused { get; private set; } = true;

    /// Normalized gaze position: (0,0)=top-left, (1,1)=bottom-right, (.5,.5)=center
    public (double X, double Y) GazePoint { get; private set; } = (0.5, 0.5);

    // ── Cascades ──────────────────────────────────────────────────────────────
    private readonly CascadeClassifier _faceCascade;
    private readonly CascadeClassifier _eyeCascade;

    // ── Timing ────────────────────────────────────────────────────────────────
    private DateTime _lastRun = DateTime.MinValue;
    private static readonly TimeSpan Interval = TimeSpan.FromMilliseconds(80);

    // ── Smoothing ─────────────────────────────────────────────────────────────
    private readonly Queue<bool>   _focusHistory = new();
    private readonly Queue<double> _gazeX        = new();
    private readonly Queue<double> _gazeY        = new();
    private const int HistLen  = 8;
    private const int FocusMin = 5;  // need 5/8 to be focused
    private const int FocusMax = 3;  // drop below 3/8 to be distracted

    // ── Init ──────────────────────────────────────────────────────────────────
    public FocusDetectorService(string facePath, string eyePath)
    {
        _faceCascade = new CascadeClassifier();
        _eyeCascade  = new CascadeClassifier();
        if (File.Exists(facePath)) _faceCascade.Load(facePath);
        if (File.Exists(eyePath))  _eyeCascade.Load(eyePath);

        // Prefill as focused so app starts naturally
        for (int i = 0; i < HistLen; i++)
        {
            _focusHistory.Enqueue(true);
            _gazeX.Enqueue(0.5);
            _gazeY.Enqueue(0.5);
        }
    }

    // ── Frame entry point ────────────────────────────────────────────────────
    public void ProcessFrame(Mat frame)
    {
        if (DateTime.Now - _lastRun < Interval) { frame.Dispose(); return; }
        _lastRun = DateTime.Now;
        try   { Evaluate(frame); }
        finally { frame.Dispose(); }
    }

    // ── Core evaluation ───────────────────────────────────────────────────────
    private void Evaluate(Mat frame)
    {
        // Resize to fixed width for consistent speed
        using var small = new Mat();
        double scale = 320.0 / Math.Max(frame.Width, 1);
        Cv2.Resize(frame, small, new Size(320, (int)(frame.Height * scale)));

        using var gray = new Mat();
        Cv2.CvtColor(small, gray, ColorConversionCodes.BGR2GRAY);
        Cv2.EqualizeHist(gray, gray);

        int W = gray.Width, H = gray.Height;

        // ── Step 1: Find face ─────────────────────────────────────────────────
        var faces = _faceCascade.DetectMultiScale(
            gray, 1.1, 4, HaarDetectionTypes.ScaleImage,
            new Size(W / 7, H / 7), new Size(W, H));

        if (faces.Length == 0) { AddResult(false, 0.5, 0.5); return; }

        var face = faces.OrderByDescending(f => f.Width * f.Height).First();
        if (face.Width < W * 0.08) { AddResult(false, 0.5, 0.5); return; }

        // ── Step 2: Find eyes in upper half of face ───────────────────────────
        int eRoiY = face.Y;
        int eRoiH = (int)(face.Height * 0.52);
        int eRoiX = face.X;
        int eRoiW = face.Width;
        Clamp(ref eRoiX, ref eRoiY, ref eRoiW, ref eRoiH, W, H);
        if (eRoiW <= 0 || eRoiH <= 0) { AddResult(false, 0.5, 0.5); return; }

        using var eyeRoi = new Mat(gray, new Rect(eRoiX, eRoiY, eRoiW, eRoiH));
        int minE = Math.Max((int)(face.Width * 0.13), 12);
        int maxE = (int)(face.Width * 0.42);

        var eyes = _eyeCascade.DetectMultiScale(
            eyeRoi, 1.05, 5,
            minSize: new Size(minE, minE),
            maxSize: new Size(maxE, maxE));

        if (eyes.Length == 0) { AddResult(false, 0.5, 0.5); return; }

        // ── Step 3: For each detected eye, find iris center ───────────────────
        var gazePoints = new List<(double gx, double gy)>();

        var sortedEyes = eyes.OrderBy(e => e.X).ToArray();

        foreach (var eye in sortedEyes.Take(2))
        {
            // Eye rect in full-gray coords
            int ex = eRoiX + eye.X, ey = eRoiY + eye.Y;
            int ew = eye.Width,     eh = eye.Height;
            Clamp(ref ex, ref ey, ref ew, ref eh, W, H);
            if (ew < 8 || eh < 6) continue;

            using var eyeMat = new Mat(gray, new Rect(ex, ey, ew, eh));

            // ── Iris detection via darkest-region method ──────────────────────
            // 1. Blur to reduce noise
            using var blurred = new Mat();
            Cv2.GaussianBlur(eyeMat, blurred, new Size(7, 7), 0);

            // 2. Find the darkest 15% of pixels = iris/pupil region
            Cv2.MinMaxLoc(blurred, out double minVal, out double maxVal, out _, out _);
            double threshold = minVal + (maxVal - minVal) * 0.25;

            using var darkMask = new Mat();
            Cv2.Threshold(blurred, darkMask, threshold, 255, ThresholdTypes.BinaryInv);

            // 3. Morphological close to fill gaps in iris
            using var kernel = Cv2.GetStructuringElement(MorphShapes.Ellipse, new Size(5, 5));
            using var closed = new Mat();
            Cv2.MorphologyEx(darkMask, closed, MorphTypes.Close, kernel);

            // 4. Find contours of dark regions
            Cv2.FindContours(closed, out var contours, out _,
                RetrievalModes.External, ContourApproximationModes.ApproxSimple);

            if (contours.Length == 0) continue;

            // 5. Pick the largest contour = iris/pupil
            var biggest = contours.OrderByDescending(c => Cv2.ContourArea(c)).First();
            var moments = Cv2.Moments(biggest);
            if (moments.M00 < 1) continue;

            // 6. Iris center in eye-local coordinates
            double irisCx = moments.M10 / moments.M00;
            double irisCy = moments.M01 / moments.M00;

            // 7. Normalize to 0..1 within the eye bounding box
            // 0 = far left/top, 1 = far right/bottom, 0.5 = center
            double normX = irisCx / Math.Max(ew, 1);
            double normY = irisCy / Math.Max(eh, 1);

            // 8. Convert to gaze in face space
            // The eye's position within the face tells us left vs right eye
            double faceRelX = (ex - face.X + irisCx) / Math.Max(face.Width,  1);
            double faceRelY = (ey - face.Y + irisCy) / Math.Max(face.Height, 1);

            gazePoints.Add((normX, normY));
        }

        if (gazePoints.Count == 0) { AddResult(false, 0.5, 0.5); return; }

        // Average gaze across both eyes
        double avgGX = gazePoints.Average(p => p.gx);
        double avgGY = gazePoints.Average(p => p.gy);

        // ── Step 4: Determine focus from iris position ─────────────────────────
        // Iris center should be in the middle 40% of the eye horizontally
        // and middle 50% vertically when looking straight at screen.
        // Outside this zone = looking away.
        bool gazeOnScreen =
            avgGX >= 0.28 && avgGX <= 0.72 &&   // horizontal center zone
            avgGY >= 0.20 && avgGY <= 0.75;      // vertical center zone (eyes open)

        AddResult(gazeOnScreen, avgGX, avgGY);
    }

    // ── Add result + smooth ───────────────────────────────────────────────────
    private void AddResult(bool focused, double gx, double gy)
    {
        // Focus smoothing
        _focusHistory.Enqueue(focused);
        if (_focusHistory.Count > HistLen) _focusHistory.Dequeue();
        int cnt = _focusHistory.Count(h => h);
        if (!IsFocused && cnt >= FocusMin) IsFocused = true;
        else if (IsFocused && cnt <= FocusMax) IsFocused = false;

        // Gaze smoothing (exponential moving average)
        _gazeX.Enqueue(gx); if (_gazeX.Count > HistLen) _gazeX.Dequeue();
        _gazeY.Enqueue(gy); if (_gazeY.Count > HistLen) _gazeY.Dequeue();
        GazePoint = (_gazeX.Average(), _gazeY.Average());
    }

    private static void Clamp(ref int x, ref int y, ref int w, ref int h, int maxW, int maxH)
    {
        x = Math.Max(0, x); y = Math.Max(0, y);
        w = Math.Min(w, maxW - x); h = Math.Min(h, maxH - y);
    }

    public void Dispose() { _faceCascade.Dispose(); _eyeCascade.Dispose(); }
}
