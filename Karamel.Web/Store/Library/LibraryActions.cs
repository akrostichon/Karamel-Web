using Karamel.Web.Models;

namespace Karamel.Web.Store.Library;

// Actions
public record LoadLibraryAction(IEnumerable<Song> Songs);
public record LoadLibrarySuccessAction(IReadOnlyList<Song> Songs);
public record LoadLibraryFailureAction(string ErrorMessage);
public record FilterSongsAction(string SearchFilter);
