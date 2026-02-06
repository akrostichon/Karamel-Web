using Karamel.Web.Models;
using Karamel.Web.Store.Library;

namespace Karamel.Web.Helpers;

/// <summary>
/// Shared utility methods for playlist and song operations.
/// </summary>
public static class PlaylistHelpers
{
    /// <summary>
    /// Look up full Song metadata from LibraryState using a song ID.
    /// PlaylistItemDto is minimal (no file paths); use this helper to get full Song for playback.
    /// 
    /// IMPORTANT: In non-main tabs, the returned Song will have empty file paths (fetched from backend).
    /// Only the main tab (with File System Access API handle) has usable file paths for playback.
    /// Secondary tabs can use this for display purposes (Artist, Title) but cannot load song files.
    /// </summary>
    /// <param name="libraryState">Current library state containing all songs</param>
    /// <param name="songId">Song ID to look up (nullable)</param>
    /// <returns>Full Song object if found, null otherwise</returns>
    public static Song? GetSongById(LibraryState libraryState, string? songId)
    {
        if (string.IsNullOrEmpty(songId)) return null;
        return libraryState.Songs.FirstOrDefault(s => s.Id.ToString() == songId);
    }
}
