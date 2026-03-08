using Microsoft.JSInterop;

namespace Karamel.Web.Services;

/// <summary>
/// Service for SignalR connection lifecycle and module initialization
/// Singleton lifecycle - manages connection for entire application
/// </summary>
public class SignalRConnectionManager : ISignalRConnectionManager
{
    private readonly IJSRuntime _jsRuntime;
    private readonly string _backendBaseAddress;
    private readonly ILogger<SignalRConnectionManager> _logger;
    private IJSObjectReference? _sessionBridgeModule;
    private bool _isInitialized;
    private bool _isMainTab;

    /// <summary>
    /// Gets whether this tab is the main tab (has directory handle)
    /// </summary>
    public bool IsMainTab => _isMainTab;

    public SignalRConnectionManager(
        IJSRuntime jsRuntime,
        string backendBaseAddress,
        ILogger<SignalRConnectionManager> logger)
    {
        _jsRuntime = jsRuntime;
        _backendBaseAddress = backendBaseAddress;
        _logger = logger;
    }

    /// <summary>
    /// Initialize session bridge with JavaScript module
    /// </summary>
    /// <param name="sessionId">Session GUID</param>
    /// <param name="asMainTab">Whether this tab has directory handle (main tab)</param>
    public async Task InitializeAsync(Guid sessionId, bool asMainTab, string? token = null)
    {
        if (_isInitialized)
            return;

        _isMainTab = asMainTab;
        _sessionBridgeModule = await _jsRuntime.InvokeAsync<IJSObjectReference>(
            "import", "./js/signalRBridge.js");

        // Pass auth token and backend URL if present so JS SignalR client can authenticate when connecting
        await _sessionBridgeModule.InvokeVoidAsync("initializeSession", sessionId.ToString(), asMainTab, token, _backendBaseAddress);

        _isInitialized = true;
        _logger.LogInformation("SignalR connection manager initialized for session {SessionId} (isMainTab={IsMainTab})", sessionId, asMainTab);
    }

    /// <summary>
    /// Check if main tab is still alive (secondary tabs only)
    /// </summary>
    public async Task<bool> CheckMainTabAliveAsync()
    {
        if (_isMainTab || _sessionBridgeModule == null)
            return true;

        try
        {
            return await _sessionBridgeModule.InvokeAsync<bool>("checkMainTabAlive");
        }
        catch
        {
            return false;
        }
    }

    /// <summary>
    /// Get the session bridge module reference for use by other services
    /// </summary>
    public async Task<IJSObjectReference?> GetModuleAsync()
    {
        // Wait a moment if initialization is in progress
        int retries = 0;
        while (!_isInitialized && retries < 10)
        {
            await Task.Delay(100);
            retries++;
        }

        return _sessionBridgeModule;
    }
}
