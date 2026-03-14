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

        // Artist browse container is rendered instead
        var artistBrowse = cut.Find(".artist-browse");
        Assert.NotNull(artistBrowse);
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

    // ── US2: Artist selection (click → dispatch) ─────────────────────────

    [Fact]
    public void ClickingArtistRow_DispatchesFilterSongsAction_WithArtistName()
    {
        // Arrange
        var state = new LibraryState
        {
            SearchFilter = string.Empty,
            ScanComplete = true,
            ArtistsLoaded = true,
            Artists = _testArtists
        };
        var mockDispatcher = SetupFluxorWithState(state);
        var cut = RenderComponent<LibrarySearch>();

        // Act – click first artist row ("ABBA")
        cut.Find(".artist-row").Click();

        // Assert – FilterSongsAction dispatched with the correct artist name
        mockDispatcher.Verify(
            d => d.Dispatch(It.Is<FilterSongsAction>(a => a.SearchFilter == "ABBA")),
            Times.Once
        );
    }

    [Fact]
    public void ClickingArtistRow_DispatchesLoadPageAction_WithArtistNameAndPage1()
    {
        // Arrange
        var state = new LibraryState
        {
            SearchFilter = string.Empty,
            ScanComplete = true,
            ArtistsLoaded = true,
            Artists = _testArtists
        };
        var mockDispatcher = SetupFluxorWithState(state);
        var cut = RenderComponent<LibrarySearch>();

        // Act – click first artist row ("ABBA")
        cut.Find(".artist-row").Click();

        // Assert – LoadPageAction dispatched with page 1, ArtistFilter carrying the name, SearchQuery null, not appending
        mockDispatcher.Verify(
            d => d.Dispatch(It.Is<LoadPageAction>(a => a.Page == 1 && a.ArtistFilter == "ABBA" && a.SearchQuery == null && !a.Append)),
            Times.Once
        );
    }

    [Fact]
    public void Component_HidesArtistList_WhenSearchFilterIsNonEmpty()
    {
        // Arrange – state as it would be after SelectArtist dispatch updates SearchFilter
        var state = new LibraryState
        {
            SearchFilter = "ABBA",
            ScanComplete = true,
            ArtistsLoaded = true,
            Artists = _testArtists
        };
        SetupFluxorWithState(state);

        // Act
        var cut = RenderComponent<LibrarySearch>();

        // Assert – browse branch is not rendered when search filter is active
        var artistRows = cut.FindAll(".artist-row");
        Assert.Empty(artistRows);
    }

    [Fact]
    public void ClickingArtistRow_FilterSongsAction_SearchFilter_MatchesArtistName()
    {
        // Arrange – verify that the dispatched FilterSongsAction carries the
        // tapped artist name (i.e. SearchFilter state will reflect the selection)
        var state = new LibraryState
        {
            SearchFilter = string.Empty,
            ScanComplete = true,
            ArtistsLoaded = true,
            Artists = _testArtists
        };
        var mockDispatcher = SetupFluxorWithState(state);

        FilterSongsAction? captured = null;
        mockDispatcher
            .Setup(d => d.Dispatch(It.IsAny<FilterSongsAction>()))
            .Callback<object>(a => captured = (FilterSongsAction)a);

        var cut = RenderComponent<LibrarySearch>();

        // Act – click third artist row ("The Beatles")
        cut.FindAll(".artist-row")[2].Click();

        // Assert – the action carries the precise artist name
        Assert.NotNull(captured);
        Assert.Equal("The Beatles", captured!.SearchFilter);
    }

    // ── US3: Artist list cache hit and no-refetch after clear ─────────────

    [Fact]
    public void ArtistList_ReappearsInstantly_AfterClearFilter_WhenCacheIsWarm()
    {
        // Arrange – state as it would be after ClearFilter() when artists are cached
        var state = new LibraryState
        {
            SearchFilter = string.Empty,
            ScanComplete = true,
            ArtistsLoaded = true,
            Artists = _testArtists,
            IsLoadingArtists = false
        };
        var mockDispatcher = SetupFluxorWithState(state);

        // Act
        var cut = RenderComponent<LibrarySearch>();

        // Assert – artist list shows immediately with no spinner and no fetch
        var artistRows = cut.FindAll(".artist-row");
        Assert.Equal(3, artistRows.Count);

        var loaderSpinners = cut.FindAll(".artist-list-loader");
        Assert.Empty(loaderSpinners);

        mockDispatcher.Verify(
            d => d.Dispatch(It.IsAny<LoadArtistsAction>()),
            Times.Never
        );
    }

    [Fact]
    public void ClearFilter_TriggersArtistLoad_WhenCacheIsEmpty()
    {
        // Arrange – filter active (X button shown), scan complete, cache empty
        var state = new LibraryState
        {
            SearchFilter = "ABBA",
            ScanComplete = true,
            ArtistsLoaded = false,
            IsLoadingArtists = false
        };
        var mockDispatcher = SetupFluxorWithState(state);
        var cut = RenderComponent<LibrarySearch>();
        // OnInit already dispatched LoadArtistsAction once

        // Act – click the X button to clear the search
        cut.Find(".clear-filter-btn").Click();

        // Assert – LoadArtistsAction dispatched twice: once on init, once by ClearFilter
        mockDispatcher.Verify(
            d => d.Dispatch(It.IsAny<LoadArtistsAction>()),
            Times.Exactly(2)
        );
    }

    [Fact]
    public void OnSearchInput_ClearedToEmpty_TriggersArtistLoad_WhenCacheIsEmpty()
    {
        // Arrange – scan complete, cache empty
        var state = new LibraryState
        {
            SearchFilter = "ABBA",
            ScanComplete = true,
            ArtistsLoaded = false,
            IsLoadingArtists = false
        };
        var mockDispatcher = SetupFluxorWithState(state);
        var cut = RenderComponent<LibrarySearch>();
        // OnInit already dispatched LoadArtistsAction once

        // Act – simulate user deleting all text in the search input
        cut.Find("input[type='text']").Input(string.Empty);

        // Assert – LoadArtistsAction dispatched twice: once on init, once from the
        // empty-string branch of OnSearchInput → TryLoadArtistsIfReady
        mockDispatcher.Verify(
            d => d.Dispatch(It.IsAny<LoadArtistsAction>()),
            Times.Exactly(2)
        );
    }

    // ── US1: Alphabet bar rendering ───────────────────────────────────────

    [Fact]
    public void AlphabetBar_Renders26LetterButtons_WhenArtistsLoaded()
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

        // Assert — exactly 27 buttons in the alphabet bar (# + A–Z)
        var buttons = cut.FindAll(".alpha-btn");
        Assert.Equal(27, buttons.Count);
    }

    [Fact]
    public void AlphabetBar_ActiveLetters_EnabledInactiveLetters_Disabled()
    {
        // Arrange — ABBA → A, Queen → Q, The Beatles → T
        var state = new LibraryState
        {
            SearchFilter = string.Empty,
            ScanComplete = true,
            ArtistsLoaded = true,
            Artists = _testArtists
        };
        SetupFluxorWithState(state);
        var cut = RenderComponent<LibrarySearch>();

        // Assert active letters A, Q, T are NOT disabled
        var activeLetters = new[] { 'A', 'Q', 'T' };
        foreach (var letter in activeLetters)
        {
            var btn = cut.Find($"button.alpha-btn[title='Jump to {letter}']");
            Assert.False(btn.HasAttribute("disabled"), $"Letter '{letter}' should be active (no disabled attr)");
        }

        // Assert a sample of inactive letters are disabled
        var inactiveLetters = new[] { 'B', 'C', 'D', 'Z' };
        foreach (var letter in inactiveLetters)
        {
            var btn = cut.Find($"button.alpha-btn[title='Jump to {letter}']");
            Assert.True(btn.HasAttribute("disabled"), $"Letter '{letter}' should be inactive (disabled attr present)");
        }
    }

    [Fact]
    public void AlphabetBar_SectionHeaders_OnePerLetterGroup()
    {
        // Arrange — 3 artists spanning 3 distinct letters → 3 section headers
        var state = new LibraryState
        {
            SearchFilter = string.Empty,
            ScanComplete = true,
            ArtistsLoaded = true,
            Artists = _testArtists
        };
        SetupFluxorWithState(state);
        var cut = RenderComponent<LibrarySearch>();

        // Assert — one header per letter group (A, Q, T)
        var headers = cut.FindAll(".artist-section-header");
        Assert.Equal(3, headers.Count);
        Assert.Contains(headers, h => h.TextContent.Trim() == "A");
        Assert.Contains(headers, h => h.TextContent.Trim() == "Q");
        Assert.Contains(headers, h => h.TextContent.Trim() == "T");
    }

    [Fact]
    public void AlphabetBar_ClickActiveLetterButton_CallsScrollToArtistSection()
    {
        // Arrange — register the module BEFORE rendering so bUnit routes the import
        var moduleSetup = JSInterop.SetupModule("./js/alphabetBridge.js");
        moduleSetup.SetupVoid("observeArtistSections", _ => true);
        moduleSetup.SetupVoid("scrollToArtistSection", _ => true);
        moduleSetup.SetupVoid("disconnectArtistSectionObserver", _ => true);

        var state = new LibraryState
        {
            SearchFilter = string.Empty,
            ScanComplete = true,
            ArtistsLoaded = true,
            Artists = _testArtists
        };
        SetupFluxorWithState(state);
        var cut = RenderComponent<LibrarySearch>();

        // Act — click the first non-disabled letter button (should be 'A')
        cut.Find("button.alpha-btn:not([disabled])").Click();

        // Assert — scrollToArtistSection called with the letter as a string
        moduleSetup.VerifyInvoke("scrollToArtistSection", 1);
        var args = moduleSetup.Invocations["scrollToArtistSection"][0].Arguments;
        Assert.Equal("A", args[0]?.ToString());
    }

    [Fact]
    public void AlphabetBar_AbsentWhenScanNotComplete()
    {
        // Arrange — scan not done yet
        var state = new LibraryState
        {
            SearchFilter = string.Empty,
            ScanComplete = false,
            ArtistsLoaded = false
        };
        SetupFluxorWithState(state);
        var cut = RenderComponent<LibrarySearch>();

        // Assert — no alphabet bar rendered
        var navs = cut.FindAll(".alphabet-bar");
        Assert.Empty(navs);
    }

    [Fact]
    public void AlphabetBar_AbsentWhenSearchFilterActive()
    {
        // Arrange — search filter active means browse mode is not shown
        var state = new LibraryState
        {
            SearchFilter = "ABBA",
            ScanComplete = true,
            ArtistsLoaded = true,
            Artists = _testArtists
        };
        SetupFluxorWithState(state);
        var cut = RenderComponent<LibrarySearch>();

        // Assert — no alphabet bar in search results view
        var navs = cut.FindAll(".alphabet-bar");
        Assert.Empty(navs);
    }

    [Fact]
    public void AlphabetBar_HashGroupArtists_VisibleInList_HashButtonShown()
    {
        // Arrange — artist with non-alpha first char belongs to '#' group
        var artistsWithHash = new List<ArtistItem>
        {
            new ArtistItem("4 Non Blondes", 2),
            new ArtistItem("ABBA", 3),
        };
        var state = new LibraryState
        {
            SearchFilter = string.Empty,
            ScanComplete = true,
            ArtistsLoaded = true,
            Artists = artistsWithHash
        };
        SetupFluxorWithState(state);
        var cut = RenderComponent<LibrarySearch>();

        // Assert — "4 Non Blondes" row IS rendered
        var rows = cut.FindAll(".artist-row");
        Assert.Contains(rows, r => r.TextContent.Contains("4 Non Blondes"));

        // Assert — alphabet bar has 27 buttons (# + A–Z); '#' button is present and enabled
        var buttons = cut.FindAll(".alpha-btn");
        Assert.Equal(27, buttons.Count);
        var hashButton = buttons.FirstOrDefault(b => b.TextContent.Trim() == "#");
        Assert.NotNull(hashButton);
        Assert.False(hashButton.HasAttribute("disabled"), "'#' button should be enabled when hash-group artists exist");
    }

    // ── US1: Scroll-following letter highlight (T017) ─────────────────────

    [Fact]
    public async Task OnLetterVisible_SetsAlphaBtnCurrentOnMatchingButton()
    {
        // Arrange — include an artist starting with 'S' so the 'S' button is active
        var artistsWithS = new List<ArtistItem>(_testArtists)
        {
            new ArtistItem("Sia", 4)
        };
        var state = new LibraryState
        {
            SearchFilter = string.Empty,
            ScanComplete = true,
            ArtistsLoaded = true,
            Artists = artistsWithS
        };
        SetupFluxorWithState(state);
        var cut = RenderComponent<LibrarySearch>();

        // Pre-condition — no button has the .alpha-btn--current class initially
        Assert.Empty(cut.FindAll(".alpha-btn--current"));

        // Act — simulate the IntersectionObserver callback notifying that 'S' is visible
        await cut.InvokeAsync(() => cut.Instance.OnLetterVisible("S"));

        // Assert — 'S' button now has the current class
        var currentButtons = cut.FindAll(".alpha-btn--current");
        Assert.Single(currentButtons);
        Assert.Equal("S", currentButtons[0].TextContent.Trim());

        // Assert — no other letter button has the current class
        var allButtons = cut.FindAll(".alpha-btn");
        var nonCurrent = allButtons.Where(b => !b.ClassList.Contains("alpha-btn--current")).ToList();
        Assert.Equal(26, nonCurrent.Count);
    }

    // ── Helper ────────────────────────────────────────────────────────────

    private Mock<IDispatcher> SetupFluxorWithState(LibraryState state)
    {
        JSInterop.Mode = JSRuntimeMode.Loose;

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
