using Karamel.Web.Models;
using Karamel.Web.Services;
using Moq;
using Xunit;

namespace Karamel.Web.Tests;

/// <summary>
/// Tests for SongEnrichmentService - verifies song enrichment with file paths from library
/// PRIVACY: Only enriches when IsMainTab=true (file paths never leave main tab)
/// </summary>
public class SongEnrichmentServiceTests
{
    private readonly Mock<ISignalRConnectionManager> _mockConnectionManager;
    private readonly SongEnrichmentService _service;

    public SongEnrichmentServiceTests()
    {
        _mockConnectionManager = new Mock<ISignalRConnectionManager>();
        _service = new SongEnrichmentService(_mockConnectionManager.Object);
    }

    [Fact]
    public void EnrichSongsWithLibraryFiles_ShouldEnrichMp3CdgSongsWithFileInformation()
    {
        // Arrange
        _mockConnectionManager.Setup(m => m.IsMainTab).Returns(true);
        
        var songId = Guid.NewGuid();
        
        // Song from backend (no file paths - privacy requirement)
        var backendSong = new Song
        {
            Id = songId,
            Artist = "Test Artist",
            Title = "Test Song",
            MediaType = MediaType.Mp3Cdg,
            Mp3FileName = null,
            CdgFileName = null,
            AddedBySinger = "John Doe"
        };

        // Song from local library (with file paths)
        var librarySong = new Song
        {
            Id = songId,
            Artist = "Test Artist",
            Title = "Test Song",
            MediaType = MediaType.Mp3Cdg,
            Mp3FileName = "test.mp3",
            CdgFileName = "test.cdg",
            Path = "some/path"
        };

        var songs = new List<Song> { backendSong };
        var libraryLookup = new Dictionary<Guid, Song> { { songId, librarySong } };

        // Act
        _service.EnrichSongsWithLibraryFiles(songs, libraryLookup);

        // Assert
        Assert.Single(songs);
        var enrichedSong = songs[0];
        Assert.Equal("test.mp3", enrichedSong.Mp3FileName);
        Assert.Equal("test.cdg", enrichedSong.CdgFileName);
        Assert.Equal("some/path", enrichedSong.Path);
        Assert.Equal("John Doe", enrichedSong.AddedBySinger); // Preserved from backend
    }

    [Fact]
    public void EnrichSongsWithLibraryFiles_ShouldEnrichVideoSongsWithFileInformation()
    {
        // Arrange
        _mockConnectionManager.Setup(m => m.IsMainTab).Returns(true);
        
        var songId = Guid.NewGuid();
        
        // Song from backend (no file paths - privacy requirement)
        var backendSong = new Song
        {
            Id = songId,
            Artist = "Test Artist",
            Title = "Test Video Song",
            MediaType = MediaType.Video,
            VideoFileName = null,
            VideoExtension = ".mp4",
            AddedBySinger = "Jane Smith"
        };

        // Song from local library (with file paths)
        var librarySong = new Song
        {
            Id = songId,
            Artist = "Test Artist",
            Title = "Test Video Song",
            MediaType = MediaType.Video,
            VideoFileName = "test-video.mp4",
            VideoExtension = ".mp4",
            Path = "videos/path"
        };

        var songs = new List<Song> { backendSong };
        var libraryLookup = new Dictionary<Guid, Song> { { songId, librarySong } };

        // Act
        _service.EnrichSongsWithLibraryFiles(songs, libraryLookup);

        // Assert
        Assert.Single(songs);
        var enrichedSong = songs[0];
        Assert.Equal("test-video.mp4", enrichedSong.VideoFileName);
        Assert.Equal(".mp4", enrichedSong.VideoExtension);
        Assert.Equal("videos/path", enrichedSong.Path);
        Assert.Equal("Jane Smith", enrichedSong.AddedBySinger); // Preserved from backend
    }

    [Fact]
    public void EnrichSongsWithLibraryFiles_ShouldSkipAlreadyEnrichedMp3CdgSongs()
    {
        // Arrange
        _mockConnectionManager.Setup(m => m.IsMainTab).Returns(true);
        
        var songId = Guid.NewGuid();
        
        // Song already has file information (e.g., from main tab)
        var alreadyEnrichedSong = new Song
        {
            Id = songId,
            Artist = "Test Artist",
            Title = "Test Song",
            MediaType = MediaType.Mp3Cdg,
            Mp3FileName = "existing.mp3",
            CdgFileName = "existing.cdg",
            Path = "existing/path",
            AddedBySinger = "John Doe"
        };

        var songs = new List<Song> { alreadyEnrichedSong };
        var libraryLookup = new Dictionary<Guid, Song>();

        // Act
        _service.EnrichSongsWithLibraryFiles(songs, libraryLookup);

        // Assert - Song should remain unchanged
        Assert.Single(songs);
        var song = songs[0];
        Assert.Equal("existing.mp3", song.Mp3FileName);
        Assert.Equal("existing.cdg", song.CdgFileName);
        Assert.Equal("existing/path", song.Path);
    }

    [Fact]
    public void EnrichSongsWithLibraryFiles_ShouldSkipAlreadyEnrichedVideoSongs()
    {
        // Arrange
        _mockConnectionManager.Setup(m => m.IsMainTab).Returns(true);
        
        var songId = Guid.NewGuid();
        
        // Video song already has file information
        var alreadyEnrichedSong = new Song
        {
            Id = songId,
            Artist = "Test Artist",
            Title = "Test Video",
            MediaType = MediaType.Video,
            VideoFileName = "existing-video.mp4",
            VideoExtension = ".mp4",
            Path = "existing/video/path",
            AddedBySinger = "Jane Smith"
        };

        var songs = new List<Song> { alreadyEnrichedSong };
        var libraryLookup = new Dictionary<Guid, Song>();

        // Act
        _service.EnrichSongsWithLibraryFiles(songs, libraryLookup);

        // Assert - Song should remain unchanged
        Assert.Single(songs);
        var song = songs[0];
        Assert.Equal("existing-video.mp4", song.VideoFileName);
        Assert.Equal(".mp4", song.VideoExtension);
        Assert.Equal("existing/video/path", song.Path);
    }

    [Fact]
    public void EnrichSongsWithLibraryFiles_ShouldHandleMixedMp3CdgAndVideoSongs()
    {
        // Arrange
        _mockConnectionManager.Setup(m => m.IsMainTab).Returns(true);
        
        var mp3SongId = Guid.NewGuid();
        var videoSongId = Guid.NewGuid();
        
        // Backend songs (no file paths)
        var backendMp3Song = new Song
        {
            Id = mp3SongId,
            Artist = "Artist 1",
            Title = "MP3 Song",
            MediaType = MediaType.Mp3Cdg,
            Mp3FileName = null,
            CdgFileName = null
        };

        var backendVideoSong = new Song
        {
            Id = videoSongId,
            Artist = "Artist 2",
            Title = "Video Song",
            MediaType = MediaType.Video,
            VideoFileName = null
        };

        // Library songs (with file paths)
        var libraryMp3Song = new Song
        {
            Id = mp3SongId,
            Artist = "Artist 1",
            Title = "MP3 Song",
            MediaType = MediaType.Mp3Cdg,
            Mp3FileName = "song1.mp3",
            CdgFileName = "song1.cdg"
        };

        var libraryVideoSong = new Song
        {
            Id = videoSongId,
            Artist = "Artist 2",
            Title = "Video Song",
            MediaType = MediaType.Video,
            VideoFileName = "video1.mp4",
            VideoExtension = ".mp4"
        };

        var songs = new List<Song> { backendMp3Song, backendVideoSong };
        var libraryLookup = new Dictionary<Guid, Song>
        {
            { mp3SongId, libraryMp3Song },
            { videoSongId, libraryVideoSong }
        };

        // Act
        _service.EnrichSongsWithLibraryFiles(songs, libraryLookup);

        // Assert
        Assert.Equal(2, songs.Count);
        
        // MP3 song enriched
        var mp3Song = songs[0];
        Assert.Equal("song1.mp3", mp3Song.Mp3FileName);
        Assert.Equal("song1.cdg", mp3Song.CdgFileName);
        
        // Video song enriched
        var videoSong = songs[1];
        Assert.Equal("video1.mp4", videoSong.VideoFileName);
        Assert.Equal(".mp4", videoSong.VideoExtension);
    }

    [Fact]
    public void EnrichSongsWithLibraryFiles_ShouldHandleSongNotFoundInLibrary()
    {
        // Arrange
        _mockConnectionManager.Setup(m => m.IsMainTab).Returns(true);
        
        var songId = Guid.NewGuid();
        var missingSongId = Guid.NewGuid();
        
        // Backend song that exists in library
        var backendSong = new Song
        {
            Id = songId,
            Artist = "Test Artist",
            Title = "Test Song",
            MediaType = MediaType.Mp3Cdg,
            Mp3FileName = null,
            CdgFileName = null
        };

        // Backend song that does NOT exist in library
        var missingBackendSong = new Song
        {
            Id = missingSongId,
            Artist = "Missing Artist",
            Title = "Missing Song",
            MediaType = MediaType.Video,
            VideoFileName = null
        };

        // Library only has one song
        var librarySong = new Song
        {
            Id = songId,
            Artist = "Test Artist",
            Title = "Test Song",
            MediaType = MediaType.Mp3Cdg,
            Mp3FileName = "test.mp3",
            CdgFileName = "test.cdg"
        };

        var songs = new List<Song> { backendSong, missingBackendSong };
        var libraryLookup = new Dictionary<Guid, Song> { { songId, librarySong } };

        // Act
        _service.EnrichSongsWithLibraryFiles(songs, libraryLookup);

        // Assert
        Assert.Equal(2, songs.Count);
        
        // First song enriched
        Assert.Equal("test.mp3", songs[0].Mp3FileName);
        
        // Second song NOT enriched (remains without file paths)
        Assert.Null(songs[1].VideoFileName);
    }

    [Fact]
    public void EnrichSongsWithLibraryFiles_WhenNotMainTab_ShouldNotEnrichSongs()
    {
        // Arrange - PRIVACY: Secondary tabs should not enrich (no file access)
        _mockConnectionManager.Setup(m => m.IsMainTab).Returns(false);
        
        var songId = Guid.NewGuid();
        
        var backendSong = new Song
        {
            Id = songId,
            Artist = "Test Artist",
            Title = "Test Song",
            MediaType = MediaType.Mp3Cdg,
            Mp3FileName = null,
            CdgFileName = null,
            AddedBySinger = "John Doe"
        };

        var librarySong = new Song
        {
            Id = songId,
            Artist = "Test Artist",
            Title = "Test Song",
            MediaType = MediaType.Mp3Cdg,
            Mp3FileName = "test.mp3",
            CdgFileName = "test.cdg"
        };

        var songs = new List<Song> { backendSong };
        var libraryLookup = new Dictionary<Guid, Song> { { songId, librarySong } };

        // Act
        _service.EnrichSongsWithLibraryFiles(songs, libraryLookup);

        // Assert - Song should NOT be enriched (privacy boundary)
        Assert.Single(songs);
        var song = songs[0];
        Assert.Null(song.Mp3FileName);
        Assert.Null(song.CdgFileName);
        Assert.Equal("John Doe", song.AddedBySinger);
    }

    [Fact]
    public void BuildLibraryLookup_ShouldCreateDictionaryBySongId()
    {
        // Arrange
        var song1 = new Song { Id = Guid.NewGuid(), Artist = "Artist 1", Title = "Song 1" };
        var song2 = new Song { Id = Guid.NewGuid(), Artist = "Artist 2", Title = "Song 2" };
        var songs = new List<Song> { song1, song2 };

        // Act
        var lookup = _service.BuildLibraryLookup(songs);

        // Assert
        Assert.Equal(2, lookup.Count);
        Assert.Equal(song1, lookup[song1.Id]);
        Assert.Equal(song2, lookup[song2.Id]);
    }
}
