using Karamel.Web.Models;
using Xunit;

namespace Karamel.Web.Tests;

public class SongModelTests
{
    [Fact]
    public void VideoSong_ShouldRequireVideoFileName()
    {
        // Arrange & Act
        var videoSong = new Song
        {
            Id = Guid.NewGuid(),
            Artist = "Test Artist",
            Title = "Test Video",
            MediaType = MediaType.Video,
            VideoFileName = "test.mp4",
            VideoExtension = ".mp4"
        };

        // Assert
        Assert.True(videoSong.IsValid());
    }

    [Fact]
    public void VideoSong_WithoutVideoFileName_ShouldBeInvalid()
    {
        // Arrange & Act
        var videoSong = new Song
        {
            Id = Guid.NewGuid(),
            Artist = "Test Artist",
            Title = "Test Video",
            MediaType = MediaType.Video,
            VideoFileName = null,
            VideoExtension = ".mp4"
        };

        // Assert
        Assert.False(videoSong.IsValid());
    }

    [Fact]
    public void Mp3CdgSong_ShouldRequireBothMp3AndCdgFileNames()
    {
        // Arrange & Act
        var mp3CdgSong = new Song
        {
            Id = Guid.NewGuid(),
            Artist = "Test Artist",
            Title = "Test Song",
            MediaType = MediaType.Mp3Cdg,
            Mp3FileName = "test.mp3",
            CdgFileName = "test.cdg"
        };

        // Assert
        Assert.True(mp3CdgSong.IsValid());
    }

    [Fact]
    public void Mp3CdgSong_WithoutMp3FileName_ShouldBeInvalid()
    {
        // Arrange & Act
        var mp3CdgSong = new Song
        {
            Id = Guid.NewGuid(),
            Artist = "Test Artist",
            Title = "Test Song",
            MediaType = MediaType.Mp3Cdg,
            Mp3FileName = null,
            CdgFileName = "test.cdg"
        };

        // Assert
        Assert.False(mp3CdgSong.IsValid());
    }

    [Fact]
    public void Mp3CdgSong_WithoutCdgFileName_ShouldBeInvalid()
    {
        // Arrange & Act
        var mp3CdgSong = new Song
        {
            Id = Guid.NewGuid(),
            Artist = "Test Artist",
            Title = "Test Song",
            MediaType = MediaType.Mp3Cdg,
            Mp3FileName = "test.mp3",
            CdgFileName = null
        };

        // Assert
        Assert.False(mp3CdgSong.IsValid());
    }

    [Fact]
    public void GetPrimaryFileName_ForMp3CdgSong_ShouldReturnMp3FileName()
    {
        // Arrange
        var mp3CdgSong = new Song
        {
            Id = Guid.NewGuid(),
            Artist = "Test Artist",
            Title = "Test Song",
            MediaType = MediaType.Mp3Cdg,
            Mp3FileName = "test.mp3",
            CdgFileName = "test.cdg"
        };

        // Act
        var fileName = mp3CdgSong.GetPrimaryFileName();

        // Assert
        Assert.Equal("test.mp3", fileName);
    }

    [Fact]
    public void GetPrimaryFileName_ForVideoSong_ShouldReturnVideoFileName()
    {
        // Arrange
        var videoSong = new Song
        {
            Id = Guid.NewGuid(),
            Artist = "Test Artist",
            Title = "Test Video",
            MediaType = MediaType.Video,
            VideoFileName = "test.mp4",
            VideoExtension = ".mp4"
        };

        // Act
        var fileName = videoSong.GetPrimaryFileName();

        // Assert
        Assert.Equal("test.mp4", fileName);
    }

    [Fact]
    public void GetPrimaryFileName_ForInvalidSong_ShouldThrowInvalidOperationException()
    {
        // Arrange
        var invalidSong = new Song
        {
            Id = Guid.NewGuid(),
            Artist = "Test Artist",
            Title = "Test Song",
            MediaType = MediaType.Mp3Cdg,
            Mp3FileName = null,
            CdgFileName = null
        };

        // Act & Assert
        var exception = Assert.Throws<InvalidOperationException>(() => invalidSong.GetPrimaryFileName());
        Assert.Equal("Cannot get primary file name for invalid song", exception.Message);
    }
}
