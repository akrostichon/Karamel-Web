using Bunit;
using Karamel.Web.Components;
using Karamel.Web.Contracts;
using Karamel.Web.Store.Playlist;
using Fluxor;
using Moq;
using Microsoft.Extensions.DependencyInjection;

namespace Karamel.Web.Tests;

/// <summary>
/// Unit tests for UpNextList component duration rendering.
/// Verifies that duration is displayed for songs with DurationSeconds > 0,
/// and that no duration element is rendered when DurationSeconds == 0.
/// </summary>
public class UpNextListTests : TestContext
{
    private void SetupPlaylistState(PlaylistState playlistState)
    {
        var mockPlaylistState = new Mock<IState<PlaylistState>>();
        mockPlaylistState.Setup(s => s.Value).Returns(playlistState);

        var mockDispatcher = new Mock<IDispatcher>();
        var mockActionSubscriber = new Mock<IActionSubscriber>();

        Services.AddSingleton(mockPlaylistState.Object);
        Services.AddSingleton(mockDispatcher.Object);
        Services.AddSingleton(mockActionSubscriber.Object);
    }

    [Fact]
    public void QueueItem_WithDuration215_ShowsFormattedDuration()
    {
        // Arrange
        var item = new PlaylistItemDto(
            Id: Guid.NewGuid().ToString(),
            SongId: Guid.NewGuid().ToString(),
            Artist: "Test Artist",
            Title: "Test Song",
            SingerName: null,
            Position: 0,
            Status: (int)SongStatus.Queued,
            DurationSeconds: 215
        );

        var playlistState = new PlaylistState { Items = new List<PlaylistItemDto> { item } };
        SetupPlaylistState(playlistState);

        // Act
        var cut = RenderComponent<UpNextList>();

        // Assert
        var durationEl = cut.Find(".up-next-song-duration");
        Assert.Equal("3:35", durationEl.TextContent.Trim());
    }

    [Fact]
    public void QueueItem_WithZeroDuration_DurationElementAbsent()
    {
        // Arrange
        var item = new PlaylistItemDto(
            Id: Guid.NewGuid().ToString(),
            SongId: Guid.NewGuid().ToString(),
            Artist: "Test Artist",
            Title: "Test Song",
            SingerName: null,
            Position: 0,
            Status: (int)SongStatus.Queued,
            DurationSeconds: 0
        );

        var playlistState = new PlaylistState { Items = new List<PlaylistItemDto> { item } };
        SetupPlaylistState(playlistState);

        // Act
        var cut = RenderComponent<UpNextList>();

        // Assert
        Assert.Throws<ElementNotFoundException>(() => cut.Find(".up-next-song-duration"));
    }

    [Fact]
    public void NowPlayingCard_WithDuration215_ShowsFormattedDuration()
    {
        // Arrange
        var currentSong = new PlaylistItemDto(
            Id: Guid.NewGuid().ToString(),
            SongId: Guid.NewGuid().ToString(),
            Artist: "Test Artist",
            Title: "Test Song",
            SingerName: null,
            Position: 0,
            Status: (int)SongStatus.NowPlaying,
            DurationSeconds: 215
        );

        var playlistState = new PlaylistState
        {
            CurrentSong = currentSong,
            Items = new List<PlaylistItemDto>()
        };
        SetupPlaylistState(playlistState);

        // Act
        var cut = RenderComponent<UpNextList>();

        // Assert
        var durationEl = cut.Find(".up-next-song-duration");
        Assert.Equal("3:35", durationEl.TextContent.Trim());
    }

    [Fact]
    public void NowPlayingCard_WithZeroDuration_DurationElementAbsent()
    {
        // Arrange
        var currentSong = new PlaylistItemDto(
            Id: Guid.NewGuid().ToString(),
            SongId: Guid.NewGuid().ToString(),
            Artist: "Test Artist",
            Title: "Test Song",
            SingerName: null,
            Position: 0,
            Status: (int)SongStatus.NowPlaying,
            DurationSeconds: 0
        );

        var playlistState = new PlaylistState
        {
            CurrentSong = currentSong,
            Items = new List<PlaylistItemDto>()
        };
        SetupPlaylistState(playlistState);

        // Act
        var cut = RenderComponent<UpNextList>();

        // Assert
        Assert.Throws<ElementNotFoundException>(() => cut.Find(".up-next-song-duration"));
    }
}
