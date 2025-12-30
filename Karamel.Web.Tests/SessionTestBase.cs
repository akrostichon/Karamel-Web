using Bunit;
using Karamel.Web.Models;
using Karamel.Web.Store.Session;
using Karamel.Web.Store.Playlist;
using Karamel.Web.Store.Library;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.JSInterop;
using Microsoft.AspNetCore.Components;
using Fluxor;
using Moq;

namespace Karamel.Web.Tests;

/// <summary>
/// Base class for component tests that require session validation.
/// Provides centralized setup for Fluxor state, NavigationManager with session parameters,
/// and common test utilities.
/// </summary>
public abstract class SessionTestBase : TestContext
{
    /// <summary>
    /// Sets up a test context with a valid session and proper URL with session parameter.
    /// Automatically constructs the URL based on the session ID in state.
    /// </summary>
    /// <param name="sessionState">The session state to use</param>
    /// <param name="playlistState">The playlist state to use</param>
    /// <param name="libraryState">The library state to use (optional, for SingerView)</param>
    /// <param name="view">The view name (e.g., "nextsong", "player", "playlist", "singer")</param>
    /// <returns>Tuple of (IActionSubscriber mock, IDispatcher mock, FakeNavigationManager)</returns>
    protected (Mock<IActionSubscriber>, Mock<IDispatcher>, FakeNavigationManager) SetupTestWithSession(
        SessionState sessionState,
        PlaylistState playlistState,
        LibraryState? libraryState = null,
        string view = "nextsong")
    {
        var sessionId = sessionState.CurrentSession?.SessionId ?? Guid.Empty;
        var currentUri = $"http://localhost/{view}?session={sessionId}";
        
        return SetupFluxorWithStates(sessionState, playlistState, libraryState, currentUri);
    }

    /// <summary>
    /// Sets up Fluxor state mocks and services with a custom URI.
    /// Use this for testing invalid session scenarios or custom URLs.
    /// </summary>
    /// <param name="sessionState">The session state to use</param>
    /// <param name="playlistState">The playlist state to use</param>
    /// <param name="libraryState">The library state to use (optional, for SingerView)</param>
    /// <param name="currentUri">The current URI for NavigationManager</param>
    /// <returns>Tuple of (IActionSubscriber mock, IDispatcher mock, FakeNavigationManager)</returns>
    protected (Mock<IActionSubscriber>, Mock<IDispatcher>, FakeNavigationManager) SetupFluxorWithStates(
        SessionState sessionState,
        PlaylistState playlistState,
        LibraryState? libraryState = null,
        string currentUri = "http://localhost/")
    {
        // Mock IState<SessionState>
        var mockSessionState = new Mock<IState<SessionState>>();
        mockSessionState.Setup(s => s.Value).Returns(sessionState);

        // Mock IState<PlaylistState>
        var mockPlaylistState = new Mock<IState<PlaylistState>>();
        mockPlaylistState.Setup(s => s.Value).Returns(playlistState);

        // Mock IState<LibraryState> if provided
        if (libraryState != null)
        {
            var mockLibraryState = new Mock<IState<LibraryState>>();
            mockLibraryState.Setup(s => s.Value).Returns(libraryState);
            Services.AddSingleton(mockLibraryState.Object);
        }

        // Mock IDispatcher
        var mockDispatcher = new Mock<IDispatcher>();

        // Mock IActionSubscriber
        var mockActionSubscriber = new Mock<IActionSubscriber>();

        // Mock NavigationManager with custom URI
        var fakeNavManager = new FakeNavigationManager(currentUri);

        // Mock JSRuntime
        var mockJSRuntime = new Mock<IJSRuntime>();
        mockJSRuntime.Setup(js => js.InvokeAsync<IJSObjectReference>(
            It.IsAny<string>(),
            It.IsAny<object[]>()))
            .ReturnsAsync((IJSObjectReference)null!);

        // Register services
        Services.AddSingleton(mockSessionState.Object);
        Services.AddSingleton(mockPlaylistState.Object);
        Services.AddSingleton(mockDispatcher.Object);
        Services.AddSingleton(mockActionSubscriber.Object);
        Services.AddSingleton<NavigationManager>(fakeNavManager);
        Services.AddSingleton(mockJSRuntime.Object);

        return (mockActionSubscriber, mockDispatcher, fakeNavManager);
    }

    /// <summary>
    /// Creates a test session with the specified ID.
    /// </summary>
    protected Session CreateTestSession(Guid? sessionId = null, bool requireSingerName = true)
    {
        return new Session
        {
            SessionId = sessionId ?? Guid.NewGuid(),
            LibraryPath = "C:\\TestKaraoke",
            RequireSingerName = requireSingerName,
            PauseBetweenSongs = true,
            PauseBetweenSongsSeconds = 5,
            AllowSingersToReorder = false,
            FilenamePattern = "%artist - %title"
        };
    }

    /// <summary>
    /// Creates a test song with the specified properties.
    /// </summary>
    protected Song CreateTestSong(
        string artist = "Test Artist",
        string title = "Test Song",
        string singerName = "Test Singer")
    {
        return new Song
        {
            Id = Guid.NewGuid(),
            Artist = artist,
            Title = title,
            Mp3FileName = $"{artist.ToLower().Replace(" ", "-")}-{title.ToLower().Replace(" ", "-")}.mp3",
            CdgFileName = $"{artist.ToLower().Replace(" ", "-")}-{title.ToLower().Replace(" ", "-")}.cdg",
            AddedBySinger = singerName
        };
    }

    /// <summary>
    /// Fake NavigationManager for testing that supports custom URIs.
    /// </summary>
    protected class FakeNavigationManager : NavigationManager
    {
        public List<string> NavigationHistory { get; } = new List<string>();
        
        public FakeNavigationManager(string uri = "http://localhost/")
        {
            Initialize("http://localhost/", uri);
            NavigationHistory.Add(uri);
        }

        protected override void NavigateToCore(string uri, bool forceLoad)
        {
            // Track navigation history
            NavigationHistory.Add(uri);
            // Update the Uri property for navigation
            Uri = ToAbsoluteUri(uri).ToString();
        }
    }
}
