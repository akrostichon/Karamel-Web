using Fluxor;

namespace Karamel.Web.Store.Library;

public static class LibraryReducers
{
    [ReducerMethod]
    public static LibraryState ReduceLoadLibraryAction(LibraryState state, LoadLibraryAction action) =>
        state with
        {
            IsLoading = true,
            ErrorMessage = null
        };

    [ReducerMethod]
    public static LibraryState ReduceLoadLibrarySuccessAction(LibraryState state, LoadLibrarySuccessAction action)
    {
        var sortedSongs = action.Songs
            .OrderBy(s => s.Artist)
            .ThenBy(s => s.Title)
            .ToList();
            
        return state with
        {
            Songs = sortedSongs,
            IsLoading = false,
            ErrorMessage = null
        };
    }

    [ReducerMethod]
    public static LibraryState ReduceLoadLibraryFailureAction(LibraryState state, LoadLibraryFailureAction action) =>
        state with
        {
            IsLoading = false,
            ErrorMessage = action.ErrorMessage
        };

    [ReducerMethod]
    public static LibraryState ReduceFilterSongsAction(LibraryState state, FilterSongsAction action) =>
        state with
        {
            SearchFilter = action.SearchFilter
        };
}
