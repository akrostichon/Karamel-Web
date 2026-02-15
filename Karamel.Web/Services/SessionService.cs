using Microsoft.JSInterop;
using System.Text.Json;
using Karamel.Web.Models;

namespace Karamel.Web.Services;

/// <summary>
/// DEPRECATED: Facade for session management services. Use specific services directly.
/// This class delegates to ISessionStorageService, ISessionApiClient, ISignalRPlaylistBridge,
/// ISignalRConnectionManager, ISongEnrichmentService, and IPlaylistStateSynchronizer.
/// Will be removed in next major version.
/// </summary>
[Obsolete("Use specific services directly (ISessionStorageService, ISessionApiClient, ISignalRPlaylistBridge, ISignalRConnectionManager, ISongEnrichmentService, IPlaylistStateSynchronizer). Will be removed in next major version.")]
public class SessionService : ISessionService
{
    private readonly ISessionStorageService _sessionStorage;
    private readonly ISessionApiClient _sessionApiClient;
    private readonly ISignalRPlaylistBridge _signalRBridge;
    private readonly ISignalRConnectionManager _connectionManager;
    private readonly ISongEnrichmentService _songEnrichment;
    private readonly IPlaylistStateSynchronizer _stateSynchronizer;

    public bool IsMainTab => _connectionManager.IsMainTab;

    public SessionService(
        ISessionStorageService sessionStorage,
        ISessionApiClient sessionApiClient,
        ISignalRPlaylistBridge signalRBridge,
        ISignalRConnectionManager connectionManager,
        ISongEnrichmentService songEnrichment,
        IPlaylistStateSynchronizer stateSynchronizer)
    {
        _sessionStorage = sessionStorage;
        _sessionApiClient = sessionApiClient;
        _signalRBridge = signalRBridge;
        _connectionManager = connectionManager;
        _songEnrichment = songEnrichment;
        _stateSynchronizer = stateSynchronizer;
    }

    public async Task InitializeAsync(Guid sessionId, bool asMainTab, string? linkToken = null)
    {
        await _connectionManager.InitializeAsync(sessionId, asMainTab, linkToken);
        if (!asMainTab)
        {
            await _stateSynchronizer.RestoreSessionStateAsync(sessionId);
        }
        await _stateSynchronizer.SetupStateUpdateListenerAsync();
    }

    public async Task<bool> UploadLibraryToServerAsync(Guid sessionId, IEnumerable<Song> songs, string? linkToken = null)
        => await _sessionApiClient.UploadLibraryToServerAsync(sessionId, songs, linkToken);

    public async Task<JsonElement> FetchLibraryPageAsync(Guid sessionId, int page = 1, int pageSize = 50, string? search = null, string? sort = null)
        => await _sessionApiClient.FetchLibraryPageAsync(sessionId, page, pageSize, search, sort);

    public async Task<JsonElement> SearchLibraryAsync(Guid sessionId, string query, int maxResults = 10)
        => await _sessionApiClient.SearchLibraryAsync(sessionId, query, maxResults);

    public async Task BroadcastPlaylistUpdatedAsync()
        => await _signalRBridge.BroadcastPlaylistUpdatedAsync();

    public async Task BroadcastSessionSettingsAsync(Session session)
        => await _signalRBridge.BroadcastSessionSettingsAsync(session);

    public async Task BroadcastCurrentSongAsync(Song? song, string? singerName)
        => await _signalRBridge.BroadcastCurrentSongAsync(song, singerName);

    public async Task<string> GenerateSessionUrlAsync(string path, Guid sessionId, string? linkToken = null)
        => await _sessionStorage.GenerateSessionUrlAsync(path, sessionId, linkToken);

    public async Task<Guid?> GetSessionIdFromUrlAsync()
        => await _sessionStorage.GetSessionIdFromUrlAsync();

    public async Task<bool> CheckMainTabAliveAsync()
        => await _connectionManager.CheckMainTabAliveAsync();

    public async Task ClearSessionAsync()
        => await _sessionStorage.ClearSessionAsync();

    public async Task<bool> AddItemToPlaylistAsync(Song song)
        => await _signalRBridge.AddItemToPlaylistAsync(song);

    public async Task<bool> RemoveItemFromPlaylistAsync(Guid itemId)
        => await _signalRBridge.RemoveItemFromPlaylistAsync(itemId);

    public async Task<bool> ReorderPlaylistAsync(int from, int to)
        => await _signalRBridge.ReorderPlaylistAsync(from, to);

    public async Task SetSongStatusAsync(string itemId, int status)
        => await _signalRBridge.SetSongStatusAsync(itemId, status);

    public async Task AdvanceToNextSongAsync()
        => await _signalRBridge.AdvanceToNextSongAsync();

    public async Task CompleteCurrentSongAsync()
        => await _signalRBridge.CompleteCurrentSongAsync();

    public async Task ClearQueueAsync()
        => await _signalRBridge.ClearQueueAsync();

    [JSInvokable]
    public void OnStateUpdated(string type, JsonElement data)
        => _stateSynchronizer.HandleBroadcastMessage(type, data);

    public async ValueTask DisposeAsync()
    {
        await _stateSynchronizer.DisposeAsync();
    }
}
