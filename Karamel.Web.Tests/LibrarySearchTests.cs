using Bunit;
using Fluxor;
using Karamel.Web.Components;
using Karamel.Web.Models;
using Karamel.Web.Store.Library;
using Karamel.Web.Store.Playlist;
using Microsoft.Extensions.DependencyInjection;
using Moq;

namespace Karamel.Web.Tests;

/// <summary>
/// Unit tests for the LibrarySearch component.
/// Tests filtering, sorting, display, and AddToPlaylist action dispatch.
/// </summary>
public class LibrarySearchTests : TestContext
{
    private readonly List<Song> _testSongs;

    public LibrarySearchTests()
    {
        // Setup test songs
        _testSongs = new List<Song>
        {
            new Song { Artist = "Beatles", Title = "Let It Be", Mp3FileName = "beatles-let-it-be.mp3", CdgFileName = "beatles-let-it-be.cdg" },
            new Song { Artist = "Queen", Title = "Bohemian Rhapsody", Mp3FileName = "queen-bohemian-rhapsody.mp3", CdgFileName = "queen-bohemian-rhapsody.cdg" },
            new Song { Artist = "ABBA", Title = "Dancing Queen", Mp3FileName = "abba-dancing-queen.mp3", CdgFileName = "abba-dancing-queen.cdg" },
            new Song { Artist = "Beatles", Title = "Yesterday", Mp3FileName = "beatles-yesterday.mp3", CdgFileName = "beatles-yesterday.cdg" },
            new Song { Artist = "Elvis Presley", Title = "Can't Help Falling in Love", Mp3FileName = "elvis-cant-help.mp3", CdgFileName = "elvis-cant-help.cdg" }
        };
    }

    [Fact]
    public void Component_WhenLibraryIsEmpty_ShowsEmptyMessage()
    {
        // Arrange
        var state = new LibraryState { Songs = Array.Empty<Song>() };
        SetupFluxorWithState(state);

        // Act
        var cut = RenderComponent<LibrarySearch>();

        // Assert
        var alert = cut.Find(".alert-info");
        Assert.Contains("No songs in library", alert.TextContent);
    }

    [Fact]
    public void Component_WhenLoading_ShowsSpinner()
    {
        // Arrange
        var state = new LibraryState { IsLoading = true };
        SetupFluxorWithState(state);

        // Act
        var cut = RenderComponent<LibrarySearch>();

        // Assert
        var spinner = cut.Find(".spinner-border");
        Assert.NotNull(spinner);
    }

    [Fact]
    public void Component_WhenError_ShowsErrorMessage()
    {
        // Arrange
        var errorMessage = "Failed to load library";
        var state = new LibraryState { ErrorMessage = errorMessage };
        SetupFluxorWithState(state);

        // Act
        var cut = RenderComponent<LibrarySearch>();

        // Assert
        var alert = cut.Find(".alert-danger");
        Assert.Contains(errorMessage, alert.TextContent);
    }

    [Fact]
    public void Component_DisplaysSongsInTable_SortedByArtistThenTitle()
    {
        // Arrange
        var state = new LibraryState { Songs = _testSongs };
        SetupFluxorWithState(state);

        // Act
        var cut = RenderComponent<LibrarySearch>();

        // Assert
        var rows = cut.FindAll("tbody tr");
        Assert.Equal(5, rows.Count);

        // Check sorting: ABBA, Beatles (2), Elvis, Queen
        Assert.Contains("ABBA", rows[0].TextContent);
        Assert.Contains("Dancing Queen", rows[0].TextContent);
        
        Assert.Contains("Beatles", rows[1].TextContent);
        Assert.Contains("Let It Be", rows[1].TextContent);
        
        Assert.Contains("Beatles", rows[2].TextContent);
        Assert.Contains("Yesterday", rows[2].TextContent);
        
        Assert.Contains("Elvis Presley", rows[3].TextContent);
        
        Assert.Contains("Queen", rows[4].TextContent);
    }

    [Fact]
    public async Task SearchBox_CallsServerSearch_WhenTextChangesAsync()
    {
        // Arrange
        var searchTerm = "Beatles";
        var beatlesSongs = _testSongs.Where(s => s.Artist.Contains(searchTerm, StringComparison.OrdinalIgnoreCase)).ToList();
        
        var state = new LibraryState { Songs = _testSongs };
        var sessionId = Guid.NewGuid();
        
        var mockDispatcher = new Mock<IDispatcher>();
        var mockState = new Mock<IState<LibraryState>>();
        var mockActionSubscriber = new Mock<IActionSubscriber>();
        var mockSessionState = new Mock<IState<Karamel.Web.Store.Session.SessionState>>();
        var mockSessionService = new Mock<Karamel.Web.Services.ISessionService>();
        
        mockState.Setup(s => s.Value).Returns(state);
        mockSessionState.Setup(s => s.Value).Returns(new Karamel.Web.Store.Session.SessionState { 
            CurrentSession = new Session { SessionId = sessionId, LibraryPath = "/test/library" } 
        });
        
        // Mock SearchLibraryAsync for suggestions (returns empty array)
        var suggestionJson = System.Text.Json.JsonDocument.Parse("[]").RootElement;
        mockSessionService.Setup(s => s.SearchLibraryAsync(sessionId, searchTerm, 8))
            .ReturnsAsync(suggestionJson);
        
        // Mock FetchLibraryPageAsync for full results with proper structure including IDs
        var pageJson = System.Text.Json.JsonDocument.Parse(
            $"{{\"items\": [{string.Join(",", beatlesSongs.Select(s => $"{{\"id\":\"{s.Id}\",\"artist\":\"{s.Artist}\",\"title\":\"{s.Title}\",\"mp3FileName\":\"{s.Mp3FileName}\",\"cdgFileName\":\"{s.CdgFileName}\"}}"))}], \"totalCount\": {beatlesSongs.Count}}}"
        ).RootElement;
        mockSessionService.Setup(s => s.FetchLibraryPageAsync(sessionId, 1, 50, searchTerm, null))
            .ReturnsAsync(pageJson);

        Services.AddSingleton(mockDispatcher.Object);
        Services.AddSingleton(mockState.Object);
        Services.AddSingleton(mockActionSubscriber.Object);
        Services.AddSingleton(mockSessionState.Object);
        Services.AddSingleton(mockSessionService.Object);
        
        var cut = RenderComponent<LibrarySearch>();
        var searchInput = cut.Find("input[type='text']");

        // Act
        searchInput.Input(searchTerm);
        
        // Wait for debounce (300ms) + processing time
        await Task.Delay(500);
        
        // Trigger render to process any pending state changes
        cut.Render();

        // Assert - Component should call server and dispatch the results
        mockSessionService.Verify(s => s.SearchLibraryAsync(sessionId, searchTerm, 8), Times.Once);
        mockSessionService.Verify(s => s.FetchLibraryPageAsync(sessionId, 1, 50, searchTerm, null), Times.Once);
        mockDispatcher.Verify(d => d.Dispatch(It.IsAny<LoadLibrarySuccessAction>()), Times.Once);
    }

    [Fact]
    public void FilteredSongs_ShowsOnlyMatchingArtists_CaseInsensitive()
    {
        // Arrange
        var state = new LibraryState 
        { 
            Songs = _testSongs,
            SearchFilter = "beatles"
        };
        SetupFluxorWithState(state);

        // Act
        var cut = RenderComponent<LibrarySearch>();

        // Assert
        var rows = cut.FindAll("tbody tr");
        Assert.Equal(2, rows.Count); // Two Beatles songs
        Assert.All(rows, row => Assert.Contains("Beatles", row.TextContent));
    }

    [Fact]
    public void FilteredSongs_ShowsOnlyMatchingTitles_CaseInsensitive()
    {
        // Arrange
        var state = new LibraryState 
        { 
            Songs = _testSongs,
            SearchFilter = "QUEEN"
        };
        SetupFluxorWithState(state);

        // Act
        var cut = RenderComponent<LibrarySearch>();

        // Assert
        var rows = cut.FindAll("tbody tr");
        Assert.Equal(2, rows.Count); // "Dancing Queen" and band "Queen"
    }

    [Fact]
    public void FilteredSongs_WhenNoMatches_ShowsNoResultsMessage()
    {
        // Arrange
        var state = new LibraryState 
        { 
            Songs = _testSongs,
            SearchFilter = "NonexistentArtist"
        };
        SetupFluxorWithState(state);

        // Act
        var cut = RenderComponent<LibrarySearch>();

        // Assert
        var alert = cut.Find(".alert-info");
        Assert.Contains("No songs match your search criteria", alert.TextContent);
    }

    [Fact]
    public void AddToQueueButton_DispatchesAddToPlaylistAction_WithCorrectSong()
    {
        // Arrange
        var state = new LibraryState { Songs = _testSongs };
        var dispatcher = SetupFluxorWithState(state);
        var cut = RenderComponent<LibrarySearch>();

        // Act
        var addButtons = cut.FindAll("button.k-btn-primary");
        addButtons[0].Click(); // Click first song's Add button

        // Assert
        dispatcher.Verify(d => d.Dispatch(It.Is<AddToPlaylistAction>(
            a => a.Song.Artist == "ABBA" && a.Song.Title == "Dancing Queen"
        )), Times.Once);
    }

    [Fact]
    public void AddToQueueButton_ShowsSuccessToast_AfterAdding()
    {
        // Arrange
        var state = new LibraryState { Songs = _testSongs };
        SetupFluxorWithState(state);
        var cut = RenderComponent<LibrarySearch>();

        // Act
        var addButtons = cut.FindAll("button.k-btn-primary");
        addButtons[0].Click();

        // Assert
        var toast = cut.Find(".toast");
        Assert.NotNull(toast);
        Assert.Contains("Success", toast.TextContent);
        Assert.Contains("ABBA", toast.TextContent);
        Assert.Contains("Dancing Queen", toast.TextContent);
        Assert.Contains("added to queue", toast.TextContent);
    }

    [Fact]
    public void Component_ShowsSongCount_AtBottom()
    {
        // Arrange
        var state = new LibraryState { Songs = _testSongs };
        SetupFluxorWithState(state);

        // Act
        var cut = RenderComponent<LibrarySearch>();

        // Assert
        var countText = cut.Find(".form-text.small");
        Assert.Contains("Showing 5 of 5 songs", countText.TextContent);
    }

    [Fact]
    public void Component_ShowsFilteredCount_WhenSearching()
    {
        // Arrange
        var state = new LibraryState 
        { 
            Songs = _testSongs,
            SearchFilter = "Beatles"
        };
        SetupFluxorWithState(state);

        // Act
        var cut = RenderComponent<LibrarySearch>();

        // Assert
        var countText = cut.Find(".form-text.small");
        Assert.Contains("Showing 2 of 5 songs", countText.TextContent);
    }

    [Fact]
    public void Component_HandlesSpecialCharacters_InSearch()
    {
        // Arrange
        var songsWithSpecialChars = new List<Song>
        {
            new Song { Artist = "AC/DC", Title = "Back in Black", Mp3FileName = "acdc.mp3", CdgFileName = "acdc.cdg" },
            new Song { Artist = "Guns N' Roses", Title = "Sweet Child O' Mine", Mp3FileName = "gnr.mp3", CdgFileName = "gnr.cdg" }
        };
        var state = new LibraryState 
        { 
            Songs = songsWithSpecialChars,
            SearchFilter = "AC/DC"
        };
        SetupFluxorWithState(state);

        // Act
        var cut = RenderComponent<LibrarySearch>();

        // Assert
        var rows = cut.FindAll("tbody tr");
        Assert.Single(rows);
        Assert.Contains("AC/DC", rows[0].TextContent);
    }

    [Fact]
    public void AddButton_HasMicrophoneIcon()
    {
        // Arrange
        var state = new LibraryState { Songs = _testSongs };
        SetupFluxorWithState(state);

        // Act
        var cut = RenderComponent<LibrarySearch>();

        // Assert
        var addButtons = cut.FindAll("button.k-btn-primary");
        Assert.All(addButtons, button => 
        {
            var icon = button.QuerySelector("i.bi-mic-fill");
            Assert.NotNull(icon);
        });
    }

    private Mock<IDispatcher> SetupFluxorWithState(LibraryState state)
    {
        var mockDispatcher = new Mock<IDispatcher>();
        var mockState = new Mock<IState<LibraryState>>();
        var mockActionSubscriber = new Mock<IActionSubscriber>();
        var mockSessionState = new Mock<IState<Karamel.Web.Store.Session.SessionState>>();
        var mockSessionService = new Mock<Karamel.Web.Services.ISessionService>();
        
        mockState.Setup(s => s.Value).Returns(state);
        mockSessionState.Setup(s => s.Value).Returns(new Karamel.Web.Store.Session.SessionState { CurrentSession = new Session { SessionId = Guid.NewGuid(), LibraryPath = "/test/library" } });

        Services.AddSingleton(mockDispatcher.Object);
        Services.AddSingleton(mockState.Object);
        Services.AddSingleton(mockActionSubscriber.Object);
        Services.AddSingleton(mockSessionState.Object);
        Services.AddSingleton(mockSessionService.Object);

        return mockDispatcher;
    }
}
