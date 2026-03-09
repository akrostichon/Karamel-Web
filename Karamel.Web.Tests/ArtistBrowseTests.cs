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
/// bUnit tests for the artist browse mode in LibrarySearch.
/// Covers US1: artist list visible when search is empty.
/// </summary>
public class ArtistBrowseTests : TestContext
{
    private readonly List<ArtistItem> _testArtists = new()
    {
        new ArtistItem("ABBA", 3),
        new ArtistItem("Queen", 5),
        new ArtistItem("The Beatles", 12),
    };

    // ── Rendering: artist list ────────────────────────────────────────────

    [Fact]
    public void Component_ShowsArtistRows_WhenSearchEmpty_And_ArtistsLoaded()
    {
        // Arrange
        var state = new LibraryState
        {
            SearchFilter = string.Empty,
            ScanComplete = true,
            ArtistsLoaded = true,
            Artists = _testArtists
        };
        SetupFluxorWithState(state);

        // Act
        var cut = RenderComponent<LibrarySearch>();

        // Assert – three rows, each showing name and song count
        var rows = cut.FindAll(".artist-row");
        Assert.Equal(3, rows.Count);

        Assert.Contains("ABBA", rows[0].TextContent);
        Assert.Contains("3", rows[0].TextContent);

        Assert.Contains("Queen", rows[1].TextContent);
        Assert.Contains("5", rows[1].TextContent);

        Assert.Contains("The Beatles", rows[2].TextContent);
        Assert.Contains("12", rows[2].TextContent);
    }

    [Fact]
    public void Component_ShowsSpinner_WhenIsLoadingArtists()
    {
        // Arrange
        var state = new LibraryState
        {
            SearchFilter = string.Empty,
            ScanComplete = true,
            IsLoadingArtists = true
        };
        SetupFluxorWithState(state);

        // Act
        var cut = RenderComponent<LibrarySearch>();

        // Assert – artist loading spinner is visible
        var spinner = cut.Find(".artist-list-loader .spinner-border");
        Assert.NotNull(spinner);
    }

    [Fact]
    public void Component_HidesSongResultsTable_WhenInBrowseMode()
    {
        // Arrange – search empty + artists loaded → artist browse branch active
        var state = new LibraryState
        {
            SearchFilter = string.Empty,
            ScanComplete = true,
            ArtistsLoaded = true,
            Artists = _testArtists
        };
        SetupFluxorWithState(state);

        // Act
        var cut = RenderComponent<LibrarySearch>();

        // Assert – songs table is not rendered in browse mode
        var songsTables = cut.FindAll("table.table-striped");
        Assert.Empty(songsTables);

        // Artist list is rendered instead
        var artistList = cut.Find(".artist-list");
        Assert.NotNull(artistList);
    }

    // ── Init dispatch ─────────────────────────────────────────────────────

    [Fact]
    public void Component_DispatchesLoadArtistsAction_OnInit_WhenScanComplete_And_ArtistsNotLoaded()
    {
        // Arrange
        var state = new LibraryState
        {
            SearchFilter = string.Empty,
            ScanComplete = true,
            ArtistsLoaded = false,
            IsLoadingArtists = false
        };
        var mockDispatcher = SetupFluxorWithState(state);

        // Act
        var cut = RenderComponent<LibrarySearch>();

        // Assert – LoadArtistsAction dispatched exactly once on init
        mockDispatcher.Verify(
            d => d.Dispatch(It.IsAny<LoadArtistsAction>()),
            Times.Once
        );
    }

    [Fact]
    public void Component_DoesNotDispatchLoadArtistsAction_WhenArtistsAlreadyLoaded()
    {
        // Arrange – artist cache is warm, no re-fetch needed
        var state = new LibraryState
        {
            SearchFilter = string.Empty,
            ScanComplete = true,
            ArtistsLoaded = true,
            Artists = _testArtists
        };
        var mockDispatcher = SetupFluxorWithState(state);

        // Act
        var cut = RenderComponent<LibrarySearch>();

        // Assert – no LoadArtistsAction dispatched
        mockDispatcher.Verify(
            d => d.Dispatch(It.IsAny<LoadArtistsAction>()),
            Times.Never
        );
    }

    [Fact]
    public void Component_DoesNotDispatchLoadArtistsAction_WhenScanNotComplete()
    {
        // Arrange – scan still in progress
        var state = new LibraryState
        {
            SearchFilter = string.Empty,
            ScanComplete = false,
            ArtistsLoaded = false,
            IsLoadingArtists = false
        };
        var mockDispatcher = SetupFluxorWithState(state);

        // Act
        var cut = RenderComponent<LibrarySearch>();

        // Assert – no LoadArtistsAction dispatched
        mockDispatcher.Verify(
            d => d.Dispatch(It.IsAny<LoadArtistsAction>()),
            Times.Never
        );
    }

    // ── Reducer: ResetPaginationAction clears artist cache ────────────────

    [Fact]
    public void Reducer_ResetPagination_ClearsArtistFields()
    {
        // Arrange – state with warm artist cache
        var state = new LibraryState
        {
            Artists = _testArtists,
            IsLoadingArtists = false,
            ArtistsLoaded = true,
            CurrentPage = 3,
            TotalCount = 100
        };

        // Act
        var result = LibraryReducers.ReduceResetPagination(state, new ResetPaginationAction());

        // Assert – artist fields are cleared
        Assert.Empty(result.Artists);
        Assert.False(result.ArtistsLoaded);
        Assert.False(result.IsLoadingArtists);
    }

    // ── Browse mode not activated until scan complete ─────────────────────

    [Fact]
    public void Component_BrowseBranchNotShown_WhenScanNotComplete()
    {
        // Arrange – songs exist but scan is not complete yet
        var state = new LibraryState
        {
            Songs = new List<Song>
            {
                new Song { Artist = "ABBA", Title = "Dancing Queen" }
            },
            ScanComplete = false,
            SearchFilter = string.Empty
        };
        SetupFluxorWithState(state);

        // Act
        var cut = RenderComponent<LibrarySearch>();

        // Assert – artist browse branch is not shown; falls through to songs table
        var artistRows = cut.FindAll(".artist-row");
        Assert.Empty(artistRows);
    }

    // ── Helper ────────────────────────────────────────────────────────────

    private Mock<IDispatcher> SetupFluxorWithState(LibraryState state)
    {
        var mockDispatcher = new Mock<IDispatcher>();
        var mockState = new Mock<IState<LibraryState>>();
        var mockActionSubscriber = new Mock<IActionSubscriber>();
        var mockSessionState = new Mock<IState<Store.Session.SessionState>>();

        mockState.Setup(s => s.Value).Returns(state);
        mockSessionState.Setup(s => s.Value).Returns(
            new Store.Session.SessionState { CurrentSession = new Session { SessionId = Guid.NewGuid() } });

        Services.AddSingleton(mockDispatcher.Object);
        Services.AddSingleton(mockState.Object);
        Services.AddSingleton(mockActionSubscriber.Object);
        Services.AddSingleton(mockSessionState.Object);

        var mockConnectionManager = new Mock<Services.ISignalRConnectionManager>();
        mockConnectionManager.Setup(m => m.IsMainTab).Returns(true);
        Services.AddSingleton(mockConnectionManager.Object);

        var mockSessionApiClient = new Mock<Services.ISessionApiClient>();
        Services.AddSingleton(mockSessionApiClient.Object);

        var mockSignalRBridge = new Mock<Services.ISignalRPlaylistBridge>();
        Services.AddSingleton(mockSignalRBridge.Object);

        return mockDispatcher;
    }
}
