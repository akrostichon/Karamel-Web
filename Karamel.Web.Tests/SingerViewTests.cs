using Bunit;
using Fluxor;
using Karamel.Web.Components;
using Karamel.Web.Models;
using Karamel.Web.Pages;
using Karamel.Web.Store.Library;
using Karamel.Web.Store.Playlist;
using Karamel.Web.Store.Session;
using Microsoft.Extensions.DependencyInjection;
using Moq;
using Xunit;

namespace Karamel.Web.Tests;

/// <summary>
/// Unit tests for the SingerView component.
/// Tests name entry, library search integration, song limit enforcement, and toast notifications.
/// </summary>
public class SingerViewTests : SessionTestBase
{
    private readonly List<Song> _testSongs;
    private readonly Models.Session _testSessionWithNameRequired;
    private readonly Models.Session _testSessionWithoutNameRequired;

    public SingerViewTests()
    {
        // Setup test songs
        _testSongs = new List<Song>
        {
            new Song { Id = Guid.NewGuid(), Artist = "Beatles", Title = "Let It Be", Mp3FileName = "beatles-let-it-be.mp3", CdgFileName = "beatles-let-it-be.cdg" },
            new Song { Id = Guid.NewGuid(), Artist = "Queen", Title = "Bohemian Rhapsody", Mp3FileName = "queen-bohemian-rhapsody.mp3", CdgFileName = "queen-bohemian-rhapsody.cdg" }
        };

        _testSessionWithNameRequired = new Models.Session
        {
            SessionId = Guid.NewGuid(),
            LibraryPath = "C:\\Karaoke",
            RequireSingerName = true
        };

        _testSessionWithoutNameRequired = new Models.Session
        {
            SessionId = Guid.NewGuid(),
            LibraryPath = "C:\\Karaoke",
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
        
        var continueButton = cut.Find("button.btn-primary");
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
        var continueButton = cut.Find("button.btn-primary");

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
        nameInput.Change("John");
        var continueButton = cut.Find("button.btn-primary");

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
        nameInput.Change("J");
        var continueButton = cut.Find("button.btn-primary");
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
        nameInput.Change("John Doe");
        var continueButton = cut.Find("button.btn-primary");
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
        nameInput.Change("  John Doe  ");
        var continueButton = cut.Find("button.btn-primary");
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
        nameInput.Change("Jane Smith");
        nameInput.KeyUp("Enter");

        // Assert
        var librarySearch = cut.FindComponent<LibrarySearch>();
        Assert.NotNull(librarySearch);
        
        var header = cut.Find(".singer-header h3");
        Assert.Contains("Welcome, Jane Smith!", header.TextContent);
    }

    [Fact]
    public void LibraryView_DisplaysSongCount_FromPlaylistState()
    {
        // Arrange
        var sessionState = new SessionState { CurrentSession = _testSessionWithoutNameRequired, IsInitialized = true };
        var libraryState = new LibraryState { Songs = _testSongs };
        var playlistState = new PlaylistState
        {
            SingerSongCounts = new Dictionary<string, int> { { "John", 3 } }
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
        nameInput.Change("Alice");
        var continueButton = cut.Find("button.btn-primary");
        continueButton.Click();

        // Act
        var librarySearch = cut.FindComponent<LibrarySearch>();
        var addButtons = librarySearch.FindAll("button.btn-primary");
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
        var addButtons = librarySearch.FindAll("button.btn-primary");
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
            Queue = new Queue<Song>(new[] { _testSongs[0] })
        };
        var (actionSubscriber, _, _) = SetupTestWithSession(sessionState, playlistState, libraryState, view: "singer");
        
        var cut = RenderComponent<SingerView>();
        
        // Enter name
        var nameInput = cut.Find("input#singerNameInput");
        nameInput.Change("Bob");
        var continueButton = cut.Find("button.btn-primary");
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
            SingerSongCounts = new Dictionary<string, int> { { "Charlie", 10 } }
        };
        var (actionSubscriber, _, _) = SetupTestWithSession(sessionState, playlistState, libraryState, view: "singer");
        
        var cut = RenderComponent<SingerView>();
        
        // Enter name
        var nameInput = cut.Find("input#singerNameInput");
        nameInput.Change("Charlie");
        var continueButton = cut.Find("button.btn-primary");
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
            SingerSongCounts = new Dictionary<string, int> 
            { 
                { "David", 3 },
                { "Eve", 5 }
            }
        };
        SetupTestWithSession(sessionState, playlistState, libraryState, view: "singer");
        
        var cut = RenderComponent<SingerView>();
        
        // Enter name
        var nameInput = cut.Find("input#singerNameInput");
        nameInput.Change("David");
        var continueButton = cut.Find("button.btn-primary");
        continueButton.Click();

        // Assert
        var songCount = cut.Find(".song-count");
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
        nameInput.Change("José María O'Neill");
        var continueButton = cut.Find("button.btn-primary");
        continueButton.Click();

        // Assert
        var header = cut.Find(".singer-header h3");
        Assert.Contains("Welcome, José María O'Neill!", header.TextContent);
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

}
