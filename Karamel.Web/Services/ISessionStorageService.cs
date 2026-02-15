using System.Text.Json;

namespace Karamel.Web.Services;

/// <summary>
/// Service for sessionStorage read/write operations
/// </summary>
public interface ISessionStorageService
{
    /// <summary>
    /// Read session state from browser sessionStorage
    /// </summary>
    Task<JsonElement> ReadSessionStorageAsync(Guid sessionId);

    /// <summary>
    /// Generate session URL with SessionId and LinkToken query parameters
    /// </summary>
    Task<string> GenerateSessionUrlAsync(string path, Guid sessionId, string? linkToken = null);

    /// <summary>
    /// Get SessionId from current URL query parameter
    /// </summary>
    Task<Guid?> GetSessionIdFromUrlAsync();

    /// <summary>
    /// Clear session state (when session ends)
    /// </summary>
    Task ClearSessionAsync();
}
