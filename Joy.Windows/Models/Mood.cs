// Mood.cs
// Joy — Windows
// The seven emotional states of Joy's robot eyes.

namespace Joy.Windows.Models;

/// <summary>Represents the emotional state of Joy's robot eyes.</summary>
public enum Mood
{
    /// <summary>Default resting expression.</summary>
    Normal  = 0,
    /// <summary>Half-closed eyelids drooping outward — low energy.</summary>
    Tired   = 1,
    /// <summary>Furrowed brow eyelids angled inward — hostile.</summary>
    Angry   = 2,
    /// <summary>Squinted bottom half — cheerful.</summary>
    Happy   = 3,
    /// <summary>Horizontal flicker — confused / frozen.</summary>
    Frozen  = 4,
    /// <summary>Vertical flicker + red tint — alarmed.</summary>
    Scary   = 5,
    /// <summary>Eyes grow taller when looking sideways — inquisitive.</summary>
    Curious = 6,
}
