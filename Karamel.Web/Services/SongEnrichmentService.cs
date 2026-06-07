using Karamel.Web.Models;

namespace Karamel.Web.Services;

/// <summary>
/// Service to enrich playlist songs with file paths from library (main tab privacy boundary)
/// Stateless transformer - reads library state but never dispatches
/// </summary>
public class SongEnrichmentService : ISongEnrichmentService
{
    private readonly ISignalRConnectionManager _connectionManager;

    public SongEnrichmentService(ISignalRConnectionManager connectionManager)
    {
        _connectionManager = connectionManager;
    }

    /// <summary>
    /// Build library lookup dictionary by song ID for O(1) access
    /// </summary>
    public Dictionary<Guid, Song> BuildLibraryLookup(IEnumerable<Song> librarySongs)
    {
        return librarySongs.ToDictionary(s => s.Id);
    }

    /// <summary>
    /// Enrich songs in the list with file information from library lookup
    /// Only enriches if this is the main tab (privacy boundary)
    /// </summary>
    public void EnrichSongsWithLibraryFiles(List<Song> songs, Dictionary<Guid, Song> libraryLookup)
    {
        // Privacy boundary: Only enrich if main tab (has file access)
        if (!_connectionManager.IsMainTab)
            return;

        for (int i = 0; i < songs.Count; i++)
        {
            var song = songs[i];
            
            // Skip if already has file information (check based on MediaType)
            if (IsMp3SongWithFilledFileInformation(song) ||
                IsVideoSongWithFilledFileInformation(song))
            {
                continue;
            }
            
            // Look up in local library by ID
            if (libraryLookup.TryGetValue(song.Id, out var libraryMatch))
            {
                // Replace with enriched song (preserving AddedBySinger from playlist)
                songs[i] = libraryMatch with { AddedBySinger = song.AddedBySinger };
#if DEBUG
                if (libraryMatch.MediaType == MediaType.Video)
                {
                    Console.WriteLine($"SongEnrichmentService: Enriched VIDEO '{song.Artist} - {song.Title}' (ID: {song.Id}) with VideoFileName: {libraryMatch.VideoFileName}");
                }
                else
                {
                    Console.WriteLine($"SongEnrichmentService: Enriched MP3+CDG '{song.Artist} - {song.Title}' (ID: {song.Id}) with Mp3FileName: {libraryMatch.Mp3FileName}");
                }
#endif
            }
            else
            {
                Console.WriteLine($"SongEnrichmentService: WARNING - Could not find song ID {song.Id} ('{song.Artist} - {song.Title}') in local library");
            }
        }
    }

    private bool IsMp3SongWithFilledFileInformation(Song song)
    {
        return song.MediaType == MediaType.Mp3Cdg &&
               !string.IsNullOrEmpty(song.Mp3FileName) &&
               !string.IsNullOrEmpty(song.CdgFileName);
    }

    private bool IsVideoSongWithFilledFileInformation(Song song)
    {
        return song.MediaType == MediaType.Video &&
               !string.IsNullOrEmpty(song.VideoFileName);
    }
}
