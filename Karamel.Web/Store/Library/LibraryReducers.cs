using Fluxor;
using Karamel.Web.Models;

namespace Karamel.Web.Store.Library;

public static class LibraryReducers
{
    [ReducerMethod]
    public static LibraryState ReduceLoadLibraryAction(LibraryState state, LoadLibraryAction action) =>
        state with
        {
            IsLoading = true,
            ErrorMessage = null,
            ScannedCount = 0,
            ScanComplete = false
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
    public static LibraryState ReduceScanProgressAction(LibraryState state, ScanProgressAction action) =>
        state with
        {
            ScannedCount = action.Scanned,
            ScanComplete = action.Complete,
            // Keep IsLoading true until we receive LoadLibrarySuccess or failure
            IsLoading = !action.Complete,
            // Invalidate artist cache when a rescan starts
            Artists = action.Complete ? state.Artists : Array.Empty<ArtistItem>(),
            IsLoadingArtists = false,
            ArtistsLoaded = action.Complete ? state.ArtistsLoaded : false
        };

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

    // Server-side pagination reducers
    [ReducerMethod]
    public static LibraryState ReduceLoadPageAction(LibraryState state, LoadPageAction action)
    {
        Console.WriteLine($"LibraryReducers: ReduceLoadPageAction called! Page={action.Page}, SearchQuery={action.SearchQuery ?? "null"}, Append={action.Append}");
        return state with
        {
            IsLoading = true
        };
    }

    [ReducerMethod]
    public static LibraryState ReduceLoadPageSuccess(LibraryState state, LoadPageSuccessAction action)
    {
        Console.WriteLine($"LibraryReducers: ReduceLoadPageSuccess called!");
        Console.WriteLine($"  Append={action.Append}, Page={action.Page}, TotalCount={action.TotalCount}");
        Console.WriteLine($"  Songs.Count={action.Songs.Count}, SearchQuery={action.SearchQuery ?? "null"}");
        
        var songs = action.Append 
            ? state.Songs.Concat(action.Songs).ToList() 
            : action.Songs.ToList();

        Console.WriteLine($"  Final Songs.Count={songs.Count}, HasMorePages={(action.Page * state.PageSize) < action.TotalCount}");

        return state with
        {
            Songs = songs,
            CurrentPage = action.Page,
            TotalCount = action.TotalCount,
            ServerSearchQuery = action.SearchQuery,
            IsLoading = false,
            ErrorMessage = null
        };
    }

    [ReducerMethod]
    public static LibraryState ReduceResetPagination(LibraryState state, ResetPaginationAction action) =>
        state with
        {
            CurrentPage = 1,
            TotalCount = 0,
            Songs = Array.Empty<Song>(),
            ServerSearchQuery = null,
            Suggestions = Array.Empty<string>(),
            HasSearchedWithNoResults = false,
            Artists = Array.Empty<ArtistItem>(),
            IsLoadingArtists = false,
            ArtistsLoaded = false
        };

    [ReducerMethod]
    public static LibraryState ReduceSearchSuggestionsAction(LibraryState state, SearchSuggestionsAction action) =>
        state with
        {
            Suggestions = action.Suggestions,
            HasSearchedWithNoResults = action.Suggestions.Count > 0
        };

    // Artist browse reducers
    [ReducerMethod]
    public static LibraryState ReduceLoadArtistsAction(LibraryState state, LoadArtistsAction action) =>
        state with { IsLoadingArtists = true };

    [ReducerMethod]
    public static LibraryState ReduceLoadArtistsSuccessAction(LibraryState state, LoadArtistsSuccessAction action) =>
        state with
        {
            Artists = action.Artists,
            IsLoadingArtists = false,
            ArtistsLoaded = true
        };

    [ReducerMethod]
    public static LibraryState ReduceLoadArtistsFailureAction(LibraryState state, LoadArtistsFailureAction action) =>
        state with
        {
            IsLoadingArtists = false,
            ArtistsLoaded = false,
            ErrorMessage = action.ErrorMessage
        };
}
