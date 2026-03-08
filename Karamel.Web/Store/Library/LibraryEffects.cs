using System.Text.Json;
using Fluxor;
using Karamel.Web.Contracts;
using Karamel.Web.Models;
using Karamel.Web.Services;
using Karamel.Web.Store.Session;

namespace Karamel.Web.Store.Library;

public class LibraryEffects(
    ISessionApiClient sessionApiClient,
    IState<SessionState> sessionState,
    IState<LibraryState> libraryState)
{
    private const string ItemsPropertyName = "items";
    private const string TotalCountPropertyName = "totalCount";
    private const string SuggestionsPropertyName = "suggestions";

    [EffectMethod]
    public async Task HandleLoadPageAction(LoadPageAction action, IDispatcher dispatcher)
    {
        var correlationId = Guid.NewGuid().ToString("N").Substring(0, 8);
        Console.WriteLine($"[DIAG:{correlationId}] LibraryEffects.HandleLoadPageAction: START - Page={action.Page}, SearchQuery={action.SearchQuery ?? "null"}, Append={action.Append}");
        
        var session = sessionState.Value.CurrentSession;
        Console.WriteLine($"[DIAG:{correlationId}] LibraryEffects: sessionState.Value.CurrentSession={(session != null ? $"EXISTS (Id={session.SessionId})" : "NULL")}");
        
        if (session == null)
        {
            Console.WriteLine($"[ERROR:{correlationId}] LibraryEffects: NO ACTIVE SESSION - dispatching LoadLibraryFailureAction");
            dispatcher.Dispatch(new LoadLibraryFailureAction("No active session"));
            return;
        }

        Console.WriteLine($"[DIAG:{correlationId}] LibraryEffects: Using sessionId={session.SessionId}");

        try
        {
            Console.WriteLine($"[DIAG:{correlationId}] LibraryEffects: Calling FetchPageResultAsync...");
            var startTime = DateTime.Now;
            var pageResult = await FetchPageResultAsync(session.SessionId, action);
            var duration = (DateTime.Now - startTime).TotalMilliseconds;
            Console.WriteLine($"[DIAG:{correlationId}] LibraryEffects: FetchPageResultAsync completed in {duration}ms");
            Console.WriteLine($"[DIAG:{correlationId}] LibraryEffects: pageResult.ValueKind={pageResult.ValueKind}");
            
            if (!IsValidJsonResponse(pageResult))
            {
                Console.WriteLine($"[ERROR:{correlationId}] LibraryEffects: Empty or invalid response from server");
                dispatcher.Dispatch(new LoadLibraryFailureAction("Empty or invalid response from server"));
                return;
            }

            if (!TryParseSongsFromResponse(pageResult, out var songs))
            {
                Console.WriteLine($"[ERROR:{correlationId}] LibraryEffects: Response missing 'items' array");
                Console.WriteLine($"[ERROR:{correlationId}] LibraryEffects: pageResult={pageResult}");
                dispatcher.Dispatch(new LoadLibraryFailureAction($"Response missing '{ItemsPropertyName}' array"));
                return;
            }

            var totalCount = ParseTotalCount(pageResult);
            var suggestions = ParseSuggestions(pageResult);

            Console.WriteLine($"[DIAG:{correlationId}] LibraryEffects: Successfully parsed - songs.Count={songs.Count}, totalCount={totalCount}");
            Console.WriteLine($"[DIAG:{correlationId}] LibraryEffects: Dispatching LoadPageSuccessAction");

            dispatcher.Dispatch(new LoadPageSuccessAction(
                Songs: songs,
                Page: action.Page,
                TotalCount: totalCount,
                SearchQuery: action.SearchQuery,
                Append: action.Append
            ));

            // Dispatch suggestions (empty list clears previous suggestions when results found)
            dispatcher.Dispatch(new SearchSuggestionsAction(suggestions));
            
            Console.WriteLine($"[DIAG:{correlationId}] LibraryEffects.HandleLoadPageAction: END - SUCCESS");
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[ERROR:{correlationId}] LibraryEffects: Exception: {ex.GetType().Name}: {ex.Message}");
            Console.WriteLine($"[ERROR:{correlationId}] LibraryEffects: Stack trace: {ex.StackTrace}");
            dispatcher.Dispatch(new LoadLibraryFailureAction($"Failed to load page {action.Page}: {ex.Message}"));
        }
    }

    private async Task<JsonElement> FetchPageResultAsync(Guid sessionId, LoadPageAction action)
    {
        var pageSize = libraryState.Value.PageSize;
        return await sessionApiClient.FetchLibraryPageAsync(
            sessionId,
            action.Page,
            pageSize,
            action.SearchQuery,
            null);
    }

    private static bool IsValidJsonResponse(JsonElement response)
    {
        return response.ValueKind != JsonValueKind.Undefined && 
               response.ValueKind != JsonValueKind.Null;
    }

    private static bool TryParseSongsFromResponse(JsonElement response, out List<Song> songs)
    {
        songs = [];

        if (!response.TryGetProperty(ItemsPropertyName, out var itemsElement) || 
            itemsElement.ValueKind != JsonValueKind.Array)
        {
            return false;
        }

        songs = itemsElement.EnumerateArray()
            .Select(SongConverters.ConvertJsonToSong)
            .ToList();

        return true;
    }

    private static long ParseTotalCount(JsonElement response)
    {
        if (!response.TryGetProperty(TotalCountPropertyName, out var totalCountElement))
        {
            return 0;
        }

        if (totalCountElement.ValueKind == JsonValueKind.Number)
        {
            return totalCountElement.GetInt64();
        }

        return long.TryParse(totalCountElement.ToString(), out var parsedCount) 
            ? parsedCount 
            : 0;
    }

    private static IReadOnlyList<string> ParseSuggestions(JsonElement response)
    {
        if (!response.TryGetProperty(SuggestionsPropertyName, out var suggestionsElement) ||
            suggestionsElement.ValueKind != JsonValueKind.Array)
        {
            return Array.Empty<string>();
        }

        var result = new List<string>();
        foreach (var item in suggestionsElement.EnumerateArray())
        {
            if (item.TryGetProperty("text", out var textEl) && textEl.ValueKind == JsonValueKind.String)
            {
                var text = textEl.GetString();
                if (!string.IsNullOrWhiteSpace(text))
                    result.Add(text);
            }
        }
        return result;
    }
}
