using Microsoft.JSInterop;
using System.Text.Json;

namespace Karamel.Web.Services;

/// <summary>
/// Service for sessionStorage read/write operations ONLY
/// </summary>
public class SessionStorageService : ISessionStorageService
{
    private readonly IJSRuntime _jsRuntime;
    private IJSObjectReference? _sessionBridgeModule;

    public SessionStorageService(IJSRuntime jsRuntime)
    {
        _jsRuntime = jsRuntime;
    }

    /// <summary>
    /// Ensure the session bridge module is loaded
    /// </summary>
    private async Task<IJSObjectReference> GetModuleAsync()
    {
        if (_sessionBridgeModule == null)
        {
            _sessionBridgeModule = await _jsRuntime.InvokeAsync<IJSObjectReference>(
                "import", "./js/signalRBridge.js");
        }
        return _sessionBridgeModule;
    }

    /// <summary>
    /// Read session state from browser sessionStorage
    /// </summary>
    public async Task<JsonElement> ReadSessionStorageAsync(Guid sessionId)
    {
        var module = await GetModuleAsync();
        var stateJson = await module.InvokeAsync<JsonElement>("getSessionStateForSession", sessionId.ToString());
#if DEBUG
        Console.WriteLine($"SessionStorageService: Got state from sessionStorage: {stateJson}");
#endif
        return stateJson;
    }

    /// <summary>
    /// Generate session URL with SessionId and LinkToken query parameters
    /// </summary>
    public async Task<string> GenerateSessionUrlAsync(string path, Guid sessionId, string? linkToken = null)
    {
        var module = await GetModuleAsync();
        return await module.InvokeAsync<string>(
            "generateSessionUrl", path, sessionId.ToString(), linkToken);
    }

    /// <summary>
    /// Get SessionId from current URL query parameter
    /// </summary>
    public async Task<Guid?> GetSessionIdFromUrlAsync()
    {
        var module = await GetModuleAsync();
        var sessionIdString = await module.InvokeAsync<string?>("getSessionIdFromUrl");
        
        return Guid.TryParse(sessionIdString, out var sessionId) ? sessionId : null;
    }

    /// <summary>
    /// Clear session state (when session ends)
    /// </summary>
    public async Task ClearSessionAsync()
    {
        var module = await GetModuleAsync();
        await module.InvokeVoidAsync("clearSessionState");
    }
}
