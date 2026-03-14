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
    public void ClickingArtistRow_DispatchesSelectArtistAction_WithArtistName()
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

        // Assert – SelectArtistAction dispatched with the correct artist name
        mockDispatcher.Verify(
            d => d.Dispatch(It.Is<SelectArtistAction>(a => a.ArtistName == "ABBA")),
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
    public void ClickingArtistRow_SelectArtistAction_ArtistName_MatchesClickedArtist()
    {
        // Arrange – verify that the dispatched SelectArtistAction carries the
        // tapped artist name (i.e. SearchFilter state will reflect the selection)
        var state = new LibraryState
        {
            SearchFilter = string.Empty,
            ScanComplete = true,
            ArtistsLoaded = true,
            Artists = _testArtists
        };
        var mockDispatcher = SetupFluxorWithState(state);

        SelectArtistAction? captured = null;
        mockDispatcher
            .Setup(d => d.Dispatch(It.IsAny<SelectArtistAction>()))
            .Callback<object>(a => captured = (SelectArtistAction)a);

        var cut = RenderComponent<LibrarySearch>();

        // Act – click third artist row ("The Beatles")
        cut.FindAll(".artist-row")[2].Click();

        // Assert – the action carries the precise artist name
        Assert.NotNull(captured);
        Assert.Equal("The Beatles", captured!.ArtistName);
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

    // ── US1 (T008): Spinner visible on artist tap ─────────────────────────

    [Fact]
    public void Spinner_Shown_WhenIsLoadingArtistSongsTrue()
    {
        // Arrange – state after SelectArtistAction reducer fires: filter set, loading = true
        var state = new LibraryState
        {
            SearchFilter = "ABBA",
            ScanComplete = true,
            ArtistsLoaded = true,
            Artists = _testArtists,
            IsLoadingArtistSongs = true
        };
        SetupFluxorWithState(state);

        // Act
        var cut = RenderComponent<LibrarySearch>();

        // Assert – artist-songs loader spinner is visible
        var loader = cut.Find(".artist-songs-loader");
        Assert.NotNull(loader);
        Assert.NotNull(loader.QuerySelector(".spinner-border"));
    }

    [Fact]
    public void Spinner_NotShown_WhenIsLoadingArtistSongsFalse_AndSongsPresent()
    {
        // Arrange – state after LoadPageSuccessAction: IsLoadingArtistSongs = false
        var state = new LibraryState
        {
            SearchFilter = "ABBA",
            ScanComplete = true,
            ArtistsLoaded = true,
            Songs = new List<Song> { new Song { Artist = "ABBA", Title = "Dancing Queen" } },
            TotalCount = 1,
            IsLoadingArtistSongs = false,
            ArtistSongsError = null
        };
        SetupFluxorWithState(state);

        // Act
        var cut = RenderComponent<LibrarySearch>();

        // Assert – no artist-songs loader
        var loaders = cut.FindAll(".artist-songs-loader");
        Assert.Empty(loaders);
    }

    [Fact]
    public void NoEmptyStateMessage_WhenIsLoadingArtistSongsTrue()
    {
        // Arrange – loading in flight: SearchFilter set, no songs yet, loading = true
        var state = new LibraryState
        {
            SearchFilter = "ABBA",
            ScanComplete = true,
            ArtistsLoaded = true,
            Songs = Array.Empty<Song>(),
            TotalCount = 10,
            IsLoadingArtistSongs = true
        };
        SetupFluxorWithState(state);

        // Act
        var cut = RenderComponent<LibrarySearch>();

        // Assert – no "No songs" alert-info messages while loading
        var infoAlerts = cut.FindAll(".alert-info");
        Assert.Empty(infoAlerts);
    }

    // ── US1 (T009): Error card and retry button ───────────────────────────

    [Fact]
    public void ErrorCard_Shown_WhenArtistSongsErrorIsSet()
    {
        // Arrange – state after LoadPageFailureAction: error set
        var state = new LibraryState
        {
            SearchFilter = "ABBA",
            ScanComplete = true,
            ArtistsLoaded = true,
            IsLoadingArtistSongs = false,
            ArtistSongsError = "Could not load songs. Tap to retry."
        };
        SetupFluxorWithState(state);

        // Act
        var cut = RenderComponent<LibrarySearch>();

        // Assert – error card is rendered with the error message
        var errorCard = cut.Find(".artist-songs-error");
        Assert.NotNull(errorCard);
        Assert.Contains("Could not load songs.", errorCard.TextContent);
        Assert.NotNull(errorCard.QuerySelector(".retry-btn"));
    }

    [Fact]
    public void RetryButton_Click_DispatchesSelectArtistAction_WithLastArtistName()
    {
        // Arrange – state shows the error card; SearchFilter carries the artist name
        // (which initializes _lastSelectedArtist on component init)
        var state = new LibraryState
        {
            SearchFilter = "ABBA",
            ScanComplete = true,
            ArtistsLoaded = true,
            IsLoadingArtistSongs = false,
            ArtistSongsError = "Could not load songs. Tap to retry."
        };
        var mockDispatcher = SetupFluxorWithState(state);

        var cut = RenderComponent<LibrarySearch>();

        // Act – click the retry button
        cut.Find(".retry-btn").Click();

        // Assert – SelectArtistAction dispatched with the last selected artist name
        mockDispatcher.Verify(
            d => d.Dispatch(It.Is<SelectArtistAction>(a => a.ArtistName == "ABBA")),
            Times.Once
        );
    }

    [Fact]
    public void NoSpinner_WhenArtistSongsErrorIsSet()
    {
        // Arrange – error state: loading must have stopped, only error card visible
        var state = new LibraryState
        {
            SearchFilter = "ABBA",
            ScanComplete = true,
            ArtistsLoaded = true,
            IsLoadingArtistSongs = false,
            ArtistSongsError = "Could not load songs. Tap to retry."
        };
        SetupFluxorWithState(state);

        // Act
        var cut = RenderComponent<LibrarySearch>();

        // Assert – no spinner (artist-songs-loader) is shown alongside the error card
        var loaders = cut.FindAll(".artist-songs-loader");
        Assert.Empty(loaders);

        // Error card is shown instead
        Assert.NotNull(cut.Find(".artist-songs-error"));
    }

    // ── US2 (T013): Scroll position captured and restored ─────────────────

    [Fact]
    public void SelectArtist_CapturesScrollY_ViaJsInterop()
    {
        // Arrange — set up the JS module so getScrollY returns a known value
        var moduleSetup = JSInterop.SetupModule("./js/alphabetBridge.js");
        moduleSetup.SetupVoid("observeArtistSections", _ => true);
        moduleSetup.SetupVoid("disconnectArtistSectionObserver", _ => true);
        moduleSetup.Setup<double>("getScrollY").SetResult(550.0);

        var state = new LibraryState
        {
            SearchFilter = string.Empty,
            ScanComplete = true,
            ArtistsLoaded = true,
            Artists = _testArtists
        };
        SetupFluxorWithState(state);
        var cut = RenderComponent<LibrarySearch>();

        // Trigger a render cycle so _alphabetModule is initialised
        cut.WaitForState(() => true);

        // Act — click artist row; SelectArtist will invoke getScrollY
        cut.Find(".artist-row").Click();

        // Assert — getScrollY was invoked (captured the scroll position)
        moduleSetup.VerifyInvoke("getScrollY");
    }

    [Fact]
    public void ClearFilter_TriggersScrollRestore_ViaJsInterop()
    {
        // Arrange — active filter state with the X button visible; module available
        var moduleSetup = JSInterop.SetupModule("./js/alphabetBridge.js");
        moduleSetup.SetupVoid("observeArtistSections", _ => true);
        moduleSetup.SetupVoid("disconnectArtistSectionObserver", _ => true);
        moduleSetup.SetupVoid("scrollToY", _ => true);
        moduleSetup.Setup<double>("getScrollY").SetResult(0.0);

        var state = new LibraryState
        {
            SearchFilter = "ABBA",
            ScanComplete = true,
            ArtistsLoaded = true,
            Artists = _testArtists
        };
        SetupFluxorWithState(state);
        var cut = RenderComponent<LibrarySearch>();

        // Act — click the clear-filter button
        cut.Find(".clear-filter-btn").Click();

        // The component sets _needsScrollRestore = true; on the next render cycle
        // OnAfterRenderAsync fires scrollToY.
        cut.WaitForState(() => true);

        // Assert — scrollToY was invoked to restore the scroll position
        moduleSetup.VerifyInvoke("scrollToY");
    }

    [Fact]
    public void FreshComponentMount_DoesNotInvokeScrollToY()
    {
        // Arrange — no ClearFilter called; just a fresh render
        var moduleSetup = JSInterop.SetupModule("./js/alphabetBridge.js");
        moduleSetup.SetupVoid("observeArtistSections", _ => true);
        moduleSetup.SetupVoid("disconnectArtistSectionObserver", _ => true);
        moduleSetup.SetupVoid("scrollToY", _ => true);

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
        cut.WaitForState(() => true);

        // Assert — scrollToY should NOT have been called on initial render
        Assert.DoesNotContain("scrollToY", moduleSetup.Invocations.Identifiers);
    }

    // ── US3 (T015): Accurate empty-state messages ─────────────────────────

    [Fact]
    public void EmptyState_ShowsNoSongsInLibrary_WhenTotalCountZero_AndNoFilter()
    {
        // Arrange – library genuinely empty: TotalCount = 0, no active filter
        // ScanComplete = false keeps us out of the artist-browse branch
        var state = new LibraryState
        {
            SearchFilter = string.Empty,
            Songs = Array.Empty<Song>(),
            TotalCount = 0,
            IsLoading = false,
            ScanComplete = false
        };
        SetupFluxorWithState(state);

        // Act
        var cut = RenderComponent<LibrarySearch>();

        // Assert – "No songs in library." is shown
        var infoAlert = cut.Find(".alert-info");
        Assert.Contains("No songs in library.", infoAlert.TextContent);
    }

    [Fact]
    public void EmptyState_ShowsNoSongsMatch_WhenSearchYieldsNoResults()
    {
        // Arrange – server returned zero songs for the search term;
        // TotalCount > 0 proves the library is not empty (old code would show "No songs in library." — wrong)
        var state = new LibraryState
        {
            SearchFilter = "xyznonexistent",
            Songs = Array.Empty<Song>(),
            TotalCount = 50,
            IsLoading = false,
            ScanComplete = true,
            IsLoadingArtistSongs = false,
            ArtistSongsError = null
        };
        SetupFluxorWithState(state);

        // Act
        var cut = RenderComponent<LibrarySearch>();

        // Assert – "No songs match" is shown, NOT "No songs in library."
        var infoAlert = cut.Find(".alert-info");
        Assert.Contains("No songs match your search criteria.", infoAlert.TextContent);
        Assert.DoesNotContain("No songs in library.", infoAlert.TextContent);
    }

    [Fact]
    public void EmptyState_ShowsNoSongsMatch_WhenArtistFilterActive_AndNoSongsReturned()
    {
        // Arrange – artist drill-in returned no songs (rare but possible);
        // TotalCount reflects the whole library, not just this artist
        var state = new LibraryState
        {
            SearchFilter = "ABBA",
            Songs = Array.Empty<Song>(),
            TotalCount = 50,
            IsLoading = false,
            ScanComplete = true,
            IsLoadingArtistSongs = false,
            ArtistSongsError = null
        };
        SetupFluxorWithState(state);

        // Act
        var cut = RenderComponent<LibrarySearch>();

        // Assert – "No songs match" is shown; NOT "No songs in library."
        var infoAlert = cut.Find(".alert-info");
        Assert.Contains("No songs match your search criteria.", infoAlert.TextContent);
        Assert.DoesNotContain("No songs in library.", infoAlert.TextContent);
    }

    [Fact]
    public void EmptyState_NotShown_WhenTextSearchInFlight()
    {
        // Arrange – user typed a new search term; FilterSongsAction cleared local filter results
        // (old songs still in Songs from previous load), but server fetch IsLoading = true.
        // FilteredSongs = [] because old songs don't match the new term.
        var state = new LibraryState
        {
            SearchFilter = "xyznothing",
            Songs = new List<Song>
            {
                new Song { Artist = "ABBA", Title = "Dancing Queen" }
            },
            TotalCount = 50,
            IsLoading = true,
            ScanComplete = true,
            IsLoadingArtistSongs = false,
            ArtistSongsError = null
        };
        SetupFluxorWithState(state);

        // Act
        var cut = RenderComponent<LibrarySearch>();

        // Assert – no empty-state message while server fetch is in progress
        var infoAlerts = cut.FindAll(".alert-info");
        Assert.Empty(infoAlerts);
    }

    [Fact]
    public void EmptyState_NotShown_WhenSongsMatchActiveSearchFilter()
    {
        // Arrange – search returned matching songs; FilteredAndSortedSongs is non-empty
        var songs = new List<Song>
        {
            new Song { Artist = "Queen", Title = "Bohemian Rhapsody" },
            new Song { Artist = "Queen", Title = "We Are the Champions" }
        };
        var state = new LibraryState
        {
            SearchFilter = "queen",
            Songs = songs,
            TotalCount = 2,
            IsLoading = false,
            ScanComplete = true,
            IsLoadingArtistSongs = false,
            ArtistSongsError = null
        };
        SetupFluxorWithState(state);

        // Act
        var cut = RenderComponent<LibrarySearch>();

        // Assert – no empty-state alert; songs table is rendered instead
        var infoAlerts = cut.FindAll(".alert-info");
        Assert.Empty(infoAlerts);

        var songsTable = cut.Find("table.table-striped");
        Assert.NotNull(songsTable);
    }

    // ── US4 (T017): _currentLetter set immediately on ScrollToLetter ─────

    [Fact]
    public void ScrollToLetter_SingleTap_SetsCurrentLetterImmediately()
    {
        // Arrange — artists with A and R groups to enable both letter buttons
        var moduleSetup = JSInterop.SetupModule("./js/alphabetBridge.js");
        moduleSetup.SetupVoid("observeArtistSections", _ => true);
        moduleSetup.SetupVoid("disconnectArtistSectionObserver", _ => true);
        moduleSetup.SetupVoid("scrollToArtistSection", _ => true);

        var state = new LibraryState
        {
            SearchFilter = string.Empty,
            ScanComplete = true,
            ArtistsLoaded = true,
            Artists = new List<ArtistItem>
            {
                new ArtistItem("ABBA", 3),
                new ArtistItem("Radiohead", 6)
            }
        };
        SetupFluxorWithState(state);
        var cut = RenderComponent<LibrarySearch>();

        // Pre-condition — no letter highlighted initially
        Assert.Empty(cut.FindAll(".alpha-btn--current"));

        // Act — tap the 'A' button
        cut.Find("button.alpha-btn[title='Jump to A']").Click();

        // Assert — 'A' button immediately has the current class
        var currentButtons = cut.FindAll(".alpha-btn--current");
        Assert.Single(currentButtons);
        Assert.Equal("A", currentButtons[0].TextContent.Trim());
    }

    [Fact]
    public void ScrollToLetter_RepeatedSameLetter_RemainsHighlighted()
    {
        // Arrange — single artist group 'A' to enable the 'A' button
        var moduleSetup = JSInterop.SetupModule("./js/alphabetBridge.js");
        moduleSetup.SetupVoid("observeArtistSections", _ => true);
        moduleSetup.SetupVoid("disconnectArtistSectionObserver", _ => true);
        moduleSetup.SetupVoid("scrollToArtistSection", _ => true);

        var state = new LibraryState
        {
            SearchFilter = string.Empty,
            ScanComplete = true,
            ArtistsLoaded = true,
            Artists = new List<ArtistItem>
            {
                new ArtistItem("ABBA", 3)
            }
        };
        SetupFluxorWithState(state);
        var cut = RenderComponent<LibrarySearch>();

        // Act — tap 'A' twice
        var aButton = cut.Find("button.alpha-btn[title='Jump to A']");
        aButton.Click();
        aButton.Click();

        // Assert — 'A' still has the current class after the repeated tap
        var currentButtons = cut.FindAll(".alpha-btn--current");
        Assert.Single(currentButtons);
        Assert.Equal("A", currentButtons[0].TextContent.Trim());
    }

    [Fact]
    public void ScrollToLetter_JumpFromRToA_AHighlighted_RUnhighlighted()
    {
        // Arrange — artists in both A and R groups to enable both buttons
        var moduleSetup = JSInterop.SetupModule("./js/alphabetBridge.js");
        moduleSetup.SetupVoid("observeArtistSections", _ => true);
        moduleSetup.SetupVoid("disconnectArtistSectionObserver", _ => true);
        moduleSetup.SetupVoid("scrollToArtistSection", _ => true);

        var state = new LibraryState
        {
            SearchFilter = string.Empty,
            ScanComplete = true,
            ArtistsLoaded = true,
            Artists = new List<ArtistItem>
            {
                new ArtistItem("ABBA", 3),
                new ArtistItem("Radiohead", 6)
            }
        };
        SetupFluxorWithState(state);
        var cut = RenderComponent<LibrarySearch>();

        // Act — tap 'R' first, then 'A'
        cut.Find("button.alpha-btn[title='Jump to R']").Click();
        cut.Find("button.alpha-btn[title='Jump to A']").Click();

        // Assert — only 'A' has the current class; 'R' is un-highlighted
        var currentButtons = cut.FindAll(".alpha-btn--current");
        Assert.Single(currentButtons);
        Assert.Equal("A", currentButtons[0].TextContent.Trim());

        var rButton = cut.Find("button.alpha-btn[title='Jump to R']");
        Assert.False(rButton.ClassList.Contains("alpha-btn--current"), "'R' should no longer be highlighted after jumping to 'A'");
    }

    // ── US5 (T019): Alphabet bar rendered in artist browse view ──────────

    [Fact]
    public void AlphabetBar_RenderedInArtistBrowseView()
    {
        // Arrange — scan complete, artists loaded, no active search filter
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

        // Assert — alphabet bar is rendered (CSS layout is validated manually via quickstart.md)
        var bars = cut.FindAll(".alphabet-bar");
        Assert.Single(bars);
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
