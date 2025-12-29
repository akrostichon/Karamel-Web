using Bunit;
using Fluxor;
using Karamel.Web.Models;
using Karamel.Web.Pages;
using Karamel.Web.Store.Playlist;
using Karamel.Web.Store.Session;
using Microsoft.Extensions.DependencyInjection;
using Moq;
using Xunit;

namespace Karamel.Web.Tests;

/// <summary>
/// Unit tests for the Playlist page component.
/// Tests display of "Now Playing" and "Up Next" sections, remove/clear actions,
/// drag-drop reordering, and empty state handling.
/// </summary>
public class PlaylistPageTests : TestContext
{
    private readonly List<Song> _testSongs;
    private readonly Session _testSession;

    public PlaylistPageTests()
    {
        // Setup test songs
        _testSongs = new List<Song>
        {
            new Song 
            { 
                Id = Guid.NewGuid(),
                Artist = "Queen", 
                Title = "Bohemian Rhapsody", 
                Mp3FileName = "queen-bohemian.mp3", 
                CdgFileName = "queen-bohemian.cdg",
                AddedBySinger = "Alice"
            },
            new Song 
            { 
                Id = Guid.NewGuid(),
                Artist = "Beatles", 
                Title = "Let It Be", 
                Mp3FileName = "beatles-let.mp3", 
                CdgFileName = "beatles-let.cdg",
                AddedBySinger = "Bob"
            },
            new Song 
            { 
                Id = Guid.NewGuid(),
                Artist = "ABBA", 
                Title = "Dancing Queen", 
                Mp3FileName = "abba-dancing.mp3", 
                CdgFileName = "abba-dancing.cdg",
                AddedBySinger = "Alice"
            },
            new Song 
            { 
                Id = Guid.NewGuid(),
                Artist = "Elvis Presley", 
                Title = "Can't Help Falling in Love", 
                Mp3FileName = "elvis-cant.mp3", 
                CdgFileName = "elvis-cant.cdg",
                AddedBySinger = "Charlie"
            }
        };

        _testSession = new Session
        {
            SessionId = Guid.NewGuid(),
            LibraryPath = "C:\\Karaoke",
            AllowSingersToReorder = false
        };
    }

    [Fact]
    public void Component_WhenPlaylistIsEmpty_ShowsEmptyStateMessage()
    {
        // Arrange
        var playlistState = new PlaylistState { Queue = new Queue<Song>() };
        var sessionState = new SessionState 
        { 
            CurrentSession = _testSession,
            IsInitialized = true 
        };
        SetupFluxorWithStates(playlistState, sessionState);

        // Act
        var cut = RenderComponent<Playlist>();

        // Assert
        var emptyMessage = cut.Find(".alert-info");
        Assert.Contains("No songs in queue", emptyMessage.TextContent);
    }

    [Fact]
    public void Component_WhenQueueHasSongs_DisplaysNowPlayingSection()
    {
        // Arrange
        var queue = new Queue<Song>(_testSongs);
        var playlistState = new PlaylistState { Queue = queue };
        var sessionState = new SessionState 
        { 
            CurrentSession = _testSession,
            IsInitialized = true 
        };
        SetupFluxorWithStates(playlistState, sessionState);

        // Act
        var cut = RenderComponent<Playlist>();

        // Assert
        var nowPlaying = cut.Find(".now-playing");
        Assert.Contains("Queen", nowPlaying.TextContent);
        Assert.Contains("Bohemian Rhapsody", nowPlaying.TextContent);
        Assert.Contains("Alice", nowPlaying.TextContent);
    }

    [Fact]
    public void Component_WhenQueueHasMultipleSongs_DisplaysUpNextSection()
    {
        // Arrange
        var queue = new Queue<Song>(_testSongs);
        var playlistState = new PlaylistState { Queue = queue };
        var sessionState = new SessionState 
        { 
            CurrentSession = _testSession,
            IsInitialized = true 
        };
        SetupFluxorWithStates(playlistState, sessionState);

        // Act
        var cut = RenderComponent<Playlist>();

        // Assert
        var upNextSection = cut.Find(".up-next");
        var songRows = upNextSection.QuerySelectorAll(".song-item");
        
        // Should have 3 songs in "Up Next" (first one is in "Now Playing")
        Assert.Equal(3, songRows.Length);
        
        // Verify order
        Assert.Contains("Beatles", songRows[0].TextContent);
        Assert.Contains("Let It Be", songRows[0].TextContent);
        Assert.Contains("Bob", songRows[0].TextContent);
    }

    [Fact]
    public void Component_WhenQueueHasOneSong_DoesNotShowUpNextSection()
    {
        // Arrange
        var queue = new Queue<Song>(new[] { _testSongs[0] });
        var playlistState = new PlaylistState { Queue = queue };
        var sessionState = new SessionState 
        { 
            CurrentSession = _testSession,
            IsInitialized = true 
        };
        SetupFluxorWithStates(playlistState, sessionState);

        // Act
        var cut = RenderComponent<Playlist>();

        // Assert
        var upNextSections = cut.FindAll(".up-next");
        Assert.Empty(upNextSections);
    }

    [Fact]
    public void RemoveButton_WhenClicked_DispatchesRemoveSongAction()
    {
        // Arrange
        var queue = new Queue<Song>(_testSongs);
        var playlistState = new PlaylistState { Queue = queue };
        var sessionState = new SessionState 
        { 
            CurrentSession = _testSession,
            IsInitialized = true 
        };
        var mockDispatcher = SetupFluxorWithStates(playlistState, sessionState);

        var cut = RenderComponent<Playlist>();

        // Act
        var removeButtons = cut.FindAll("button.btn-remove");
        removeButtons[0].Click(); // Click first remove button (for 2nd song in queue)

        // Assert - The first button is for the second song in the queue (_testSongs[1])
        mockDispatcher.Verify(d => d.Dispatch(It.Is<RemoveSongAction>(
            a => a.SongId == _testSongs[1].Id)), Times.Once);
    }

    [Fact]
    public void ClearPlaylistButton_WhenClickedAndConfirmed_DispatchesClearPlaylistAction()
    {
        // Arrange
        var queue = new Queue<Song>(_testSongs);
        var playlistState = new PlaylistState { Queue = queue };
        var sessionState = new SessionState 
        { 
            CurrentSession = _testSession,
            IsInitialized = true 
        };
        var mockDispatcher = SetupFluxorWithStates(playlistState, sessionState);

        var cut = RenderComponent<Playlist>();

        // Mock window.confirm to return true
        JSInterop.Setup<bool>("confirm", _ => true).SetResult(true);

        // Act
        var clearButton = cut.Find("button.btn-clear-playlist");
        clearButton.Click();

        // Assert
        mockDispatcher.Verify(d => d.Dispatch(It.IsAny<ClearPlaylistAction>()), Times.Once);
    }

    [Fact]
    public void ClearPlaylistButton_WhenClickedAndCancelled_DoesNotDispatchAction()
    {
        // Arrange
        var queue = new Queue<Song>(_testSongs);
        var playlistState = new PlaylistState { Queue = queue };
        var sessionState = new SessionState 
        { 
            CurrentSession = _testSession,
            IsInitialized = true 
        };
        var mockDispatcher = SetupFluxorWithStates(playlistState, sessionState);

        var cut = RenderComponent<Playlist>();

        // Mock window.confirm to return false
        JSInterop.Setup<bool>("confirm", _ => true).SetResult(false);

        // Act
        var clearButton = cut.Find("button.btn-clear-playlist");
        clearButton.Click();

        // Assert
        mockDispatcher.Verify(d => d.Dispatch(It.IsAny<ClearPlaylistAction>()), Times.Never);
    }

    [Fact]
    public void Component_WhenReorderingDisabled_DragDropNotEnabled()
    {
        // Arrange
        var queue = new Queue<Song>(_testSongs);
        var playlistState = new PlaylistState { Queue = queue };
        var session = _testSession with { AllowSingersToReorder = false };
        var sessionState = new SessionState 
        { 
            CurrentSession = session,
            IsInitialized = true 
        };
        SetupFluxorWithStates(playlistState, sessionState);

        // Act
        var cut = RenderComponent<Playlist>();

        // Assert
        var upNextSection = cut.Find(".up-next");
        var songItems = upNextSection.QuerySelectorAll(".song-item");
        
        foreach (var item in songItems)
        {
            // Draggable attribute should not be set or be false
            var draggable = item.GetAttribute("draggable");
            Assert.True(draggable == null || draggable == "false");
        }
    }

    [Fact]
    public void Component_WhenReorderingEnabled_DragDropIsEnabled()
    {
        // Arrange
        var queue = new Queue<Song>(_testSongs);
        var playlistState = new PlaylistState { Queue = queue };
        var session = _testSession with { AllowSingersToReorder = true };
        var sessionState = new SessionState 
        { 
            CurrentSession = session,
            IsInitialized = true 
        };
        SetupFluxorWithStates(playlistState, sessionState);

        // Act
        var cut = RenderComponent<Playlist>();

        // Assert
        var upNextSection = cut.Find(".up-next");
        var songItems = upNextSection.QuerySelectorAll(".song-item");
        
        foreach (var item in songItems)
        {
            // Draggable attribute should be true
            var draggable = item.GetAttribute("draggable");
            Assert.Equal("true", draggable);
        }
    }

    [Fact]
    public void Component_DisplaysQueuePositionNumbers()
    {
        // Arrange
        var queue = new Queue<Song>(_testSongs);
        var playlistState = new PlaylistState { Queue = queue };
        var sessionState = new SessionState 
        { 
            CurrentSession = _testSession,
            IsInitialized = true 
        };
        SetupFluxorWithStates(playlistState, sessionState);

        // Act
        var cut = RenderComponent<Playlist>();

        // Assert
        var upNextSection = cut.Find(".up-next");
        var songItems = upNextSection.QuerySelectorAll(".song-item");
        
        // Check position numbers (2, 3, 4 - since first song is "Now Playing")
        Assert.Contains("#2", songItems[0].TextContent);
        Assert.Contains("#3", songItems[1].TextContent);
        Assert.Contains("#4", songItems[2].TextContent);
    }

    [Fact]
    public void Component_ShowsSingerNameForEachSong()
    {
        // Arrange
        var queue = new Queue<Song>(_testSongs);
        var playlistState = new PlaylistState { Queue = queue };
        var sessionState = new SessionState 
        { 
            CurrentSession = _testSession,
            IsInitialized = true 
        };
        SetupFluxorWithStates(playlistState, sessionState);

        // Act
        var cut = RenderComponent<Playlist>();

        // Assert - Check "Now Playing"
        var nowPlaying = cut.Find(".now-playing");
        Assert.Contains("Alice", nowPlaying.TextContent);

        // Assert - Check "Up Next" items
        var upNextSection = cut.Find(".up-next");
        Assert.Contains("Bob", upNextSection.TextContent);
        Assert.Contains("Alice", upNextSection.TextContent); // ABBA song
        Assert.Contains("Charlie", upNextSection.TextContent);
    }

    private Mock<IDispatcher> SetupFluxorWithStates(PlaylistState playlistState, SessionState sessionState)
    {
        var mockDispatcher = new Mock<IDispatcher>();
        var mockPlaylistState = new Mock<IState<PlaylistState>>();
        var mockSessionState = new Mock<IState<SessionState>>();
        var mockActionSubscriber = new Mock<IActionSubscriber>();
        
        mockPlaylistState.Setup(s => s.Value).Returns(playlistState);
        mockSessionState.Setup(s => s.Value).Returns(sessionState);

        Services.AddSingleton(mockDispatcher.Object);
        Services.AddSingleton(mockPlaylistState.Object);
        Services.AddSingleton(mockSessionState.Object);
        Services.AddSingleton(mockActionSubscriber.Object);

        return mockDispatcher;
    }
}
