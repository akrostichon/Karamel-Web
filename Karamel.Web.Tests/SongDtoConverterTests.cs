using System.Text.Json;
using Karamel.Web.Contracts;
using Karamel.Web.Models;

namespace Karamel.Web.Tests;

/// <summary>
/// Tests for SongDto conversion methods, particularly handling missing optional fields
/// </summary>
public class SongDtoConverterTests
{
    [Fact]
    public void ConvertJsonToSong_WithAllFields_PopulatesBasicFieldsOnly()
    {
        // Arrange - OLD TEST BEHAVIOR: Expected paths to be deserialized
        // NEW BEHAVIOR: Paths are NEVER deserialized from backend (privacy)
        var json = """
        {
            "id": "12345678-1234-1234-1234-123456789012",
            "artist": "Test Artist",
            "title": "Test Title",
            "mp3FileName": "test.mp3",
            "cdgFileName": "test.cdg",
            "path": "/path/to/song",
            "fullPath": "/full/path/to/song",
            "sourceType": "Directory",
            "addedBySinger": "John Doe"
        }
        """;
        var jsonElement = JsonDocument.Parse(json).RootElement;

        // Act
        var song = SongConverters.ConvertJsonToSong(jsonElement);

        // Assert - Only basic fields deserialized, paths ignored for privacy
        Assert.Equal(Guid.Parse("12345678-1234-1234-1234-123456789012"), song.Id);
        Assert.Equal("Test Artist", song.Artist);
        Assert.Equal("Test Title", song.Title);
        // PRIVACY: File paths never deserialized from backend
        Assert.Equal(string.Empty, song.Mp3FileName);
        Assert.Equal(string.Empty, song.CdgFileName);
        Assert.Null(song.Path);
        Assert.Null(song.FullPath);
        Assert.Equal(SongSourceType.Directory, song.SourceType);  // Default
        Assert.Equal("John Doe", song.AddedBySinger);  // Singer name IS deserialized for playlist display
    }

    [Fact]
    public void ConvertJsonToSong_WithMissingFileNames_UsesEmptyStrings()
    {
        // Arrange - This simulates the backend SongListItemDto which doesn't include file names
        var json = """
        {
            "id": "12345678-1234-1234-1234-123456789012",
            "artist": "Test Artist",
            "title": "Test Title"
        }
        """;
        var jsonElement = JsonDocument.Parse(json).RootElement;

        // Act
        var song = SongConverters.ConvertJsonToSong(jsonElement);

        // Assert
        Assert.Equal(Guid.Parse("12345678-1234-1234-1234-123456789012"), song.Id);
        Assert.Equal("Test Artist", song.Artist);
        Assert.Equal("Test Title", song.Title);
        Assert.Equal(string.Empty, song.Mp3FileName);
        Assert.Equal(string.Empty, song.CdgFileName);
        Assert.Null(song.Path);
        Assert.Null(song.FullPath);
        Assert.Equal(SongSourceType.Directory, song.SourceType);
        Assert.Null(song.AddedBySinger);
    }

    [Fact]
    public void ConvertJsonToSong_WithNullFileNames_UsesEmptyStrings()
    {
        // Arrange
        var json = """
        {
            "id": "12345678-1234-1234-1234-123456789012",
            "artist": "Test Artist",
            "title": "Test Title",
            "mp3FileName": null,
            "cdgFileName": null
        }
        """;
        var jsonElement = JsonDocument.Parse(json).RootElement;

        // Act
        var song = SongConverters.ConvertJsonToSong(jsonElement);

        // Assert
        Assert.Equal(string.Empty, song.Mp3FileName);
        Assert.Equal(string.Empty, song.CdgFileName);
    }

    [Fact]
    public void ConvertJsonToSong_WithBackendSongListItemDto_ConvertsSuccessfully()
    {
        // Arrange - Simulates actual backend response from LibraryController.GetPage
        var json = """
        {
            "id": "a1b2c3d4-e5f6-7890-abcd-ef1234567890",
            "sessionId": "12345678-1234-1234-1234-123456789012",
            "artist": "Backend Artist",
            "title": "Backend Title",
            "metadataJson": null,
            "addedAt": "2026-01-30T20:00:00Z"
        }
        """;
        var jsonElement = JsonDocument.Parse(json).RootElement;

        // Act - Should not throw KeyNotFoundException
        var song = SongConverters.ConvertJsonToSong(jsonElement);

        // Assert
        Assert.Equal(Guid.Parse("a1b2c3d4-e5f6-7890-abcd-ef1234567890"), song.Id);
        Assert.Equal("Backend Artist", song.Artist);
        Assert.Equal("Backend Title", song.Title);
        Assert.Equal(string.Empty, song.Mp3FileName);
        Assert.Equal(string.Empty, song.CdgFileName);
    }

    [Fact]
    public void ConvertJsonToSong_WithZipSourceType_IgnoresZipPaths()
    {
        // Arrange - OLD TEST BEHAVIOR: Expected ZIP paths to be deserialized
        // NEW BEHAVIOR: All paths (including ZIP) are NEVER deserialized from backend (privacy)
        var json = """
        {
            "id": "12345678-1234-1234-1234-123456789012",
            "artist": "Zip Artist",
            "title": "Zip Title",
            "mp3FileName": "song.mp3",
            "cdgFileName": "song.cdg",
            "sourceType": "Zip",
            "zipFileName": "archive.zip",
            "zipEntryMp3Path": "songs/song.mp3",
            "zipEntryCdgPath": "songs/song.cdg",
            "zipFilePath": "/path/to/archive.zip"
        }
        """;
        var jsonElement = JsonDocument.Parse(json).RootElement;

        // Act
        var song = SongConverters.ConvertJsonToSong(jsonElement);

        // Assert - PRIVACY: All file paths ignored, including ZIP metadata
        Assert.Equal(Guid.Parse("12345678-1234-1234-1234-123456789012"), song.Id);
        Assert.Equal("Zip Artist", song.Artist);
        Assert.Equal("Zip Title", song.Title);
        Assert.Equal(SongSourceType.Directory, song.SourceType);  // Default (not Zip)
        Assert.Null(song.ZipFileName);
        Assert.Null(song.ZipEntryMp3Path);
        Assert.Null(song.ZipEntryCdgPath);
        Assert.Null(song.ZipFilePath);
    }

    [Fact]
    public void ConvertSongToUploadDto_ExcludesFilePaths()
    {
        // Arrange
        var song = new Song
        {
            Id = Guid.NewGuid(),
            Artist = "Privacy Artist",
            Title = "Secret Song",
            Mp3FileName = "secret.mp3",
            CdgFileName = "secret.cdg",
            Path = "/private/local/path",
            FullPath = "C:\\Private\\Local\\Full\\Path",
            ZipFilePath = "C:\\Private\\archive.zip",
            ZipFileName = "archive.zip",
            ZipEntryMp3Path = "internal/path.mp3",
            ZipEntryCdgPath = "internal/path.cdg",
            SourceType = SongSourceType.Zip
        };

        // Act
        var dto = SongConverters.ConvertSongToUploadDto(song);

        // Assert - DTO should only have safe fields
        Assert.Equal(song.Id.ToString(), dto.Id);
        Assert.Equal("Privacy Artist", dto.Artist);
        Assert.Equal("Secret Song", dto.Title);
        Assert.Null(dto.MetadataJson); // Placeholder for future use
        
        // Verify DTO type doesn't expose file path properties
        var dtoType = dto.GetType();
        Assert.Null(dtoType.GetProperty("Path"));
        Assert.Null(dtoType.GetProperty("FullPath"));
        Assert.Null(dtoType.GetProperty("Mp3FileName"));
        Assert.Null(dtoType.GetProperty("CdgFileName"));
        Assert.Null(dtoType.GetProperty("ZipFilePath"));
        Assert.Null(dtoType.GetProperty("ZipFileName"));
    }

    [Fact]
    public void ConvertSongToUploadDto_IncludesBasicFields()
    {
        // Arrange
        var songId = Guid.NewGuid();
        var song = new Song
        {
            Id = songId,
            Artist = "Test Artist",
            Title = "Test Title",
            Mp3FileName = "test.mp3",
            CdgFileName = "test.cdg"
        };

        // Act
        var dto = SongConverters.ConvertSongToUploadDto(song);

        // Assert
        Assert.Equal(songId.ToString(), dto.Id);
        Assert.Equal("Test Artist", dto.Artist);
        Assert.Equal("Test Title", dto.Title);
        Assert.NotNull(dto); // Verify object created successfully
    }

    [Fact]
    public void ConvertSongToUploadDto_SetsMetadataJsonNull()
    {
        // Arrange
        var song = new Song
        {
            Id = Guid.NewGuid(),
            Artist = "Artist",
            Title = "Title",
            Mp3FileName = "song.mp3",
            CdgFileName = "song.cdg"
        };

        // Act
        var dto = SongConverters.ConvertSongToUploadDto(song);

        // Assert
        Assert.Null(dto.MetadataJson); // Reserved for future legitimate metadata (duration, genre)
    }

    [Fact]
    public void ConvertJsonToSong_WithMissingPaths_CreatesEmptyFilePaths()
    {
        // Arrange - Backend returns minimal data (Artist, Title only)
        var json = """
        {
            "id": "12345678-1234-1234-1234-123456789012",
            "artist": "Artist from Backend",
            "title": "Title from Backend"
        }
        """;
        var jsonElement = JsonDocument.Parse(json).RootElement;

        // Act
        var song = SongConverters.ConvertJsonToSong(jsonElement);

        // Assert - File paths should be empty/null (privacy protection)
        Assert.Equal(Guid.Parse("12345678-1234-1234-1234-123456789012"), song.Id);
        Assert.Equal("Artist from Backend", song.Artist);
        Assert.Equal("Title from Backend", song.Title);
        Assert.Equal(string.Empty, song.Mp3FileName);
        Assert.Equal(string.Empty, song.CdgFileName);
        Assert.Null(song.Path);
        Assert.Null(song.FullPath);
        Assert.Null(song.ZipFilePath);
        Assert.Null(song.ZipFileName);
        Assert.Null(song.ZipEntryMp3Path);
        Assert.Null(song.ZipEntryCdgPath);
        Assert.Equal(SongSourceType.Directory, song.SourceType); // Default
    }

    [Fact]
    public void ConvertJsonToSong_WithPathsInJson_StillIgnoresThem()
    {
        // Arrange - Even if backend accidentally returns paths, they should be ignored
        var json = """
        {
            "id": "12345678-1234-1234-1234-123456789012",
            "artist": "Artist",
            "title": "Title",
            "path": "/this/should/be/ignored",
            "fullPath": "/this/should/also/be/ignored",
            "mp3FileName": "ignored.mp3",
            "cdgFileName": "ignored.cdg",
            "zipFilePath": "/also/ignored.zip"
        }
        """;
        var jsonElement = JsonDocument.Parse(json).RootElement;

        // Act
        var song = SongConverters.ConvertJsonToSong(jsonElement);

        // Assert - All paths should be empty/null regardless of JSON content
        Assert.Equal(string.Empty, song.Mp3FileName);
        Assert.Equal(string.Empty, song.CdgFileName);
        Assert.Null(song.Path);
        Assert.Null(song.FullPath);
        Assert.Null(song.ZipFilePath);
    }
}
