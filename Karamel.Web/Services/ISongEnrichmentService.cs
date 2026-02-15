using Karamel.Web.Models;

namespace Karamel.Web.Services;

/// <summary>
/// Service to enrich playlist songs with file paths from library (main tab privacy boundary)
/// </summary>
public interface ISongEnrichmentService
{
    /// <summary>
    /// Build library lookup dictionary by song ID for O(1) access
    /// </summary>
    Dictionary<Guid, Song> BuildLibraryLookup(IEnumerable<Song> librarySongs);

    /// <summary>
    /// Enrich songs in the list with file information from library lookup
    /// Only enriches if this is the main tab (privacy boundary)
    /// </summary>
    void EnrichSongsWithLibraryFiles(List<Song> songs, Dictionary<Guid, Song> libraryLookup);
}
