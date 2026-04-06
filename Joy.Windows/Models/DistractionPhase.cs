// DistractionPhase.cs
// Joy — Windows
// Escalation ladder driven by how long the user has been distracted.

namespace Joy.Windows.Models;

/// <summary>
/// Distraction escalation phases — mirrors the macOS implementation exactly.
/// </summary>
public enum DistractionPhase
{
    /// <summary>User is focused — normal operation.</summary>
    None      = 0,
    /// <summary>0–3 s: curious eyes, timer frozen.</summary>
    Searching = 1,
    /// <summary>3–6 s: angry eyes, "DISTRACTED" label.</summary>
    Angry     = 2,
    /// <summary>6–9 s: angry + red eyes + shaking text.</summary>
    Critical  = 3,
    /// <summary>9 s+: timer reset, brief tired state.</summary>
    Reset     = 4,
}
