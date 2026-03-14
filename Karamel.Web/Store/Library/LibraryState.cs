using Fluxor;
using Karamel.Web.Models;

namespace Karamel.Web.Store.Library;

[FeatureState]
public record LibraryState
{
    public IReadOnlyList<Song> Songs { get; init; } = Array.Empty<Song>();
    public bool IsLoading { get; init; }
    public string? ErrorMessage { get; init; }
    public string SearchFilter { get; init; } = string.Empty;
    // Number of song matches discovered so far during scan
    public int ScannedCount { get; init; }
    // Whether the scan completed
    public bool ScanComplete { get; init; }
    
    // Server-side pagination properties
    public int CurrentPage { get; init; } = 1;
    public int PageSize { get; init; } = 50;
    public long TotalCount { get; init; } = 0;
    public string? ServerSearchQuery { get; init; } = null;
    
    // Computed property: whether more pages are available from server
    public bool HasMorePages => (CurrentPage * PageSize) < TotalCount;

    // Fuzzy search suggestions and zero-results state
    public IReadOnlyList<string> Suggestions { get; init; } = Array.Empty<string>();
    public bool HasSearchedWithNoResults { get; init; } = false;

    // Artist browse state
    public IReadOnlyList<ArtistItem> Artists { get; init; } = Array.Empty<ArtistItem>();
    public bool IsLoadingArtists { get; init; } = false;
    public bool ArtistsLoaded { get; init; } = false;
    public bool IsLoadingArtistSongs { get; init; } = false;
    public string? ArtistSongsError { get; init; } = null;
    
    public IReadOnlyList<Song> FilteredSongs
    {
        get
        {
            if (string.IsNullOrWhiteSpace(SearchFilter))
                return Songs;
                
            var filter = SearchFilter.ToLowerInvariant();
            return Songs
                .Where(s => s.Artist.ToLowerInvariant().Contains(filter) || 
                           s.Title.ToLowerInvariant().Contains(filter))
                .ToList();
        }
    }
}
