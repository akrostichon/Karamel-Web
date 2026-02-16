using Bunit;
using Karamel.Web.Models;
using Karamel.Web.Pages;
using Karamel.Web.Store.Session;
using Karamel.Web.Store.Playlist;
using Karamel.Web.Store.Library;
using Karamel.Web.Services;
using Karamel.Web.Tests.TestHelpers;
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
    /// <param name="isMainTab">Whether this is the main tab (default: true)</param>
    /// <returns>Tuple of (IActionSubscriber mock, IDispatcher mock, FakeNavigationManager)</returns>
    protected (Mock<IActionSubscriber>, Mock<IDispatcher>, FakeNavigationManager) SetupTestWithSession(
        SessionState sessionState,
        PlaylistState playlistState,
        LibraryState? libraryState = null,
        string view = "nextsong",
        bool isMainTab = true)
    {
        var sessionId = sessionState.CurrentSession?.SessionId ?? Guid.Empty;
        var currentUri = $"http://localhost/{view}?session={sessionId}";
        
        return SetupFluxorWithStates(sessionState, playlistState, libraryState, currentUri, isMainTab);
    }

    /// <summary>
    /// Sets up a test context with a non-localhost URL (for testing QR code behavior).
    /// </summary>
    /// <param name="sessionState">The session state to use</param>
    /// <param name="playlistState">The playlist state to use</param>
    /// <param name="libraryState">The library state to use (optional, for SingerView)</param>
    /// <param name="view">The view name (e.g., "nextsong", "player", "playlist", "singer")</param>
    /// <param name="isMainTab">Whether this is the main tab (default: true)</param>
    /// <returns>Tuple of (IActionSubscriber mock, IDispatcher mock, FakeNavigationManager)</returns>
    protected (Mock<IActionSubscriber>, Mock<IDispatcher>, FakeNavigationManager) SetupTestWithNonLocalhostSession(
        SessionState sessionState,
        PlaylistState playlistState,
        LibraryState? libraryState = null,
        string view = "nextsong",
        bool isMainTab = true)
    {
        var sessionId = sessionState.CurrentSession?.SessionId ?? Guid.Empty;
        var currentUri = $"https://karaoke.example.com/{view}?session={sessionId}";
        
        return SetupFluxorWithStates(sessionState, playlistState, libraryState, currentUri, isMainTab);
    }

    /// <summary>
    /// Sets up Fluxor state mocks and services with a custom URI.
    /// Use this for testing invalid session scenarios or custom URLs.
    /// </summary>
    /// <param name="sessionState">The session state to use</param>
    /// <param name="playlistState">The playlist state to use</param>
    /// <param name="isMainTab">Whether this is the main tab (default: true)</param>
    /// <returns>Tuple of (IActionSubscriber mock, IDispatcher mock, FakeNavigationManager)</returns>
    protected (Mock<IActionSubscriber>, Mock<IDispatcher>, FakeNavigationManager) SetupFluxorWithStates(
        SessionState sessionState,
        PlaylistState playlistState,
        LibraryState? libraryState = null,
        string currentUri = "http://localhost/",
        bool isMainTab = true)
    {
        // Mock IState<SessionState>
        var mockSessionState = new Mock<IState<SessionState>>();
        mockSessionState.Setup(s => s.Value).Returns(sessionState);

        // Mock IState<PlaylistState>
        var mockPlaylistState = new Mock<IState<PlaylistState>>();
        mockPlaylistState.Setup(s => s.Value).Returns(playlistState);

        // Mock IDispatcher
        var mockDispatcher = new Mock<IDispatcher>();

        // Mock IActionSubscriber
        var mockActionSubscriber = new Mock<IActionSubscriber>();

        // Mock NavigationManager with custom URI
        var fakeNavManager = new FakeNavigationManager(currentUri);

        // Mock JSRuntime
        var mockJSRuntime = new Mock<IJSRuntime>();
        var mockJSModule = new Mock<IJSObjectReference>();
        mockJSRuntime.Setup(js => js.InvokeAsync<IJSObjectReference>(
            It.IsAny<string>(),
            It.IsAny<object[]>()))
            .ReturnsAsync(mockJSModule.Object);

        // Create mock LibraryState
        var mockLibraryState = new Mock<IState<LibraryState>>();
        mockLibraryState.Setup(s => s.Value).Returns(libraryState ?? new LibraryState());

        // Register all services BEFORE creating any components
        Services.AddSingleton(mockSessionState.Object);
        Services.AddSingleton(mockPlaylistState.Object);
        Services.AddSingleton(mockLibraryState.Object);
        Services.AddSingleton(mockDispatcher.Object);
        Services.AddSingleton(mockActionSubscriber.Object);
        Services.AddSingleton<NavigationManager>(fakeNavManager);
        Services.AddSingleton(mockJSRuntime.Object);
        
        // Register service mocks for components
        var mockConnectionManager = new Mock<ISignalRConnectionManager>();
        mockConnectionManager.Setup(m => m.IsMainTab).Returns(isMainTab);
        mockConnectionManager.Setup(m => m.InitializeAsync(It.IsAny<Guid>(), It.IsAny<bool>(), It.IsAny<string?>()))
            .Returns(Task.CompletedTask);
        Services.AddSingleton(mockConnectionManager.Object);
        
        var mockSessionApiClient = new Mock<ISessionApiClient>();
        Services.AddSingleton(mockSessionApiClient.Object);
        
        var mockSignalRBridge = new Mock<ISignalRPlaylistBridge>();
        Services.AddSingleton(mockSignalRBridge.Object);

        var mockStateSynchronizer = new Mock<IPlaylistStateSynchronizer>();
        Services.AddSingleton(mockStateSynchronizer.Object);

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
    /// Helper method to render SingerView with proper session parameter and wait for initialization.
    /// Must be called AFTER SetupTestWithSession has been called to register services.
    /// </summary>
    /// <param name="sessionId">The session ID to pass as component parameter</param>
    /// <param name="tokenParam">Optional token parameter for authentication</param>
    /// <returns>The rendered component</returns>
    protected IRenderedComponent<SingerView> RenderSingerViewComponent(
        Guid sessionId,
        string? tokenParam = null)
    {
        // For SupplyParameterFromQuery parameters, we must use NavigationManager
        var navManager = Services.GetRequiredService<NavigationManager>();
        var uri = navManager.GetUriWithQueryParameter("session", sessionId.ToString());
        if (tokenParam != null)
        {
            var uriBuilder = new UriBuilder(uri);
            uriBuilder.Query += $"&token={Uri.EscapeDataString(tokenParam)}";
            uri = uriBuilder.Uri.ToString();
        }
        navManager.NavigateTo(uri);
        
        var cut = RenderComponent<SingerView>();
        
        // Wait for initialization to complete (component should no longer show spinner)
        cut.WaitForState(() => 
        {
            var markup = cut.Markup;
            return !markup.Contains("Loading session...") || 
                   markup.Contains("input#singerNameInput") || 
                   markup.Contains("library-container") ||
                   markup.Contains("Session Loading Failed");
        }, timeout: TimeSpan.FromSeconds(5));
        
        return cut;
    }

    /// <summary>
    /// Fake NavigationManager for testing that supports custom URIs.
    /// </summary>
    protected class FakeNavigationManager : NavigationManager
    {
        public List<string> NavigationHistory { get; } = new List<string>();
        
        public FakeNavigationManager(string uri = "http://localhost/")
        {
            var baseUri = new Uri(uri);
            var baseUrl = $"{baseUri.Scheme}://{baseUri.Host}{(baseUri.IsDefaultPort ? "" : $":{baseUri.Port}")}/";
            Initialize(baseUrl, uri);
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
