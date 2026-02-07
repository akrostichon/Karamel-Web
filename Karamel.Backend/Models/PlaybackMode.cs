namespace Karamel.Backend.Models;

/// <summary>
/// Defines the playback control state for a karaoke session.
/// </summary>
public enum PlaybackMode
{
    /// <summary>
    /// Normal playback mode - songs advance automatically from queue.
    /// Default state when session is created.
    /// </summary>
    Normal = 0,

    /// <summary>
    /// Admin has requested playback stop after the current song finishes.
    /// Transition: Normal → StopAfterCurrent (when admin clicks "Stop after current song").
    /// </summary>
    StopAfterCurrent = 1,

    /// <summary>
    /// Playback has stopped - no current song is playing.
    /// Current song completed while in StopAfterCurrent mode.
    /// Transition: StopAfterCurrent → Stopped (when current song completes).
    /// Transition: Stopped → Normal (when admin clicks "Proceed playback").
    /// </summary>
    Stopped = 2
}
