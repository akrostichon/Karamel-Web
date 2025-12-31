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
