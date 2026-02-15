using System.Text.Json;
using Karamel.Web.Models;
using Karamel.Web.Contracts;
using Microsoft.JSInterop;

namespace Karamel.Web.Services;

/// <summary>
/// Service for session state restoration orchestration for secondary tabs
/// Orchestrator that returns DTOs - Effects handle action dispatching
/// </summary>
public interface IPlaylistStateSynchronizer : IAsyncDisposable
{
    /// <summary>
    /// Event raised when a broadcast state update is received and parsed.
    /// Effects should subscribe and dispatch appropriate Fluxor actions.
    /// </summary>
    event Action<BroadcastStateUpdate>? StateUpdateReceived;

    /// <summary>
    /// Restore session state from sessionStorage (secondary tabs)
    /// Returns (SessionConfig, PlaylistItems, CurrentSong) tuple for Effects to dispatch
    /// </summary>
    Task<(Session? session, List<PlaylistItemDto>? playlist, SongDto? currentSong)> RestoreSessionStateAsync(Guid sessionId);

    /// <summary>
    /// Setup listener for ongoing state updates from main tab (secondary tabs only)
    /// </summary>
    Task SetupStateUpdateListenerAsync();

    /// <summary>
    /// Handle state update from broadcast (called by JavaScript via JSInvokable)
    /// Parses payload and raises StateUpdateReceived event for Effects to dispatch
    /// </summary>
    [JSInvokable]
    void HandleBroadcastMessage(string type, JsonElement data);
}

public sealed record PlaylistBroadcastUpdate(
    List<Song> Queue,
    Song? CurrentSong,
    Dictionary<string, int> SingerCounts,
    string? CurrentSingerName);

public sealed record CurrentSongBroadcastUpdate(Song? Song, string? SingerName);

public sealed record BroadcastStateUpdate(
    string Type,
    PlaylistBroadcastUpdate? Playlist,
    Session? Session,
    CurrentSongBroadcastUpdate? CurrentSong);
