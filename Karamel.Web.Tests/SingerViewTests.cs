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

        // Act - don't pass SessionParam, triggering error state
        var cut = RenderComponent<SingerView>();
        
        // Wait for initialization to complete (should show error)
        cut.WaitForState(() => cut.Markup.Contains("Session Loading Failed"), timeout: TimeSpan.FromSeconds(2));

        // Assert
        var alert = cut.Find(".alert-danger");
        Assert.Contains("Session Loading Failed", alert.TextContent);
        Assert.Contains("Unable to load the karaoke session", alert.TextContent);
    }

    [Fact]
    public void Component_WhenRequireSingerNameTrue_ShowsNameEntryForm()
    {
        // Arrange
        var sessionState = new SessionState { CurrentSession = _testSessionWithNameRequired, IsInitialized = true };
        var libraryState = new LibraryState();
        SetupTestWithSession(sessionState, new PlaylistState(), libraryState, "singer", false);

        // Act
        var cut = RenderSingerViewComponent(_testSessionWithNameRequired.SessionId);

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
        SetupTestWithSession(sessionState, new PlaylistState(), libraryState, "singer", false);

        // Act
        var cut = RenderSingerViewComponent(_testSessionWithoutNameRequired.SessionId);

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
        SetupTestWithSession(sessionState, new PlaylistState(), new LibraryState(), "singer", false);

        // Act
        var cut = RenderSingerViewComponent(_testSessionWithNameRequired.SessionId);
        var continueButton = cut.Find("button.k-btn-primary");

        // Assert
        Assert.True(continueButton.HasAttribute("disabled"));
    }

    [Fact]
    public void NameEntry_ContinueButtonEnabled_WhenNameIsEntered()
    {
        // Arrange
        var sessionState = new SessionState { CurrentSession = _testSessionWithNameRequired, IsInitialized = true };
        SetupTestWithSession(sessionState, new PlaylistState(), new LibraryState(), "singer", false);
        var cut = RenderSingerViewComponent(_testSessionWithNameRequired.SessionId);
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
        SetupTestWithSession(sessionState, new PlaylistState(), new LibraryState(), "singer", false);
        var cut = RenderSingerViewComponent(_testSessionWithNameRequired.SessionId);
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
        SetupTestWithSession(sessionState, new PlaylistState(), libraryState, "singer", false);
        var cut = RenderSingerViewComponent(sessionState.CurrentSession.SessionId);
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
        SetupTestWithSession(sessionState, new PlaylistState(), libraryState, "singer", false);
        var cut = RenderSingerViewComponent(sessionState.CurrentSession.SessionId);
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
        SetupTestWithSession(sessionState, new PlaylistState(), libraryState, "singer", false);
        var cut = RenderSingerViewComponent(sessionState.CurrentSession.SessionId);
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
        SetupTestWithSession(sessionState, playlistState, libraryState, "singer", false);

        // Act
        var cut = RenderSingerViewComponent(sessionState.CurrentSession.SessionId);

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
        
        var cut = RenderSingerViewComponent(sessionState.CurrentSession.SessionId);
        
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
        
        var cut = RenderSingerViewComponent(sessionState.CurrentSession.SessionId);

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
        var (mockActionSubscriber, _, _) = SetupTestWithSession(sessionState, playlistState, libraryState, view: "singer");
        
        var cut = RenderSingerViewComponent(sessionState.CurrentSession.SessionId);
        
        // Enter name first
        var nameInput = cut.Find("input#singerNameInput");
        nameInput.Input("Alice");
        var continueButton = cut.Find("button.k-btn-primary");
        continueButton.Click();

        // Act - Simulate success action
        var successAction = new AddToPlaylistSuccessAction(
            new Song { 
                Id = _testSongs[0].Id, 
                Title = "Let It Be", 
                Artist = "Beatles", 
                AddedBySinger = "Alice",
                Mp3FileName = "beatles-let-it-be.mp3",
                CdgFileName = "beatles-let-it-be.cdg"
            }
        );
        
        // Directly call the handler (since we can't easily trigger action subscriber in tests)
        cut.InvokeAsync(() => cut.Instance.GetType()
            .GetMethod("HandleAddToPlaylistSuccess", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance)?
            .Invoke(cut.Instance, new[] { successAction }));
        
        // Assert - Check for success toast
        cut.WaitForAssertion(() => 
        {
            var toast = cut.Find(".toast.show");
            Assert.Contains("Success", toast.TextContent);
            Assert.Contains("added", toast.TextContent);
        }, timeout: TimeSpan.FromSeconds(2));
    }

    [Fact]
    public async Task Component_ShowsErrorToast_OnAddToPlaylistFailure()
    {
        // Arrange
        var sessionState = new SessionState { CurrentSession = _testSessionWithNameRequired, IsInitialized = true };
        var libraryState = new LibraryState { Songs = _testSongs };
        var playlistState = new PlaylistState
        {
            Items = new List<PlaylistItemDto>() // SingerSongCounts removed
        };
        var (actionSubscriber, _, _) = SetupTestWithSession(sessionState, playlistState, libraryState, view: "singer");
        
        var cut = RenderSingerViewComponent(sessionState.CurrentSession.SessionId);
        
        // Enter name
        var nameInput = cut.Find("input#singerNameInput");
        nameInput.Input("Charlie");
        var continueButton = cut.Find("button.k-btn-primary");
        continueButton.Click();

        // Act - Simulate AddToPlaylistFailureAction
        var failureAction = new AddToPlaylistFailureAction("Maximum 10 songs per singer reached");
        
        await cut.InvokeAsync(() => cut.Instance.GetType()
            .GetMethod("HandleAddToPlaylistFailure", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance)?
            .Invoke(cut.Instance, new[] { failureAction }));

        // Wait for the toast to appear in the DOM
        cut.WaitForState(() => cut.Markup.Contains("toast show"), timeout: TimeSpan.FromSeconds(2));

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
        
        SetupTestWithSession(sessionState, playlistState, libraryState, "singer", false);
        var cut = RenderSingerViewComponent(sessionState.CurrentSession.SessionId);
        
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
        SetupTestWithSession(sessionState, new PlaylistState(), libraryState, "singer", false);
        var cut = RenderSingerViewComponent(sessionState.CurrentSession.SessionId);
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
        SetupTestWithSession(sessionState, new PlaylistState(), new LibraryState(), "singer", false);
        var cut = RenderSingerViewComponent(sessionState.CurrentSession.SessionId);
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
        
        var cut = RenderSingerViewComponent(sessionState.CurrentSession.SessionId);
        
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
        
        var cut = RenderSingerViewComponent(sessionState.CurrentSession.SessionId);
        
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
        
        var cut = RenderSingerViewComponent(sessionState.CurrentSession.SessionId);
        
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

    // Pagination tests
    [Fact]
    public void LoadMoreButton_ShowsWhenMorePagesAvailable()
    {
        // Arrange: Setup state with more pages available
        var sessionGuid = Guid.NewGuid();
        var sessionState = new SessionState 
        { 
            CurrentSession = new Session { SessionId = sessionGuid, RequireSingerName = false },
            IsInitialized = true
        };
        
        var songs = Enumerable.Range(1, 50)
            .Select(i => new Song 
            { 
                Id = Guid.NewGuid(), 
                Artist = $"Artist {i}", 
                Title = $"Song {i}",
                Mp3FileName = $"song{i}.mp3",
                CdgFileName = $"song{i}.cdg"
            })
            .ToList();

        var libraryState = new LibraryState
        {
            Songs = songs,
            CurrentPage = 1,
            PageSize = 50,
            TotalCount = 100,
            IsLoading = false
        };
        
        var playlistState = new PlaylistState { Items = new List<Karamel.Web.Contracts.PlaylistItemDto>() };
        
        SetupTestWithSession(sessionState, playlistState, libraryState, "singer", false);

        // Act
        var cut = RenderSingerViewComponent(sessionState.CurrentSession.SessionId);

        // Assert: Load more button should be visible
        var loadMoreButton = cut.FindAll("button").FirstOrDefault(b => b.TextContent.Contains("Load more"));
        Assert.NotNull(loadMoreButton);
        Assert.False(loadMoreButton.HasAttribute("disabled"));
    }

    [Fact]
    public void LoadMoreButton_HidesWhenAllPagesLoaded()
    {
        // Arrange: Setup state with all pages loaded
        var sessionGuid = Guid.NewGuid();
        var sessionState = new SessionState 
        { 
            CurrentSession = new Session { SessionId = sessionGuid, RequireSingerName = false },
            IsInitialized = true
        };
        
        var songs = Enumerable.Range(1, 30)
            .Select(i => new Song 
            { 
                Id = Guid.NewGuid(), 
                Artist = $"Artist {i}", 
                Title = $"Song {i}",
                Mp3FileName = $"song{i}.mp3",
                CdgFileName = $"song{i}.cdg"
            })
            .ToList();

        var libraryState = new LibraryState
        {
            Songs = songs,
            CurrentPage = 1,
            PageSize = 50,
            TotalCount = 30,
            IsLoading = false
        };
        
        var playlistState = new PlaylistState { Items = new List<Karamel.Web.Contracts.PlaylistItemDto>() };
        
        SetupTestWithSession(sessionState, playlistState, libraryState, "singer", false);

        // Act
        var cut = RenderSingerViewComponent(sessionState.CurrentSession.SessionId);

        // Assert: Load more button should NOT exist
        var loadMoreButton = cut.FindAll("button").FirstOrDefault(b => b.TextContent.Contains("Load more"));
        Assert.Null(loadMoreButton);
    }

    [Fact]
    public void LoadMoreButton_ShowsLoadingStateWhenFetching()
    {
        // Arrange: Setup state with loading = true
        var sessionGuid = Guid.NewGuid();
        var sessionState = new SessionState 
        { 
            CurrentSession = new Session { SessionId = sessionGuid, RequireSingerName = false },
            IsInitialized = true
        };
        
        var songs = Enumerable.Range(1, 50)
            .Select(i => new Song 
            { 
                Id = Guid.NewGuid(), 
                Artist = $"Artist {i}", 
                Title = $"Song {i}",
                Mp3FileName = $"song{i}.mp3",
                CdgFileName = $"song{i}.cdg"
            })
            .ToList();

        var libraryState = new LibraryState
        {
            Songs = songs,
            CurrentPage = 1,
            PageSize = 50,
            TotalCount = 100,
            IsLoading = true
        };
        
        var playlistState = new PlaylistState { Items = new List<Karamel.Web.Contracts.PlaylistItemDto>() };
        
        SetupTestWithSession(sessionState, playlistState, libraryState, "singer", false);

        // Act
        var cut = RenderSingerViewComponent(sessionState.CurrentSession.SessionId);

        // Assert: Button should exist and be disabled with loading text
        var loadingButton = cut.FindAll("button").FirstOrDefault(b => b.TextContent.Contains("Loading"));
        Assert.NotNull(loadingButton);
        Assert.True(loadingButton.HasAttribute("disabled"));
        Assert.Contains("spinner-border", cut.Markup);
    }

    [Fact]
    public void LoadMorePage_DispatchesLoadPageActionWithSearchQuery()
    {
        // Arrange: Setup state with search query active
        var sessionGuid = Guid.NewGuid();
        var sessionState = new SessionState 
        { 
            CurrentSession = new Session { SessionId = sessionGuid, RequireSingerName = false },
            IsInitialized = true
        };
        
        var songs = Enumerable.Range(1, 50)
            .Select(i => new Song 
            { 
                Id = Guid.NewGuid(), 
                Artist = $"Beatles {i}", 
                Title = $"Song {i}",
                Mp3FileName = $"song{i}.mp3",
                CdgFileName = $"song{i}.cdg"
            })
            .ToList();

        var libraryState = new LibraryState
        {
            Songs = songs,
            CurrentPage = 2,
            PageSize = 50,
            TotalCount = 150,
            ServerSearchQuery = "Beatles",
            IsLoading = false
        };
        
        var playlistState = new PlaylistState { Items = new List<Karamel.Web.Contracts.PlaylistItemDto>() };
        
        var (_, mockDispatcher, _) = SetupTestWithSession(sessionState, playlistState, libraryState, "singer", false);

        // Act
        var cut = RenderSingerViewComponent(sessionState.CurrentSession.SessionId);

        var loadMoreButton = cut.FindAll("button").FirstOrDefault(b => b.TextContent.Contains("Load more"));
        Assert.NotNull(loadMoreButton);
        loadMoreButton.Click();

        // Assert: LoadPageAction should be dispatched with correct params
        mockDispatcher.Verify(
            d => d.Dispatch(It.Is<LoadPageAction>(a => 
                a.Page == 3 && 
                a.SearchQuery == "Beatles" && 
                a.Append == true
            )),
            Times.Once
        );
    }

    // ── Phase 6: SingerView read-only playlist mode ────────────────────────────

    [Fact]
    public void ViewToggle_IsVisible_WhenSessionLoaded()
    {
        // Arrange
        var sessionState = new SessionState
        {
            CurrentSession = _testSessionWithoutNameRequired,
            IsInitialized = true
        };
        SetupTestWithSession(sessionState, new PlaylistState(), new LibraryState(), "singer", false);

        // Act
        var cut = RenderSingerViewComponent(_testSessionWithoutNameRequired.SessionId);

        // Assert: segmented toggle control is rendered
        var toggleBar = cut.Find(".singer-view-toggle");
        Assert.NotNull(toggleBar);

        var buttons = toggleBar.QuerySelectorAll("button");
        Assert.Equal(2, buttons.Length);
        Assert.Contains("Library", buttons[0].TextContent);
        Assert.Contains("Up Next", buttons[1].TextContent);
    }

    [Fact]
    public void ViewToggle_DefaultView_ShowsLibrarySearch()
    {
        // Arrange
        var sessionState = new SessionState
        {
            CurrentSession = _testSessionWithoutNameRequired,
            IsInitialized = true
        };
        SetupTestWithSession(sessionState, new PlaylistState(), new LibraryState { Songs = _testSongs }, "singer", false);

        // Act
        var cut = RenderSingerViewComponent(_testSessionWithoutNameRequired.SessionId);

        // Assert: Library tab is active and LibrarySearch is rendered
        var activeToggle = cut.Find(".singer-toggle-btn.active");
        Assert.Contains("Library", activeToggle.TextContent);

        var librarySearch = cut.FindComponent<Karamel.Web.Components.LibrarySearch>();
        Assert.NotNull(librarySearch);

        // UpNextList should NOT be visible in library mode
        Assert.Empty(cut.FindComponents<Karamel.Web.Components.UpNextList>());
    }

    [Fact]
    public void ViewToggle_ClickingUpNextTab_ShowsUpNextList()
    {
        // Arrange
        var sessionState = new SessionState
        {
            CurrentSession = _testSessionWithoutNameRequired,
            IsInitialized = true
        };
        SetupTestWithSession(sessionState, new PlaylistState(), new LibraryState(), "singer", false);

        var cut = RenderSingerViewComponent(_testSessionWithoutNameRequired.SessionId);

        // Act: click the "Up Next" tab button
        var toggleBar = cut.Find(".singer-view-toggle");
        var upNextBtn = toggleBar.QuerySelectorAll("button")
            .First(b => b.TextContent.Contains("Up Next"));
        upNextBtn.Click();

        // Assert: UpNextList component is now rendered
        var upNextList = cut.FindComponent<Karamel.Web.Components.UpNextList>();
        Assert.NotNull(upNextList);

        // LibrarySearch should no longer be rendered
        Assert.Empty(cut.FindComponents<Karamel.Web.Components.LibrarySearch>());

        // "Up Next" button should now be active
        var activeToggle = cut.Find(".singer-toggle-btn.active");
        Assert.Contains("Up Next", activeToggle.TextContent);
    }

    [Fact]
    public void UpNextList_IsReadOnly_NoRemoveOrDragButtons()
    {
        // Arrange
        var sessionGuid = _testSessionWithoutNameRequired.SessionId;
        var sessionState = new SessionState
        {
            CurrentSession = _testSessionWithoutNameRequired,
            IsInitialized = true
        };

        var queuedItems = new List<PlaylistItemDto>
        {
            new PlaylistItemDto(Id: Guid.NewGuid().ToString(), SongId: Guid.NewGuid().ToString(),
                Artist: "Queen", Title: "Don't Stop Me Now", SingerName: "Alice", Position: 0, Status: (int)SongStatus.UpNext),
            new PlaylistItemDto(Id: Guid.NewGuid().ToString(), SongId: Guid.NewGuid().ToString(),
                Artist: "ABBA", Title: "Dancing Queen", SingerName: "Bob", Position: 1, Status: (int)SongStatus.Queued),
        };
        var playlistState = new PlaylistState { Items = queuedItems };

        SetupTestWithSession(sessionState, playlistState, new LibraryState(), "singer", false);
        var cut = RenderSingerViewComponent(sessionGuid);

        // Switch to Up Next view
        var upNextBtn = cut.Find(".singer-view-toggle").QuerySelectorAll("button")
            .First(b => b.TextContent.Contains("Up Next"));
        upNextBtn.Click();

        // Assert: no remove buttons
        var removeButtons = cut.FindAll(".btn-remove");
        Assert.Empty(removeButtons);

        // Assert: no draggable song items
        var draggableItems = cut.FindAll("[draggable=\"true\"]");
        Assert.Empty(draggableItems);
    }

    [Fact]
    public void UpNextList_ShowsQueuedAndUpNextSongs()
    {
        // Arrange
        var sessionGuid = _testSessionWithoutNameRequired.SessionId;
        var sessionState = new SessionState
        {
            CurrentSession = _testSessionWithoutNameRequired,
            IsInitialized = true
        };

        var currentSong = new PlaylistItemDto(Id: Guid.NewGuid().ToString(), SongId: Guid.NewGuid().ToString(),
            Artist: "Beatles", Title: "Let It Be", SingerName: "Alice", Position: -1, Status: (int)SongStatus.NowPlaying);

        var queuedItems = new List<PlaylistItemDto>
        {
            new PlaylistItemDto(Id: Guid.NewGuid().ToString(), SongId: Guid.NewGuid().ToString(),
                Artist: "Queen", Title: "Bohemian Rhapsody", SingerName: "Bob", Position: 0, Status: (int)SongStatus.UpNext),
            new PlaylistItemDto(Id: Guid.NewGuid().ToString(), SongId: Guid.NewGuid().ToString(),
                Artist: "ABBA", Title: "Dancing Queen", SingerName: "Carol", Position: 1, Status: (int)SongStatus.Queued),
        };
        var playlistState = new PlaylistState { Items = queuedItems, CurrentSong = currentSong };

        SetupTestWithSession(sessionState, playlistState, new LibraryState(), "singer", false);
        var cut = RenderSingerViewComponent(sessionGuid);

        // Switch to Up Next view
        var upNextBtn = cut.Find(".singer-view-toggle").QuerySelectorAll("button")
            .First(b => b.TextContent.Contains("Up Next"));
        upNextBtn.Click();

        // Assert: Now Playing section shows current song
        var markup = cut.Markup;
        Assert.Contains("Now Playing", markup);
        Assert.Contains("Let It Be", markup);
        Assert.Contains("Alice", markup);

        // Assert: queued songs are displayed
        Assert.Contains("Bohemian Rhapsody", markup);
        Assert.Contains("Bob", markup);
        Assert.Contains("Dancing Queen", markup);
        Assert.Contains("Carol", markup);
    }

    [Fact]
    public void UpNextList_EmptyQueue_ShowsEmptyState()
    {
        // Arrange
        var sessionState = new SessionState
        {
            CurrentSession = _testSessionWithoutNameRequired,
            IsInitialized = true
        };
        var playlistState = new PlaylistState { Items = new List<PlaylistItemDto>() };

        SetupTestWithSession(sessionState, playlistState, new LibraryState(), "singer", false);
        var cut = RenderSingerViewComponent(_testSessionWithoutNameRequired.SessionId);

        // Switch to Up Next view
        var upNextBtn = cut.Find(".singer-view-toggle").QuerySelectorAll("button")
            .First(b => b.TextContent.Contains("Up Next"));
        upNextBtn.Click();

        // Assert: empty state message is shown
        var markup = cut.Markup;
        Assert.Contains("No songs in queue", markup);
    }

    // ── Phase 2: Edit Singer Name (US1) ───────────────────────────────────────

    [Fact]
    public void EditName_WhenPencilIconClicked_ShowsInlineEditMode()
    {
        // Arrange
        var sessionState = new SessionState { CurrentSession = _testSessionWithNameRequired, IsInitialized = true };
        var libraryState = new LibraryState { Songs = _testSongs };
        SetupTestWithSession(sessionState, new PlaylistState(), libraryState, "singer", false);
        var cut = RenderSingerViewComponent(_testSessionWithNameRequired.SessionId);

        // Enter name to get past the name-entry form
        cut.Find("input#singerNameInput").Input("Alice");
        cut.Find("button.k-btn-primary").Click();

        // Act: click the pencil icon button
        cut.Find("button.singer-edit-btn").Click();

        // Assert: inline edit input appears pre-filled with current name
        var editInput = cut.Find("input.singer-name-input");
        Assert.NotNull(editInput);
        Assert.Equal("Alice", editInput.GetAttribute("value"));
    }

    [Fact]
    public void EditName_WhenNameClicked_ShowsInlineEditMode()
    {
        // Arrange
        var sessionState = new SessionState { CurrentSession = _testSessionWithNameRequired, IsInitialized = true };
        var libraryState = new LibraryState { Songs = _testSongs };
        SetupTestWithSession(sessionState, new PlaylistState(), libraryState, "singer", false);
        var cut = RenderSingerViewComponent(_testSessionWithNameRequired.SessionId);

        // Enter name to get past the name-entry form
        cut.Find("input#singerNameInput").Input("Alice");
        cut.Find("button.k-btn-primary").Click();

        // Act: click the h3 name text directly
        cut.Find(".singer-name-display h3").Click();

        // Assert: inline edit input appears
        Assert.NotNull(cut.Find("input.singer-name-input"));
    }

    [Fact]
    public void EditName_ConfirmWithValidName_SavesNameAndExitsEditMode()
    {
        // Arrange
        var sessionState = new SessionState { CurrentSession = _testSessionWithNameRequired, IsInitialized = true };
        var libraryState = new LibraryState { Songs = _testSongs };
        SetupTestWithSession(sessionState, new PlaylistState(), libraryState, "singer", false);
        var cut = RenderSingerViewComponent(_testSessionWithNameRequired.SessionId);

        // Enter initial name
        cut.Find("input#singerNameInput").Input("Alice");
        cut.Find("button.k-btn-primary").Click();

        // Enter edit mode and change name
        cut.Find("button.singer-edit-btn").Click();
        cut.Find("input.singer-name-input").Input("AliceRenamed");

        // Act: confirm
        cut.Find("button.singer-name-confirm").Click();

        // Assert: edit mode closed, pencil visible again, header shows new name
        Assert.Throws<ElementNotFoundException>(() => cut.Find("input.singer-name-input"));
        cut.Find("button.singer-edit-btn");
        Assert.Contains("AliceRenamed", cut.Find(".singer-header h3").TextContent);
    }

    [Fact]
    public void EditName_UpdatedNameUsedForSubsequentQueueAdditions()
    {
        // Arrange
        var sessionState = new SessionState { CurrentSession = _testSessionWithNameRequired, IsInitialized = true };
        var libraryState = new LibraryState { Songs = _testSongs };
        var (_, dispatcher, _) = SetupTestWithSession(sessionState, new PlaylistState(), libraryState, view: "singer");
        var cut = RenderSingerViewComponent(_testSessionWithNameRequired.SessionId);

        // Enter initial name and rename
        cut.Find("input#singerNameInput").Input("Alice");
        cut.Find("button.k-btn-primary").Click();
        cut.Find("button.singer-edit-btn").Click();
        cut.Find("input.singer-name-input").Input("AliceRenamed");
        cut.Find("button.singer-name-confirm").Click();

        // Act: invoke HandleAddToQueue via reflection
        cut.Instance.GetType()
            .GetMethod("HandleAddToQueue", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance)?
            .Invoke(cut.Instance, new object[] { _testSongs[0] });

        // Assert: dispatched action carries the updated name
        dispatcher.Verify(d => d.Dispatch(It.Is<AddToPlaylistAction>(
            a => a.SingerName == "AliceRenamed"
        )), Times.Once);
    }

    // ── Phase 3: Edit Singer Name — Cancel (US2) ──────────────────────────────

    [Fact]
    public void EditName_WhenFocusLost_CancelsEditAndRestoresOriginalName()
    {
        // Arrange
        var sessionState = new SessionState { CurrentSession = _testSessionWithNameRequired, IsInitialized = true };
        var libraryState = new LibraryState { Songs = _testSongs };
        SetupTestWithSession(sessionState, new PlaylistState(), libraryState, "singer", false);
        var cut = RenderSingerViewComponent(_testSessionWithNameRequired.SessionId);

        // Enter initial name
        cut.Find("input#singerNameInput").Input("Alice");
        cut.Find("button.k-btn-primary").Click();

        // Enter edit mode and change the value without confirming
        cut.Find("button.singer-edit-btn").Click();
        cut.Find("input.singer-name-input").Input("PartialEdit");

        // Act: trigger focusout on the input (simulates clicking away)
        cut.Find("input.singer-name-input").TriggerEvent("onfocusout", EventArgs.Empty);

        // Assert: edit mode closed, original name unchanged
        Assert.Throws<ElementNotFoundException>(() => cut.Find("input.singer-name-input"));
        Assert.Contains("Alice", cut.Find(".singer-header h3").TextContent);
    }

    [Fact]
    public void EditName_CancelDoesNotSavePartialEdit()
    {
        // Arrange
        var sessionState = new SessionState { CurrentSession = _testSessionWithNameRequired, IsInitialized = true };
        var libraryState = new LibraryState { Songs = _testSongs };
        SetupTestWithSession(sessionState, new PlaylistState(), libraryState, "singer", false);
        var cut = RenderSingerViewComponent(_testSessionWithNameRequired.SessionId);

        // Enter initial name
        cut.Find("input#singerNameInput").Input("Bob");
        cut.Find("button.k-btn-primary").Click();

        // Enter edit mode, type partial new name, then cancel via focus loss
        cut.Find("button.singer-edit-btn").Click();
        cut.Find("input.singer-name-input").Input("SomethingDifferent");
        cut.Find("input.singer-name-input").TriggerEvent("onfocusout", EventArgs.Empty);

        // Assert: the header still shows the original name "Bob", not the partial edit
        var header = cut.Find(".singer-header h3");
        Assert.Contains("Bob", header.TextContent);
        Assert.DoesNotContain("SomethingDifferent", header.TextContent);
    }

    // ── Phase 4: Edit Singer Name — Empty Name Validation (US3) ──────────────

    [Fact]
    public void EditName_ConfirmWithEmptyInput_DoesNotSaveAndShowsError()
    {
        // Arrange
        var sessionState = new SessionState { CurrentSession = _testSessionWithNameRequired, IsInitialized = true };
        var libraryState = new LibraryState { Songs = _testSongs };
        SetupTestWithSession(sessionState, new PlaylistState(), libraryState, "singer", false);
        var cut = RenderSingerViewComponent(_testSessionWithNameRequired.SessionId);

        // Enter initial name
        cut.Find("input#singerNameInput").Input("Alice");
        cut.Find("button.k-btn-primary").Click();

        // Enter edit mode and clear the input
        cut.Find("button.singer-edit-btn").Click();
        cut.Find("input.singer-name-input").Input(string.Empty);

        // Act: click confirm with empty input
        cut.Find("button.singer-name-confirm").Click();

        // Assert: is-invalid class present, edit mode still active
        var editInput = cut.Find("input.singer-name-input");
        Assert.NotNull(editInput);
        Assert.Contains("is-invalid", editInput.ClassName);

        // Cancel edit and verify original name was preserved
        cut.Find("input.singer-name-input").TriggerEvent("onfocusout", EventArgs.Empty);
        Assert.Contains("Alice", cut.Find(".singer-header h3").TextContent);
    }

    [Fact]
    public void EditName_ConfirmWithWhitespaceOnly_TreatedAsEmpty()
    {
        // Arrange
        var sessionState = new SessionState { CurrentSession = _testSessionWithNameRequired, IsInitialized = true };
        var libraryState = new LibraryState { Songs = _testSongs };
        SetupTestWithSession(sessionState, new PlaylistState(), libraryState, "singer", false);
        var cut = RenderSingerViewComponent(_testSessionWithNameRequired.SessionId);

        // Enter initial name
        cut.Find("input#singerNameInput").Input("Alice");
        cut.Find("button.k-btn-primary").Click();

        // Enter edit mode and set whitespace-only value
        cut.Find("button.singer-edit-btn").Click();
        cut.Find("input.singer-name-input").Input("   ");

        // Act: click confirm
        cut.Find("button.singer-name-confirm").Click();

        // Assert: treated as empty — is-invalid shown, edit mode still open, original name intact
        var editInput = cut.Find("input.singer-name-input");
        Assert.NotNull(editInput);
        Assert.Contains("is-invalid", editInput.ClassName);

        // Cancel edit and verify original name was preserved
        cut.Find("input.singer-name-input").TriggerEvent("onfocusout", EventArgs.Empty);
        Assert.Contains("Alice", cut.Find(".singer-header h3").TextContent);
    }

    [Fact]
    public void EditName_AfterErrorState_ValidNameSavesAndClearsError()
    {
        // Arrange
        var sessionState = new SessionState { CurrentSession = _testSessionWithNameRequired, IsInitialized = true };
        var libraryState = new LibraryState { Songs = _testSongs };
        SetupTestWithSession(sessionState, new PlaylistState(), libraryState, "singer", false);
        var cut = RenderSingerViewComponent(_testSessionWithNameRequired.SessionId);

        // Enter initial name
        cut.Find("input#singerNameInput").Input("Alice");
        cut.Find("button.k-btn-primary").Click();

        // Enter edit mode, submit empty to trigger error state
        cut.Find("button.singer-edit-btn").Click();
        cut.Find("input.singer-name-input").Input(string.Empty);
        cut.Find("button.singer-name-confirm").Click();

        // Verify error state is active
        Assert.Contains("is-invalid", cut.Find("input.singer-name-input").ClassName);

        // Act: type a valid name and confirm
        cut.Find("input.singer-name-input").Input("AliceNew");
        cut.Find("button.singer-name-confirm").Click();

        // Assert: edit mode exits, error class gone, new name shown
        Assert.Throws<ElementNotFoundException>(() => cut.Find("input.singer-name-input"));
        Assert.Contains("AliceNew", cut.Find(".singer-header h3").TextContent);
    }

    // ── Phase 5: Edit Controls Hidden When RequireSingerName Is Disabled (US4) ─

    [Fact]
    public void EditName_WhenRequireSingerNameFalse_NoPencilIconRendered()
    {
        // Arrange
        var sessionState = new SessionState { CurrentSession = _testSessionWithoutNameRequired, IsInitialized = true };
        SetupTestWithSession(sessionState, new PlaylistState(), new LibraryState(), "singer", false);

        // Act
        var cut = RenderSingerViewComponent(_testSessionWithoutNameRequired.SessionId);

        // Assert: button.singer-edit-btn must not exist in the DOM
        Assert.Throws<ElementNotFoundException>(() => cut.Find("button.singer-edit-btn"));
    }

    [Fact]
    public void EditName_WhenRequireSingerNameFalse_ClickingNameDoesNotTriggerEditMode()
    {
        // Arrange
        var sessionState = new SessionState { CurrentSession = _testSessionWithoutNameRequired, IsInitialized = true };
        SetupTestWithSession(sessionState, new PlaylistState(), new LibraryState(), "singer", false);
        var cut = RenderSingerViewComponent(_testSessionWithoutNameRequired.SessionId);

        // Assert: the h3 in the singer-header has no onclick attribute — edit mode is structurally unreachable
        var heading = cut.Find(".singer-header h3");
        Assert.False(heading.HasAttribute("onclick"), "h3 must not have an onclick when RequireSingerName is false");

        // Assert: no edit input in DOM
        Assert.Throws<ElementNotFoundException>(() => cut.Find("input.singer-name-input"));
    }
}

