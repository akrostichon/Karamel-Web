using Microsoft.JSInterop;

namespace Karamel.Web.Services;

/// <summary>
/// Service for SignalR connection lifecycle and module initialization
/// Singleton lifecycle - manages connection for entire session
/// </summary>
public interface ISignalRConnectionManager
{
    /// <summary>
    /// Gets whether this tab is the main tab (has directory handle)
    /// </summary>
    bool IsMainTab { get; }

    /// <summary>
    /// Initialize session bridge with JavaScript module
    /// </summary>
    /// <param name="sessionId">Session GUID</param>
    /// <param name="asMainTab">Whether this tab has directory handle (main tab)</param>
    Task InitializeAsync(Guid sessionId, bool asMainTab, string? linkToken = null);

    /// <summary>
    /// Check if main tab is still alive (secondary tabs only)
    /// </summary>
    Task<bool> CheckMainTabAliveAsync();

    /// <summary>
    /// Get the session bridge module reference for use by other services
    /// </summary>
    Task<IJSObjectReference?> GetModuleAsync();
}
