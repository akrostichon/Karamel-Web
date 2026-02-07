using Bunit;
using Karamel.Web.Components;
using Karamel.Web.Models;
using Karamel.Web.Tests.TestHelpers;
using Karamel.Web.Contracts;
using Karamel.Web.Pages;
using Karamel.Web.Store.Library;
using Karamel.Web.Store.Playlist;
using Karamel.Web.Store.Session;
using Moq;
using Fluxor;

namespace Karamel.Web.Tests;

/// <summary>
/// Unit tests for the SingerView component.
/// Tests name entry, library search integration, song limit enforcement, and toast notifications.
/// </summary>
public class SingerViewTests : SessionTestBase
{
    private readonly List<Song> _testSongs;
    private readonly Session _testSessionWithNameRequired;
    private readonly Session _testSessionWithoutNameRequired;

    public SingerViewTests()
    {
        // Setup test songs
        _testSongs = new List<Song>
        {
            new Song { Id = Guid.NewGuid(), Artist = "Beatles", Title = "Let It Be", Mp3FileName = "beatles-let-it-be.mp3", CdgFileName = "beatles-let-it-be.cdg" },
            new Song { Id = Guid.NewGuid(), Artist = "Queen", Title = "Bohemian Rhapsody", Mp3FileName = "queen-bohemian-rhapsody.mp3", CdgFileName = "queen-bohemian-rhapsody.cdg" }
        };

        _testSessionWithNameRequired = new Session
        {
            SessionId = Guid.NewGuid(),
            RequireSingerName = true
        };

        _testSessionWithoutNameRequired = new Session
        {
            SessionId = Guid.NewGuid(),
            RequireSingerName = false
        };
    }

    [Fact]
    public void Component_WhenNoSession_ShowsInvalidSessionMessage()
    {
        // Arrange
        var sessionState = new SessionState { CurrentSession = null };
        SetupTestWithSession(sessionState, new PlaylistState(), new LibraryState(), view: "singer");

        // Act
        var cut = RenderComponent<SingerView>();

        // Assert
        var alert = cut.Find(".alert-danger");
        Assert.Contains("Invalid Session", alert.TextContent);
        Assert.Contains("No active karaoke session found", alert.TextContent);
    }

    [Fact]
    public void Component_WhenRequireSingerNameTrue_ShowsNameEntryForm()
    {
        // Arrange
        var sessionState = new SessionState { CurrentSession = _testSessionWithNameRequired, IsInitialized = true };
        SetupTestWithSession(sessionState, new PlaylistState(), new LibraryState(), view: "singer");

        // Act
        var cut = RenderComponent<SingerView>();

        // Assert
        var nameInput = cut.Find("input#singerNameInput");
        Assert.NotNull(nameInput);
        
        var heading = cut.Find("h2");
        Assert.Contains("Welcome to Karaoke", heading.TextContent);
        
        var continueButton = cut.Find("button.k-btn-primary");
        Assert.Contains("Continue", continueButton.TextContent);
    }

    [Fact]
    public void Component_WhenRequireSingerNameFalse_ShowsLibrarySearchDirectly()
    {
        // Arrange
        var sessionState = new SessionState { CurrentSession = _testSessionWithoutNameRequired, IsInitialized = true };
        var libraryState = new LibraryState { Songs = _testSongs };
        SetupTestWithSession(sessionState, new PlaylistState(), libraryState, view: "singer");

        // Act
        var cut = RenderComponent<SingerView>();

        // Assert
        var librarySearch = cut.FindComponent<LibrarySearch>();
        Assert.NotNull(librarySearch);
        
        // Should not show name entry form
        Assert.Throws<ElementNotFoundException>(() => cut.Find("input#singerNameInput"));
    }

    [Fact]
    public void NameEntry_ContinueButtonDisabled_WhenNameIsEmpty()
    {
        // Arrange
        var sessionState = new SessionState { CurrentSession = _testSessionWithNameRequired, IsInitialized = true };
        SetupTestWithSession(sessionState, new PlaylistState(), new LibraryState(), view: "singer");

        // Act
        var cut = RenderComponent<SingerView>();
        var continueButton = cut.Find("button.k-btn-primary");

        // Assert
        Assert.True(continueButton.HasAttribute("disabled"));
    }

    [Fact]
    public void NameEntry_ContinueButtonEnabled_WhenNameIsEntered()
    {
        // Arrange
        var sessionState = new SessionState { CurrentSession = _testSessionWithNameRequired, IsInitialized = true };
        SetupTestWithSession(sessionState, new PlaylistState(), new LibraryState(), view: "singer");
        var cut = RenderComponent<SingerView>();
        var nameInput = cut.Find("input#singerNameInput");

        // Act
        nameInput.Input("John");
        var continueButton = cut.Find("button.k-btn-primary");

        // Assert
        Assert.False(continueButton.HasAttribute("disabled"));
    }

    [Fact]
    public void NameEntry_ShowsErrorMessage_WhenNameIsTooShort()
    {
        // Arrange
        var sessionState = new SessionState { CurrentSession = _testSessionWithNameRequired, IsInitialized = true };
        SetupTestWithSession(sessionState, new PlaylistState(), new LibraryState(), view: "singer");
        var cut = RenderComponent<SingerView>();
        var nameInput = cut.Find("input#singerNameInput");

        // Act
        nameInput.Input("J");
        var continueButton = cut.Find("button.k-btn-primary");
        continueButton.Click();

        // Assert
        var errorAlert = cut.Find(".alert-danger");
        Assert.Contains("Name must be at least 2 characters", errorAlert.TextContent);
    }

    [Fact]
    public void NameEntry_AcceptsName_AndShowsLibrarySearch()
    {
        // Arrange
        var sessionState = new SessionState { CurrentSession = _testSessionWithNameRequired, IsInitialized = true };
        var libraryState = new LibraryState { Songs = _testSongs };
        SetupTestWithSession(sessionState, new PlaylistState(), libraryState, view: "singer");
        var cut = RenderComponent<SingerView>();
        var nameInput = cut.Find("input#singerNameInput");

        // Act
        nameInput.Input("John Doe");
        var continueButton = cut.Find("button.k-btn-primary");
        continueButton.Click();

        // Assert
        var librarySearch = cut.FindComponent<LibrarySearch>();
        Assert.NotNull(librarySearch);
        
        var header = cut.Find(".singer-header h3");
        Assert.Contains("Welcome, John Doe!", header.TextContent);
    }

    [Fact]
    public void NameEntry_TrimsWhitespace_FromEnteredName()
    {
        // Arrange
        var sessionState = new SessionState { CurrentSession = _testSessionWithNameRequired, IsInitialized = true };
        var libraryState = new LibraryState { Songs = _testSongs };
        SetupTestWithSession(sessionState, new PlaylistState(), libraryState, view: "singer");
        var cut = RenderComponent<SingerView>();
        var nameInput = cut.Find("input#singerNameInput");

        // Act
        nameInput.Input("  John Doe  ");
        var continueButton = cut.Find("button.k-btn-primary");
        continueButton.Click();

        // Assert
        var header = cut.Find(".singer-header h3");
        Assert.Contains("Welcome, John Doe!", header.TextContent);
    }

    [Fact]
    public void NameEntry_HandlesEnterKey_ToConfirmName()
    {
        // Arrange
        var sessionState = new SessionState { CurrentSession = _testSessionWithNameRequired, IsInitialized = true };
        var libraryState = new LibraryState { Songs = _testSongs };
        SetupTestWithSession(sessionState, new PlaylistState(), libraryState, view: "singer");
        var cut = RenderComponent<SingerView>();
        var nameInput = cut.Find("input#singerNameInput");

        // Act
        nameInput.Input("John");
        nameInput.KeyUp("Enter");

        // Assert
        var librarySearch = cut.FindComponent<LibrarySearch>();
        Assert.NotNull(librarySearch);
        
        var header = cut.Find(".singer-header h3");
        Assert.Contains("Welcome, John!", header.TextContent);
    }

    [Fact]
    public void LibraryView_DisplaysSongCount_FromPlaylistState()
    {
        // Arrange
        var sessionState = new SessionState { CurrentSession = _testSessionWithoutNameRequired, IsInitialized = true };
        var libraryState = new LibraryState { Songs = _testSongs };
        var playlistState = new PlaylistState
        {
            Items = new List<PlaylistItemDto>() // SingerSongCounts removed
        };
        SetupTestWithSession(sessionState, playlistState, libraryState, view: "singer");

        // Act
        var cut = RenderComponent<SingerView>();

        // Assert
        var songCount = cut.Find(".song-count");
        Assert.Contains("0 / 10 songs in queue", songCount.TextContent); // 0 because no singer name when RequireSingerName is false
    }

    [Fact]
    public void AddToQueue_DispatchesAction_WithSingerName()
    {
        // Arrange
        var sessionState = new SessionState { CurrentSession = _testSessionWithNameRequired, IsInitialized = true };
        var libraryState = new LibraryState { Songs = _testSongs };
        var (_, dispatcher, _) = SetupTestWithSession(sessionState, new PlaylistState(), libraryState, view: "singer");
        
        var cut = RenderComponent<SingerView>();
        
        // Enter name first
        var nameInput = cut.Find("input#singerNameInput");
        nameInput.Input("Alice");
        var continueButton = cut.Find("button.k-btn-primary");
        continueButton.Click();

        // Act
        var librarySearch = cut.FindComponent<LibrarySearch>();
        var addButtons = librarySearch.FindAll("button.k-btn-primary");
        addButtons[0].Click(); // Click first song's Add button

        // Assert
        dispatcher.Verify(d => d.Dispatch(It.Is<AddToPlaylistAction>(
            a => a.Song.Artist == "Beatles" && 
                 a.Song.Title == "Let It Be" && 
                 a.SingerName == "Alice"
        )), Times.Once);
    }

    [Fact]
    public void AddToQueue_DispatchesAction_WithNullSingerName_WhenNotRequired()
    {
        // Arrange
        var sessionState = new SessionState { CurrentSession = _testSessionWithoutNameRequired, IsInitialized = true };
        var libraryState = new LibraryState { Songs = _testSongs };
        var (_, dispatcher, _) = SetupTestWithSession(sessionState, new PlaylistState(), libraryState, view: "singer");
        
        var cut = RenderComponent<SingerView>();

        // Act
        var librarySearch = cut.FindComponent<LibrarySearch>();
        var addButtons = librarySearch.FindAll("button.k-btn-primary");
        addButtons[0].Click();

        // Assert
        dispatcher.Verify(d => d.Dispatch(It.Is<AddToPlaylistAction>(
            a => a.Song.Artist == "Beatles" && 
                 a.Song.Title == "Let It Be" && 
                 a.SingerName == null
        )), Times.Once);
    }

    [Fact]
    public void Component_ShowsSuccessToast_OnAddToPlaylistSuccess()
    {
        // Arrange
        var sessionState = new SessionState { CurrentSession = _testSessionWithNameRequired, IsInitialized = true };
        var libraryState = new LibraryState { Songs = _testSongs };
        var playlistState = new PlaylistState
        {
            Items = TestDataFactory.CreatePlaylistItems(new[] { _testSongs[0] })
        };
        var (actionSubscriber, _, _) = SetupTestWithSession(sessionState, playlistState, libraryState, view: "singer");
        
        var cut = RenderComponent<SingerView>();
        
        // Enter name
        var nameInput = cut.Find("input#singerNameInput");
        nameInput.Input("Bob");
        var continueButton = cut.Find("button.k-btn-primary");
        continueButton.Click();

        // Act - Simulate AddToPlaylistSuccessAction
        var songWithSinger = _testSongs[0] with { AddedBySinger = "Bob" };
        var successAction = new AddToPlaylistSuccessAction(songWithSinger);
        
        // We need to directly call the component's handler
        cut.InvokeAsync(() => cut.Instance.GetType()
            .GetMethod("HandleAddToPlaylistSuccess", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance)?
            .Invoke(cut.Instance, new[] { successAction }));

        // Assert
        var toast = cut.Find(".toast.show");
        Assert.NotNull(toast);
        
        var toastBody = cut.Find(".toast-body");
        Assert.Contains("added", toastBody.TextContent);
        Assert.Contains("#1 in queue", toastBody.TextContent);
    }

    [Fact]
    public void Component_ShowsErrorToast_OnAddToPlaylistFailure()
    {
        // Arrange
        var sessionState = new SessionState { CurrentSession = _testSessionWithNameRequired, IsInitialized = true };
        var libraryState = new LibraryState { Songs = _testSongs };
        var playlistState = new PlaylistState
        {
            Items = new List<PlaylistItemDto>() // SingerSongCounts removed
        };
        var (actionSubscriber, _, _) = SetupTestWithSession(sessionState, playlistState, libraryState, view: "singer");
        
        var cut = RenderComponent<SingerView>();
        
        // Enter name
        var nameInput = cut.Find("input#singerNameInput");
        nameInput.Input("Charlie");
        var continueButton = cut.Find("button.k-btn-primary");
        continueButton.Click();

        // Act - Simulate AddToPlaylistFailureAction
        var failureAction = new AddToPlaylistFailureAction("Maximum 10 songs per singer reached");
        
        cut.InvokeAsync(() => cut.Instance.GetType()
            .GetMethod("HandleAddToPlaylistFailure", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance)?
            .Invoke(cut.Instance, new[] { failureAction }));

        // Assert
        var toast = cut.Find(".toast.show");
        Assert.NotNull(toast);
        
        var toastBody = cut.Find(".toast-body");
        Assert.Contains("Maximum 10 songs per singer reached", toastBody.TextContent);
        
        var toastHeader = cut.Find(".toast-header");
        Assert.Contains("toast-error", toastHeader.ClassName);
    }

    [Fact]
    public void Component_ShowsSongCountForCurrentSinger()
    {
        // Arrange
        var sessionState = new SessionState { CurrentSession = _testSessionWithNameRequired, IsInitialized = true };
        var libraryState = new LibraryState { Songs = _testSongs };
        var playlistState = new PlaylistState
        {
            Items = new List<PlaylistItemDto>
            {
                new PlaylistItemDto(Guid.NewGuid().ToString(), _testSongs[0].Id.ToString(), _testSongs[0].Artist, _testSongs[0].Title, "David", 0, 0),
                new PlaylistItemDto(Guid.NewGuid().ToString(), _testSongs[1].Id.ToString(), _testSongs[1].Artist, _testSongs[1].Title, "David", 1, 0),
                new PlaylistItemDto(Guid.NewGuid().ToString(), _testSongs[0].Id.ToString(), _testSongs[0].Artist, _testSongs[0].Title, "David", 2, 0)
            }
        };
        SetupTestWithSession(sessionState, playlistState, libraryState, view: "singer");
        
        var cut = RenderComponent<SingerView>();
        
        // Enter name
        var nameInput = cut.Find("input#singerNameInput");
        nameInput.Input("David");
        var continueButton = cut.Find("button.k-btn-primary");
        continueButton.Click();

        // Assert - GetSongCount() filters by singerName and Status != Completed
        var songCount = cut.Find(".song-count");
        // All 3 items have SingerName="David" and Status=Queued (Queued)
        Assert.Contains("3 / 10 songs in queue", songCount.TextContent);
    }

    [Fact]
    public void Component_HandlesSpecialCharactersInName()
    {
        // Arrange
        var sessionState = new SessionState { CurrentSession = _testSessionWithNameRequired, IsInitialized = true };
        var libraryState = new LibraryState { Songs = _testSongs };
        SetupTestWithSession(sessionState, new PlaylistState(), libraryState, view: "singer");
        var cut = RenderComponent<SingerView>();
        var nameInput = cut.Find("input#singerNameInput");

        // Act
        nameInput.Input("José García-López");
        var continueButton = cut.Find("button.k-btn-primary");
        continueButton.Click();

        // Assert
        var header = cut.Find(".singer-header h3");
        Assert.Contains("Welcome, José García-López!", header.TextContent);
    }

    [Fact]
    public void Component_EnforcesMaxLength_OnNameInput()
    {
        // Arrange
        var sessionState = new SessionState { CurrentSession = _testSessionWithNameRequired, IsInitialized = true };
        SetupTestWithSession(sessionState, new PlaylistState(), new LibraryState(), view: "singer");
        var cut = RenderComponent<SingerView>();
        var nameInput = cut.Find("input#singerNameInput");

        // Assert
        Assert.Equal("50", nameInput.GetAttribute("maxlength"));
    }

    [Fact]
    public void Component_UpdatesDisplay_WhenPlaylistStateChanges()
    {
        // This test verifies that SingerView reactively updates the song count display
        // when playlist state changes via Fluxor StateChanged events.
        // Strategy: Use reflection to access the mock state and manually trigger StateChanged,
        // simulating what would happen when Fluxor updates state in the real app.

        // Arrange
        var sessionState = new SessionState { CurrentSession = _testSessionWithNameRequired, IsInitialized = true };
        var libraryState = new LibraryState { Songs = _testSongs };
        
        // Create an initial empty playlist state
        var currentPlaylistState = new PlaylistState { Items = new List<PlaylistItemDto>() };
        
        var (_, _, _) = SetupTestWithSession(sessionState, currentPlaylistState, libraryState, view: "singer");
        
        // Get the mock playlist state to trigger events
        var playlistStateService = Services.GetService<IState<PlaylistState>>();
        Assert.NotNull(playlistStateService);
        var mockPlaylistState = Mock.Get(playlistStateService);
        
        var cut = RenderComponent<SingerView>();
        
        // Enter singer name
        var nameInput = cut.Find("input#singerNameInput");
        nameInput.Input("Alice");
        var continueButton = cut.Find("button.k-btn-primary");
        continueButton.Click();

        // Assert initial state - should show 0 songs
        var songCount = cut.Find(".song-count");
        Assert.Contains("0 / 10 songs in queue", songCount.TextContent);

        // Act - simulate adding first song to playlist state
        var song1WithSinger = _testSongs[0] with { AddedBySinger = "Alice" };
        var item1 = new PlaylistItemDto(
            Guid.NewGuid().ToString(), 
            song1WithSinger.Id.ToString(), 
            song1WithSinger.Artist, 
            song1WithSinger.Title, 
            "Alice", 
            0, // position
            (int)SongStatus.Queued  // status (Queued)
        );
        
        // Update the state that the mock returns
        currentPlaylistState = currentPlaylistState with { Items = new List<PlaylistItemDto> { item1 } };
        mockPlaylistState.Setup(s => s.Value).Returns(currentPlaylistState);
        // Trigger StateChanged event that FluxorComponent subscribes to
        mockPlaylistState.Raise(m => m.StateChanged += null, EventArgs.Empty);
        
        // Re-render to pick up state changes
        cut.Render();
        
        // Assert - should now show 1 song
        songCount = cut.Find(".song-count");
        Assert.Contains("1 / 10 songs in queue", songCount.TextContent);

        // Act - simulate adding second song for "Alice"
        var song2WithSinger = _testSongs[1] with { AddedBySinger = "Alice" };
        var item2 = new PlaylistItemDto(
            Guid.NewGuid().ToString(), 
            song2WithSinger.Id.ToString(), 
            song2WithSinger.Artist, 
            song2WithSinger.Title, 
            "Alice", 
            1, // position
            (int)SongStatus.Queued  // status (Queued)
        );
        
        currentPlaylistState = currentPlaylistState with { Items = new List<PlaylistItemDto> { item1, item2 } };
        mockPlaylistState.Setup(s => s.Value).Returns(currentPlaylistState);
        mockPlaylistState.Raise(m => m.StateChanged += null, EventArgs.Empty);
        
        cut.Render();
        
        // Assert - should now show 2 songs
        songCount = cut.Find(".song-count");
        Assert.Contains("2 / 10 songs in queue", songCount.TextContent);
    }

    [Fact]
    public void Component_UpdatesDisplay_WhenQueueBecomesEmpty()
    {
        // This test verifies that the song count updates correctly when all songs for
        // the current singer are removed or completed.
        // Strategy: Start with songs in queue, update state to mark them as completed (Status=Completed),
        // and verify the count returns to 0.

        // Arrange
        var sessionState = new SessionState { CurrentSession = _testSessionWithNameRequired, IsInitialized = true };
        var libraryState = new LibraryState { Songs = _testSongs };
        
        // Start with 2 songs in queue for "Bob"
        var song1 = _testSongs[0] with { AddedBySinger = "Bob" };
        var song2 = _testSongs[1] with { AddedBySinger = "Bob" };
        var item1 = new PlaylistItemDto(
            Guid.NewGuid().ToString(), 
            song1.Id.ToString(), 
            song1.Artist, 
            song1.Title, 
            "Bob", 
            0, 
            (int)SongStatus.Queued  // Queued
        );
        var item2 = new PlaylistItemDto(
            Guid.NewGuid().ToString(), 
            song2.Id.ToString(), 
            song2.Artist, 
            song2.Title, 
            "Bob", 
            1, 
            (int)SongStatus.Queued  // Queued
        );
        
        var currentPlaylistState = new PlaylistState { Items = new List<PlaylistItemDto> { item1, item2 } };
        
        var (_, _, _) = SetupTestWithSession(sessionState, currentPlaylistState, libraryState, view: "singer");
        
        var playlistStateService = Services.GetService<IState<PlaylistState>>();
        Assert.NotNull(playlistStateService);
        var mockPlaylistState = Mock.Get(playlistStateService);
        
        var cut = RenderComponent<SingerView>();
        
        // Enter singer name
        var nameInput = cut.Find("input#singerNameInput");
        nameInput.Input("Bob");
        var continueButton = cut.Find("button.k-btn-primary");
        continueButton.Click();

        // Assert initial state - should show 2 songs (both with Status=Queued, not completed)
        var songCount = cut.Find(".song-count");
        Assert.Contains("2 / 10 songs in queue", songCount.TextContent);

        // Act - mark first song as completed (Status=Completed)
        var item1Completed = item1 with { Status = (int)SongStatus.Completed };  // Completed
        currentPlaylistState = currentPlaylistState with { Items = new List<PlaylistItemDto> { item1Completed, item2 } };
        mockPlaylistState.Setup(s => s.Value).Returns(currentPlaylistState);
        mockPlaylistState.Raise(m => m.StateChanged += null, EventArgs.Empty);
        
        cut.Render();
        
        // Assert - should now show 1 song (only non-completed songs count)
        songCount = cut.Find(".song-count");
        Assert.Contains("1 / 10 songs in queue", songCount.TextContent);

        // Act - mark second song as completed too
        var item2Completed = item2 with { Status = (int)SongStatus.Completed };  // Completed
        currentPlaylistState = currentPlaylistState with { Items = new List<PlaylistItemDto> { item1Completed, item2Completed } };
        mockPlaylistState.Setup(s => s.Value).Returns(currentPlaylistState);
        mockPlaylistState.Raise(m => m.StateChanged += null, EventArgs.Empty);
        
        cut.Render();
        
        // Assert - should now show 0 songs (all completed)
        songCount = cut.Find(".song-count");
        Assert.Contains("0 / 10 songs in queue", songCount.TextContent);
    }

    [Fact]
    public void Component_ReactsTo_MultipleQueueChanges()
    {
        // This test verifies that the component correctly handles rapid successive state changes,
        // which can happen in a multi-user scenario where multiple singers add songs simultaneously.
        // Strategy: Simulate adding multiple songs in quick succession, verify each state update
        // is reflected correctly in the display.

        // Arrange
        var sessionState = new SessionState { CurrentSession = _testSessionWithNameRequired, IsInitialized = true };
        var libraryState = new LibraryState { Songs = _testSongs };
        
        var currentPlaylistState = new PlaylistState { Items = new List<PlaylistItemDto>() };
        
        var (_, _, _) = SetupTestWithSession(sessionState, currentPlaylistState, libraryState, view: "singer");
        
        var playlistStateService = Services.GetService<IState<PlaylistState>>();
        Assert.NotNull(playlistStateService);
        var mockPlaylistState = Mock.Get(playlistStateService);
        
        var cut = RenderComponent<SingerView>();
        
        // Enter singer name
        var nameInput = cut.Find("input#singerNameInput");
        nameInput.Input("Charlie");
        var continueButton = cut.Find("button.k-btn-primary");
        continueButton.Click();

        // Assert initial state
        var songCount = cut.Find(".song-count");
        Assert.Contains("0 / 10 songs in queue", songCount.TextContent);

        // Act - rapidly add 5 songs for "Charlie" (simulating multiple quick additions)
        var items = new List<PlaylistItemDto>();
        for (int i = 0; i < 5; i++)
        {
            // Alternate between the two test songs
            var song = _testSongs[i % 2] with { AddedBySinger = "Charlie" };
            var item = new PlaylistItemDto(
                Guid.NewGuid().ToString(), 
                song.Id.ToString(), 
                song.Artist, 
                song.Title, 
                "Charlie", 
                i, 
                (int)SongStatus.Queued  // Queued
            );
            items.Add(item);
            
            // Update state after each addition
            currentPlaylistState = currentPlaylistState with { Items = new List<PlaylistItemDto>(items) };
            mockPlaylistState.Setup(s => s.Value).Returns(currentPlaylistState);
            mockPlaylistState.Raise(m => m.StateChanged += null, EventArgs.Empty);
            
            cut.Render();
            
            // Verify count increases correctly
            songCount = cut.Find(".song-count");
            Assert.Contains($"{i + 1} / 10 songs in queue", songCount.TextContent);
        }

        // Assert final state - should have 5 songs
        songCount = cut.Find(".song-count");
        Assert.Contains("5 / 10 songs in queue", songCount.TextContent);

        // Act - now remove 2 songs by marking them as completed
        items[0] = items[0] with { Status = (int)SongStatus.Completed };  // Completed
        items[1] = items[1] with { Status = (int)SongStatus.Completed };  // Completed
        currentPlaylistState = currentPlaylistState with { Items = new List<PlaylistItemDto>(items) };
        mockPlaylistState.Setup(s => s.Value).Returns(currentPlaylistState);
        mockPlaylistState.Raise(m => m.StateChanged += null, EventArgs.Empty);
        
        cut.Render();
        
        // Assert - should now show 3 songs (5 - 2 completed)
        songCount = cut.Find(".song-count");
        Assert.Contains("3 / 10 songs in queue", songCount.TextContent);

        // Act - add 2 more songs
        for (int i = 5; i < 7; i++)
        {
            var song = _testSongs[i % 2] with { AddedBySinger = "Charlie" };
            var item = new PlaylistItemDto(
                Guid.NewGuid().ToString(), 
                song.Id.ToString(), 
                song.Artist, 
                song.Title, 
                "Charlie", 
                i, 
                (int)SongStatus.Queued  // Queued
            );
            items.Add(item);
        }
        
        currentPlaylistState = currentPlaylistState with { Items = new List<PlaylistItemDto>(items) };
        mockPlaylistState.Setup(s => s.Value).Returns(currentPlaylistState);
        mockPlaylistState.Raise(m => m.StateChanged += null, EventArgs.Empty);
        
        cut.Render();
        
        // Assert - should now show 5 songs (3 + 2 new, still 2 completed)
        songCount = cut.Find(".song-count");
        Assert.Contains("5 / 10 songs in queue", songCount.TextContent);
    }

}


