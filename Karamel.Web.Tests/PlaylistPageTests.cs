using Bunit;
using Karamel.Web.Models;
using Karamel.Web.Tests.TestHelpers;
using Karamel.Web.Contracts;
using Karamel.Web.Pages;
using Karamel.Web.Store.Playlist;
using Karamel.Web.Store.Session;
using Moq;

namespace Karamel.Web.Tests;

/// <summary>
/// Unit tests for the Playlist page component.
/// Tests display of "Now Playing" and "Up Next" sections, remove/clear actions,
/// drag-drop reordering, and empty state handling.
/// </summary>
public class PlaylistPageTests : SessionTestBase
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
            AllowSingersToReorder = false
        };
    }

    [Fact]
    public void Component_WhenPlaylistIsEmpty_ShowsEmptyStateMessage()
    {
        // Arrange
        var playlistState = new PlaylistState { Items = new List<PlaylistItemDto>() };
        var sessionState = new SessionState 
        { 
            CurrentSession = _testSession,
            IsInitialized = true 
        };
        SetupTestWithSession(sessionState, playlistState, view: "playlist");

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
        var playlistState = new PlaylistState 
        { 
            Items = TestDataFactory.CreatePlaylistItems(queue.ToArray()),
            CurrentSong = TestDataFactory.CreatePlaylistItem(_testSongs[0]) // Set CurrentSong to first song
        };
        var sessionState = new SessionState 
        { 
            CurrentSession = _testSession,
            IsInitialized = true 
        };
        SetupTestWithSession(sessionState, playlistState, view: "playlist");

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
        var playlistState = new PlaylistState { Items = TestDataFactory.CreatePlaylistItems(queue.ToArray()) };
        var sessionState = new SessionState 
        { 
            CurrentSession = _testSession,
            IsInitialized = true 
        };
        SetupTestWithSession(sessionState, playlistState, view: "playlist");

        // Act
        var cut = RenderComponent<Playlist>();

        // Assert
        var upNextSection = cut.Find(".up-next");
        var songRows = upNextSection.QuerySelectorAll(".song-item");
        
        // Should have 4 songs in "Up Next" (no CurrentSong set, so all in queue)
        Assert.Equal(4, songRows.Length);
        
        // Verify first song is at index 0
        Assert.Contains("Queen", songRows[0].TextContent);
        Assert.Contains("Bohemian Rhapsody", songRows[0].TextContent);
        Assert.Contains("Alice", songRows[0].TextContent);
    }

    [Fact]
    public void Component_WhenQueueHasOneSong_DoesNotShowUpNextSection()
    {
        // Arrange - Simulate state after NextSongAction: song moved from Queue to CurrentSong
        var queue = new Queue<Song>(); // Empty queue after song taken out
        var playlistState = new PlaylistState 
        { 
            Items = TestDataFactory.CreatePlaylistItems(queue.ToArray()),
            CurrentSong = TestDataFactory.CreatePlaylistItem(_testSongs[0]) // Song is now current
        };
        var sessionState = new SessionState 
        { 
            CurrentSession = _testSession,
            IsInitialized = true 
        };
        SetupTestWithSession(sessionState, playlistState, view: "playlist");

        // Act
        var cut = RenderComponent<Playlist>();

        // Assert - Now Playing should show, but Up Next should not (queue is empty)
        var upNextSections = cut.FindAll(".up-next");
        Assert.Empty(upNextSections);
    }

    [Fact]
    public void RemoveButton_WhenClicked_DispatchesRemoveSongAction()
    {
        // Arrange
        var queue = new Queue<Song>(_testSongs);
        var playlistState = new PlaylistState { Items = TestDataFactory.CreatePlaylistItems(queue.ToArray()) };
        var sessionState = new SessionState 
        { 
            CurrentSession = _testSession,
            IsInitialized = true 
        };
        var (_, mockDispatcher, _) = SetupTestWithSession(sessionState, playlistState, view: "playlist");

        var cut = RenderComponent<Playlist>();

        // Act
        var removeButtons = cut.FindAll("button.btn-remove");
        removeButtons[0].Click(); // Click first remove button (for first song in queue)

        // Assert - RemoveSongAction now receives the ItemId (playlist item ID), not the Song ID
        var firstItemId = Guid.Parse(playlistState.Items[0].Id);
        mockDispatcher.Verify(d => d.Dispatch(It.Is<RemoveSongAction>(
            a => a.SongId == firstItemId)), Times.Once);
    }

    [Fact(Skip = "bUnit limitation: async @onclick handlers with JSRuntime.InvokeAsync (confirm dialog) don't complete in tests. The ClearPlaylistAction → PlaylistEffects.HandleClearPlaylistAction → SessionService.ClearQueueAsync flow is verified via backend integration tests.")]
    public async Task ClearPlaylistButton_WhenClickedAndConfirmed_DispatchesClearPlaylistAction()
    {
        // Arrange
        var queue = new Queue<Song>(_testSongs);
        var playlistState = new PlaylistState { Items = TestDataFactory.CreatePlaylistItems(queue.ToArray()) };
        var sessionState = new SessionState 
        { 
            CurrentSession = _testSession,
            IsInitialized = true 
        };
        var (_, mockDispatcher, _) = SetupTestWithSession(sessionState, playlistState, view: "playlist");

        // Mock window.confirm to return true
        JSInterop.Mode = JSRuntimeMode.Loose;
        var confirmHandler = JSInterop.Setup<bool>("confirm", "Are you sure you want to clear all queued songs? The currently playing song will not be affected.");
        confirmHandler.SetResult(true);

        var cut = RenderComponent<Playlist>();

        // Act - Find and click the clear button
        var clearButton = cut.Find("button.btn-clear-playlist");
        
        // Use ClickAsync to trigger async onclick handler
        await cut.InvokeAsync(async () => 
        {
            await clearButton.ClickAsync(new Microsoft.AspNetCore.Components.Web.MouseEventArgs());
        });

        // Give time for async operations
        await Task.Delay(100);

        // Assert - Verify ClearPlaylistAction was dispatched (effect will call SessionService.ClearQueueAsync)
        mockDispatcher.Verify(d => d.Dispatch(It.IsAny<ClearPlaylistAction>()), Times.Once);
    }

    [Fact(Skip = "bUnit limitation: async @onclick handlers with JSRuntime.InvokeAsync (confirm dialog) don't complete in tests. The ClearPlaylistAction → PlaylistEffects.HandleClearPlaylistAction → SessionService.ClearQueueAsync flow is verified via backend integration tests.")]
    public async Task ClearPlaylistButton_WhenClickedAndCancelled_DoesNotDispatchAction()
    {
        // Arrange
        var queue = new Queue<Song>(_testSongs);
        var playlistState = new PlaylistState { Items = TestDataFactory.CreatePlaylistItems(queue.ToArray()) };
        var sessionState = new SessionState 
        { 
            CurrentSession = _testSession,
            IsInitialized = true 
        };
        var (_, mockDispatcher, _) = SetupTestWithSession(sessionState, playlistState, view: "playlist");

        // Mock window.confirm to return false
        JSInterop.Mode = JSRuntimeMode.Loose;
        JSInterop.Setup<bool>("confirm", "Are you sure you want to clear all queued songs? The currently playing song will not be affected.")
            .SetResult(false);

        var cut = RenderComponent<Playlist>();

        // Act - Find and click the clear button
        var clearButton = cut.Find("button.btn-clear-playlist");
        await clearButton.ClickAsync(new Microsoft.AspNetCore.Components.Web.MouseEventArgs());

        // Wait for async operations to complete
        await Task.Delay(50);

        // Assert - Verify ClearPlaylistAction was NOT dispatched
        mockDispatcher.Verify(d => d.Dispatch(It.IsAny<ClearPlaylistAction>()), Times.Never);
    }

    [Fact]
    public void Component_WhenReorderingDisabled_DragDropNotEnabled()
    {
        // Arrange
        var queue = new Queue<Song>(_testSongs);
        var playlistState = new PlaylistState { Items = TestDataFactory.CreatePlaylistItems(queue.ToArray()) };
        var session = _testSession with { AllowSingersToReorder = false };
        var sessionState = new SessionState 
        { 
            CurrentSession = session,
            IsInitialized = true 
        };
        SetupTestWithSession(sessionState, playlistState, view: "playlist");

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
        var playlistState = new PlaylistState { Items = TestDataFactory.CreatePlaylistItems(queue.ToArray()) };
        var session = _testSession with { AllowSingersToReorder = true };
        var sessionState = new SessionState 
        { 
            CurrentSession = session,
            IsInitialized = true 
        };
        SetupTestWithSession(sessionState, playlistState, view: "playlist");

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

    // NEW: Role-based drag & drop tests
    [Fact(Skip = "Requires sessionStorage role check implementation - IsAdminTabAsync() not yet implemented")]
    public void Playlist_WithAdminToken_EnablesDragDrop()
    {
        // Arrange: Mock admin role in sessionStorage
        var queue = new Queue<Song>(_testSongs);
        var playlistState = new PlaylistState { Items = TestDataFactory.CreatePlaylistItems(queue.ToArray()) };
        var session = _testSession with { AllowSingersToReorder = false }; // Even with false, admin can reorder
        var sessionState = new SessionState 
        { 
            CurrentSession = session,
            IsInitialized = true 
        };
        SetupTestWithSession(sessionState, playlistState, view: "playlist");
        
        // Mock sessionStorage to return "admin" role
        // JSInterop.SetupAsync<string?>("sessionStorage.getItem", $"karamel-session-{session.SessionId}-role")
        //     .SetResult("admin");

        // Act
        // var cut = RenderComponent<Playlist>(parameters => parameters
        //     .Add(p => p.SessionParam, session.SessionId.ToString())
        //     .Add(p => p.TokenParam, "admin-token"));

        // Assert: Verify draggable=true attribute on playlist items
        // var upNextSection = cut.Find(".up-next");
        // var songItems = upNextSection.QuerySelectorAll(".song-item");
        // foreach (var item in songItems)
        // {
        //     var draggable = item.GetAttribute("draggable");
        //     Assert.Equal("true", draggable);
        // }
    }

    [Fact(Skip = "Requires sessionStorage role check implementation - IsAdminTabAsync() not yet implemented")]
    public void Playlist_WithSingerToken_AndAllowSingersToReorderFalse_DisablesDragDrop()
    {
        // Arrange: Mock singer role and AllowSingersToReorder=false
        var queue = new Queue<Song>(_testSongs);
        var playlistState = new PlaylistState { Items = TestDataFactory.CreatePlaylistItems(queue.ToArray()) };
        var session = _testSession with { AllowSingersToReorder = false };
        var sessionState = new SessionState 
        { 
            CurrentSession = session,
            IsInitialized = true 
        };
        SetupTestWithSession(sessionState, playlistState, view: "playlist");
        
        // Mock sessionStorage to return "singer" role
        // JSInterop.SetupAsync<string?>("sessionStorage.getItem", $"karamel-session-{session.SessionId}-role")
        //     .SetResult("singer");

        // Act
        // var cut = RenderComponent<Playlist>(parameters => parameters
        //     .Add(p => p.SessionParam, session.SessionId.ToString())
        //     .Add(p => p.TokenParam, "singer-token"));

        // Assert: Verify draggable=false or absent
        // var upNextSection = cut.Find(".up-next");
        // var songItems = upNextSection.QuerySelectorAll(".song-item");
        // foreach (var item in songItems)
        // {
        //     var draggable = item.GetAttribute("draggable");
        //     Assert.True(draggable == null || draggable == "false");
        // }
    }

    [Fact(Skip = "Requires sessionStorage role check implementation - IsAdminTabAsync() not yet implemented")]
    public void Playlist_WithSingerToken_AndAllowSingersToReorderTrue_EnablesDragDrop()
    {
        // Arrange: Mock singer role and AllowSingersToReorder=true
        var queue = new Queue<Song>(_testSongs);
        var playlistState = new PlaylistState { Items = TestDataFactory.CreatePlaylistItems(queue.ToArray()) };
        var session = _testSession with { AllowSingersToReorder = true }; // Singer allowed to reorder
        var sessionState = new SessionState 
        { 
            CurrentSession = session,
            IsInitialized = true 
        };
        SetupTestWithSession(sessionState, playlistState, view: "playlist");
        
        // Mock sessionStorage to return "singer" role
        // JSInterop.SetupAsync<string?>("sessionStorage.getItem", $"karamel-session-{session.SessionId}-role")
        //     .SetResult("singer");

        // Act
        // var cut = RenderComponent<Playlist>(parameters => parameters
        //     .Add(p => p.SessionParam, session.SessionId.ToString())
        //     .Add(p => p.TokenParam, "singer-token"));

        // Assert: Verify draggable=true attribute on playlist items
        // var upNextSection = cut.Find(".up-next");
        // var songItems = upNextSection.QuerySelectorAll(".song-item");
        // foreach (var item in songItems)
        // {
        //     var draggable = item.GetAttribute("draggable");
        //     Assert.Equal("true", draggable);
        // }
    }

    [Fact]
    public void Component_DisplaysQueuePositionNumbers()
    {
        // Arrange
        var queue = new Queue<Song>(_testSongs);
        var playlistState = new PlaylistState { Items = TestDataFactory.CreatePlaylistItems(queue.ToArray()) };
        var sessionState = new SessionState 
        { 
            CurrentSession = _testSession,
            IsInitialized = true 
        };
        SetupTestWithSession(sessionState, playlistState, view: "playlist");

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
        var playlistState = new PlaylistState { Items = TestDataFactory.CreatePlaylistItems(queue.ToArray()) };
        var sessionState = new SessionState 
        { 
            CurrentSession = _testSession,
            IsInitialized = true 
        };
        playlistState = playlistState with { CurrentSong = TestDataFactory.CreatePlaylistItem(_testSongs[0]) }; // Set CurrentSong
        SetupTestWithSession(sessionState, playlistState, view: "playlist");

        // Act
        var cut = RenderComponent<Playlist>();

        // Assert - Check "Now Playing"
        var nowPlaying = cut.Find(".now-playing");
        Assert.Contains("Alice", nowPlaying.TextContent);

        // Assert - Check "Up Next" items (all songs in queue)
        var upNextSection = cut.Find(".up-next");
        Assert.Contains("Bob", upNextSection.TextContent);
        Assert.Contains("Alice", upNextSection.TextContent); // ABBA song
        Assert.Contains("Charlie", upNextSection.TextContent);
    }
}


