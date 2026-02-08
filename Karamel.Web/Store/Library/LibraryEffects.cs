using System.Text.Json;
using Fluxor;
using Karamel.Web.Contracts;
using Karamel.Web.Models;
using Karamel.Web.Services;
using Karamel.Web.Store.Session;

namespace Karamel.Web.Store.Library;

public class LibraryEffects(
    ISessionService sessionService,
    IState<SessionState> sessionState,
    IState<LibraryState> libraryState)
{
    private const string ItemsPropertyName = "items";
    private const string TotalCountPropertyName = "totalCount";

    [EffectMethod]
    public async Task HandleLoadPageAction(LoadPageAction action, IDispatcher dispatcher)
    {
        var session = sessionState.Value.CurrentSession;
        if (session == null)
        {
            dispatcher.Dispatch(new LoadLibraryFailureAction("No active session"));
            return;
        }

        try
        {
            var pageResult = await FetchPageResultAsync(session.SessionId, action);
            
            if (!IsValidJsonResponse(pageResult))
            {
                dispatcher.Dispatch(new LoadLibraryFailureAction("Empty or invalid response from server"));
                return;
            }

            if (!TryParseSongsFromResponse(pageResult, out var songs))
            {
                dispatcher.Dispatch(new LoadLibraryFailureAction($"Response missing '{ItemsPropertyName}' array"));
                return;
            }

            var totalCount = ParseTotalCount(pageResult);

            Console.WriteLine($"LibraryEffects: Loaded page {action.Page}: {songs.Count} songs (total: {totalCount})");

            dispatcher.Dispatch(new LoadPageSuccessAction(
                Songs: songs,
                Page: action.Page,
                TotalCount: totalCount,
                SearchQuery: action.SearchQuery,
                Append: action.Append
            ));
        }
        catch (Exception ex)
        {
            dispatcher.Dispatch(new LoadLibraryFailureAction($"Failed to load page {action.Page}: {ex.Message}"));
        }
    }

    private async Task<JsonElement> FetchPageResultAsync(Guid sessionId, LoadPageAction action)
    {
        var pageSize = libraryState.Value.PageSize;
        return await sessionService.FetchLibraryPageAsync(
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
}
