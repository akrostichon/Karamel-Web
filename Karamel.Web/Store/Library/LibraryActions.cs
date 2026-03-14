using Karamel.Web.Models;

namespace Karamel.Web.Store.Library;

// Actions
public record LoadLibraryAction(IEnumerable<Song> Songs);
public record LoadLibrarySuccessAction(IReadOnlyList<Song> Songs);
public record LoadLibraryFailureAction(string ErrorMessage);
public record FilterSongsAction(string SearchFilter);
public record ScanProgressAction(int Scanned, bool Complete = false);

// Server-side pagination actions
public record LoadPageAction(int Page, string? SearchQuery, bool Append, string? ArtistFilter = null);
public record LoadPageSuccessAction(IReadOnlyList<Song> Songs, int Page, long TotalCount, string? SearchQuery, bool Append);
public record ResetPaginationAction();

// Fuzzy search suggestion actions
public record SearchSuggestionsAction(IReadOnlyList<string> Suggestions);

// Artist browse actions
public record LoadArtistsAction();
public record LoadArtistsSuccessAction(IReadOnlyList<ArtistItem> Artists);
public record LoadArtistsFailureAction(string ErrorMessage);
