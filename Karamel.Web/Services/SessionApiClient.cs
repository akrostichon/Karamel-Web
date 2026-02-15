using Microsoft.JSInterop;
using System.Net.Http.Json;
using System.Text.Json;
using Karamel.Web.Contracts;
using Karamel.Web.Models;

namespace Karamel.Web.Services;

/// <summary>
/// Service for HTTP calls to backend /api/sessions endpoints
/// Thin wrapper over HttpClient, returns DTOs
/// </summary>
public class SessionApiClient : ISessionApiClient
{
    private readonly IJSRuntime _jsRuntime;
    private readonly HttpClient _httpClient;
    private IJSObjectReference? _sessionBridgeModule;

    public SessionApiClient(IJSRuntime jsRuntime, HttpClient httpClient)
    {
        _jsRuntime = jsRuntime;
        _httpClient = httpClient;
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
    /// Upload sanitized library to server-side API for paginated listing (main tab only)
    /// PRIVACY: Uses ConvertSongToUploadDto which excludes file paths
    /// </summary>
    public async Task<bool> UploadLibraryToServerAsync(Guid sessionId, IEnumerable<Song> songs, string? linkToken = null)
    {
        var module = await GetModuleAsync();
        
        var data = new
        {
            songs = songs.Select(SongConverters.ConvertSongToUploadDto).ToArray()  // PRIVACY: Use sanitized DTO
        };

        try
        {
            return await module.InvokeAsync<bool>("uploadLibraryToServer", sessionId.ToString(), data, new { linkToken });
        }
        catch (Exception ex)
        {
            Console.WriteLine($"SessionApiClient: uploadLibraryToServer failed: {ex.Message}");
            return false;
        }
    }

    /// <summary>
    /// Fetch a paginated library page from server
    /// </summary>
    public async Task<JsonElement> FetchLibraryPageAsync(Guid sessionId, int page = 1, int pageSize = 50, string? search = null, string? sort = null)
    {
        var module = await GetModuleAsync();
        
        try
        {
            var result = await module.InvokeAsync<JsonElement>("fetchLibraryPage", sessionId.ToString(), page, pageSize, search, sort);
            return result;
        }
        catch (Exception ex)
        {
            Console.WriteLine($"SessionApiClient: FetchLibraryPageAsync failed: {ex.Message}");
            return default;
        }
    }

    /// <summary>
    /// Search library on server
    /// </summary>
    public async Task<JsonElement> SearchLibraryAsync(Guid sessionId, string query, int maxResults = 10)
    {
        var module = await GetModuleAsync();
        
        try
        {
            var result = await module.InvokeAsync<JsonElement>("searchLibrary", sessionId.ToString(), query, maxResults);
            return result;
        }
        catch (Exception ex)
        {
            Console.WriteLine($"SessionApiClient: SearchLibraryAsync failed: {ex.Message}");
            return default;
        }
    }

    /// <summary>
    /// Fetch session configuration from backend API (multi-device scenario)
    /// </summary>
    public async Task<Session?> FetchSessionConfigFromBackendAsync(Guid sessionId)
    {
        try
        {
            Console.WriteLine($"[DIAG] SessionApiClient.FetchSessionConfigFromBackendAsync: START for sessionId={sessionId}");
            Console.WriteLine($"[DIAG] SessionApiClient: Making HTTP GET to /api/sessions/{sessionId}");
            
            var response = await _httpClient.GetAsync($"/api/sessions/{sessionId}");
            
            Console.WriteLine($"[DIAG] SessionApiClient: Response StatusCode={response.StatusCode}");
            
            if (response.IsSuccessStatusCode)
            {
                var responseBody = await response.Content.ReadAsStringAsync();
                Console.WriteLine($"[DIAG] SessionApiClient: Response body={responseBody}");
                
                var sessionDto = await response.Content.ReadFromJsonAsync<JsonElement>();
                
                var session = new Session
                {
                    SessionId = sessionId,
                    RequireSingerName = sessionDto.GetProperty("requireSingerName").GetBoolean(),
                    AllowSingersToReorder = sessionDto.TryGetProperty("allowSingersToReorder", out var allowReorder) 
                        ? allowReorder.GetBoolean() 
                        : false,
                    PauseBetweenSongs = sessionDto.TryGetProperty("pauseBetweenSongs", out var pauseEnabled) 
                        ? pauseEnabled.GetBoolean() 
                        : true,
                    PauseBetweenSongsSeconds = sessionDto.GetProperty("pauseBetweenSongsSeconds").GetInt32(),
                    FilenamePattern = "%artist - %title",
                    Theme = sessionDto.TryGetProperty("theme", out var theme) && theme.ValueKind == JsonValueKind.String
                        ? theme.GetString()
                        : null
                };
                
                Console.WriteLine($"[DIAG] SessionApiClient: Parsed session from backend - SessionId={session.SessionId}");
                return session;
            }
            else if (response.StatusCode == System.Net.HttpStatusCode.NotFound)
            {
                Console.WriteLine($"[ERROR] SessionApiClient: Session {sessionId} NOT FOUND on backend (404)");
                throw new InvalidOperationException("Session has expired or does not exist. Please start a new session.");
            }
            else
            {
                var errorBody = await response.Content.ReadAsStringAsync();
                Console.WriteLine($"[ERROR] SessionApiClient: Failed to fetch session - StatusCode={response.StatusCode}, Body={errorBody}");
                throw new InvalidOperationException($"Failed to retrieve session configuration: {response.StatusCode}");
            }
        }
        catch (InvalidOperationException)
        {
            // Re-throw our own exceptions
            throw;
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[ERROR] SessionApiClient: Exception in FetchSessionConfigFromBackendAsync: {ex.GetType().Name}: {ex.Message}");
            Console.WriteLine($"[ERROR] SessionApiClient: Stack trace: {ex.StackTrace}");
            throw new InvalidOperationException("Unable to connect to session. Please check your network connection and try again.", ex);
        }
    }
}
