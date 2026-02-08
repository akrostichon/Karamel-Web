using Fluxor;
using Karamel.Web.Contracts;
using Karamel.Web.Services;
using Karamel.Web.Store.Session;

namespace Karamel.Web.Store.Library;

public class LibraryEffects(
    ISessionService sessionService,
    IState<SessionState> sessionState,
    IState<LibraryState> libraryState)
{
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
            var pageSize = libraryState.Value.PageSize;
            var pageResult = await sessionService.FetchLibraryPageAsync(
                session.SessionId,
                action.Page,
                pageSize,
                action.SearchQuery,
                null);

            if (pageResult.ValueKind != System.Text.Json.JsonValueKind.Undefined && 
                pageResult.ValueKind != System.Text.Json.JsonValueKind.Null)
            {
                if (pageResult.TryGetProperty("items", out var itemsArr) && 
                    itemsArr.ValueKind == System.Text.Json.JsonValueKind.Array)
                {
                    var songs = itemsArr.EnumerateArray()
                        .Select(SongConverters.ConvertJsonToSong)
                        .ToList();

                    long totalCount = 0;
                    if (pageResult.TryGetProperty("totalCount", out var totalCountProp) && 
                        totalCountProp.ValueKind == System.Text.Json.JsonValueKind.Number)
                    {
                        totalCount = totalCountProp.GetInt64();
                    }

                    dispatcher.Dispatch(new LoadPageSuccessAction(
                        Songs: songs,
                        Page: action.Page,
                        TotalCount: totalCount,
                        SearchQuery: action.SearchQuery,
                        Append: action.Append
                    ));
                }
                else
                {
                    dispatcher.Dispatch(new LoadLibraryFailureAction("Invalid response format"));
                }
            }
            else
            {
                dispatcher.Dispatch(new LoadLibraryFailureAction("Empty response from server"));
            }
        }
        catch (Exception ex)
        {
            dispatcher.Dispatch(new LoadLibraryFailureAction($"Failed to load page: {ex.Message}"));
        }
    }
}
