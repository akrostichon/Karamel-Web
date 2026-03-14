using System.Text.Json;
using Karamel.Web.Models;

namespace Karamel.Web.Services;

/// <summary>
/// Service for HTTP calls to backend /api/sessions endpoints
/// </summary>
public interface ISessionApiClient
{
    /// <summary>
    /// Upload sanitized library to server-side API for paginated listing (main tab only)
    /// PRIVACY: Uses ConvertSongToUploadDto which excludes file paths
    /// </summary>
    Task<bool> UploadLibraryToServerAsync(Guid sessionId, IEnumerable<Song> songs, string? token = null);

    /// <summary>
    /// Fetch a paginated library page from server (prefers SignalR RPC when available)
    /// Returns a JSON element containing { items, page, pageSize, totalCount }
    /// </summary>
    Task<JsonElement> FetchLibraryPageAsync(Guid sessionId, int page = 1, int pageSize = 50, string? search = null, string? sort = null, string? artist = null);

    /// <summary>
    /// Search library on server
    /// </summary>
    Task<JsonElement> SearchLibraryAsync(Guid sessionId, string query, int maxResults = 10);

    /// <summary>
    /// Fetch session configuration from backend API (multi-device scenario)
    /// Returns session configuration with theme
    /// </summary>
    Task<Session?> FetchSessionConfigFromBackendAsync(Guid sessionId);

    /// <summary>
    /// Fetches the full artist list for a session from the backend API.
    /// Returns an empty list if the session has no library or the request fails.
    /// </summary>
    Task<IReadOnlyList<ArtistItem>> FetchArtistsAsync(Guid sessionId);
}
