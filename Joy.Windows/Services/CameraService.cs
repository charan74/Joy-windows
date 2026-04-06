// CameraService.cs
// Joy — Windows
//
// Captures video frames from the default webcam using OpenCvSharp.
// Runs the capture loop on a background thread; raises FrameCaptured
// on the thread pool for processing by FocusDetectorService.

using OpenCvSharp;

namespace Joy.Windows.Services;

/// <summary>
/// Manages webcam capture. Call <see cref="Start"/> to begin streaming frames.
/// Subscribe to <see cref="FrameCaptured"/> to receive each <see cref="Mat"/>.
/// Always dispose the received Mat after use.
/// </summary>
public sealed class CameraService : IDisposable
{
    // ─── Public API ──────────────────────────────────────────────────────────

    /// <summary>Raised on a background thread for every captured frame.</summary>
    public event Action<Mat>? FrameCaptured;

    /// <summary>True while the capture loop is running.</summary>
    public bool IsRunning => _running;

    // ─── Private ──────────────────────────────────────────────────────────────

    private VideoCapture? _capture;
    private Thread?       _thread;
    private volatile bool _running;
    private readonly object _lock = new();

    // ─── Start / Stop ─────────────────────────────────────────────────────────

    /// <summary>
    /// Opens the default camera (index 0) and begins the capture loop.
    /// Safe to call multiple times; no-op if already running.
    /// </summary>
    public void Start()
    {
        lock (_lock)
        {
            if (_running) return;
            _capture = new VideoCapture(0)
            {
                FrameWidth  = 320,   // Low resolution — we only need face detection
                FrameHeight = 240,
            };
            if (!_capture.IsOpened())
            {
                _capture.Dispose();
                _capture = null;
                return;
            }
            _running = true;
            _thread  = new Thread(CaptureLoop) { IsBackground = true, Name = "Joy.CameraCapture" };
            _thread.Start();
        }
    }

    /// <summary>Stops the capture loop and releases the camera device.</summary>
    public void Stop()
    {
        lock (_lock)
        {
            _running = false;
            _capture?.Dispose();
            _capture = null;
        }
    }

    // ─── Capture loop ─────────────────────────────────────────────────────────

    private void CaptureLoop()
    {
        while (_running)
        {
            Mat frame;
            lock (_lock)
            {
                if (_capture is null || !_running) break;
                frame = new Mat();
                if (!_capture.Read(frame) || frame.Empty())
                {
                    frame.Dispose();
                    Thread.Sleep(50);
                    continue;
                }
            }

            try
            {
                FrameCaptured?.Invoke(frame);
            }
            catch
            {
                frame.Dispose();
            }
            // Frame ownership passes to subscriber; they must dispose.
        }
    }

    // ─── IDisposable ──────────────────────────────────────────────────────────

    public void Dispose() => Stop();
}
