using System.Text.Json;
using Karamel.Web.Models;
using Karamel.Web.Contracts;

namespace Karamel.Web.Services;

/// <summary>
/// Service for session state restoration orchestration for secondary tabs
/// Orchestrator that returns DTOs - Effects handle action dispatching
/// </summary>
public interface IPlaylistStateSynchronizer
{
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
    /// Returns parsed data for Effects to dispatch
    /// </summary>
    (List<Song> queue, Song? currentSong, Dictionary<string, int> singerCounts, string? currentSingerName)? HandlePlaylistUpdate(JsonElement data);

    /// <summary>
    /// Handle session settings update from broadcast
    /// </summary>
    Session? HandleSessionSettingsUpdate(JsonElement data);

    /// <summary>
    /// Handle current song update from broadcast
    /// </summary>
    (Song? song, string? singerName)? HandleCurrentSongUpdate(JsonElement data);
}
