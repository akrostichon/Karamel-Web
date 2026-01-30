using System;
using System.Text.Json;
using Karamel.Web.Contracts;
using Karamel.Web.Models;
using Xunit;

namespace Karamel.Web.Tests;

/// <summary>
/// Tests for SongDto conversion methods, particularly handling missing optional fields
/// </summary>
public class SongDtoConverterTests
{
    [Fact]
    public void ConvertJsonToSong_WithAllFields_PopulatesAllProperties()
    {
        // Arrange
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

        // Assert
        Assert.Equal(Guid.Parse("12345678-1234-1234-1234-123456789012"), song.Id);
        Assert.Equal("Test Artist", song.Artist);
        Assert.Equal("Test Title", song.Title);
        Assert.Equal("test.mp3", song.Mp3FileName);
        Assert.Equal("test.cdg", song.CdgFileName);
        Assert.Equal("/path/to/song", song.Path);
        Assert.Equal("/full/path/to/song", song.FullPath);
        Assert.Equal(SongSourceType.Directory, song.SourceType);
        Assert.Equal("John Doe", song.AddedBySinger);
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
    public void ConvertJsonToSong_WithZipSourceType_ParsesCorrectly()
    {
        // Arrange
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

        // Assert
        Assert.Equal(SongSourceType.Zip, song.SourceType);
        Assert.Equal("archive.zip", song.ZipFileName);
        Assert.Equal("songs/song.mp3", song.ZipEntryMp3Path);
        Assert.Equal("songs/song.cdg", song.ZipEntryCdgPath);
        Assert.Equal("/path/to/archive.zip", song.ZipFilePath);
    }
}
