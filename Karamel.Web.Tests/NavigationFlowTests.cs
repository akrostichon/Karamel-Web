using Bunit;
using Karamel.Web.Pages;
using Karamel.Web.Models;
using Karamel.Web.Store.Session;
using Karamel.Web.Store.Playlist;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.JSInterop;
using Microsoft.AspNetCore.Components;
using Fluxor;
using Moq;

namespace Karamel.Web.Tests;

/// <summary>
/// Unit tests for navigation flow and layout structure in Step 2.11.
/// Tests that MainLayout no longer includes NavMenu and has minimal structure.
/// Tests that views validate session and handle routing correctly.
/// </summary>
public class NavigationFlowTests : TestContext
{
    [Fact]
    public void MainLayout_DoesNotIncludeNavMenuComponent()
    {
        // Arrange - Render MainLayout through App component or directly inspect markup
        var layoutMarkup = System.IO.File.ReadAllText("d:\\Projects\\Karamel-Web\\Karamel.Web\\Layout\\MainLayout.razor");

        // Assert - Verify NavMenu component is not referenced
        Assert.DoesNotContain("<NavMenu", layoutMarkup);
        Assert.DoesNotContain("NavMenu />", layoutMarkup);
    }

    [Fact]
    public void MainLayout_DoesNotContainSidebarDiv()
    {
        // Arrange - Read MainLayout markup
        var layoutMarkup = System.IO.File.ReadAllText("d:\\Projects\\Karamel-Web\\Karamel.Web\\Layout\\MainLayout.razor");

        // Assert - Verify no sidebar div exists
        Assert.DoesNotContain("class=\"sidebar\"", layoutMarkup);
        Assert.DoesNotContain("<div class=\"sidebar\">", layoutMarkup);
    }

    [Fact]
    public void MainLayout_ContainsMainContentArea()
    {
        // Arrange - Read MainLayout markup
        var layoutMarkup = System.IO.File.ReadAllText("d:\\Projects\\Karamel-Web\\Karamel.Web\\Layout\\MainLayout.razor");

        // Assert - Verify the body content area is rendered with @Body
        Assert.Contains("@Body", layoutMarkup);
        Assert.Contains("<main>", layoutMarkup);
    }

    [Fact]
    public void MainLayout_HasMinimalStructure()
    {
        // Arrange - Read MainLayout markup
        var layoutMarkup = System.IO.File.ReadAllText("d:\\Projects\\Karamel-Web\\Karamel.Web\\Layout\\MainLayout.razor");

        // Assert - Verify there's no "page" wrapper div with sidebar
        Assert.DoesNotContain("class=\"page\"", layoutMarkup);
        Assert.DoesNotContain("sidebar", layoutMarkup);
    }

    [Fact]
    public void MainLayout_RendersBodyContent()
    {
        // Arrange - Read MainLayout markup
        var layoutMarkup = System.IO.File.ReadAllText("d:\\Projects\\Karamel-Web\\Karamel.Web\\Layout\\MainLayout.razor");

        // Assert - Verify body content placeholder is present
        Assert.Contains("@Body", layoutMarkup);
        Assert.Contains("<article class=\"content\">", layoutMarkup);
    }

    #region Session Validation Tests

    [Fact]
    public void NextSongView_WithMissingSessionParameter_ShowsInvalidSessionMessage()
    {
        // Arrange - URL without session parameter, no session in state
        var sessionState = new SessionState { CurrentSession = null };
        var playlistState = new PlaylistState();
        var navManager = SetupFluxorWithStates(sessionState, playlistState, "http://localhost/nextsong");

        // Act
        var cut = RenderComponent<NextSongView>();

        // Assert
        var alert = cut.Find(".alert-danger");
        Assert.Contains("Invalid Session", alert.TextContent);
    }

    [Fact]
    public void NextSongView_WithInvalidSessionGuid_ShowsInvalidSessionMessage()
    {
        // Arrange - URL with invalid GUID format
        var sessionState = new SessionState { CurrentSession = null };
        var playlistState = new PlaylistState();
        var navManager = SetupFluxorWithStates(sessionState, playlistState, "http://localhost/nextsong?session=invalid-guid");

        // Act
        var cut = RenderComponent<NextSongView>();

        // Assert
        var alert = cut.Find(".alert-danger");
        Assert.Contains("Invalid Session", alert.TextContent);
    }

    [Fact]
    public void PlayerView_WithMissingSessionParameter_ShowsInvalidSessionMessage()
    {
        // Arrange - URL without session parameter, no session in state
        var sessionState = new SessionState { CurrentSession = null };
        var playlistState = new PlaylistState();
        var navManager = SetupFluxorWithStates(sessionState, playlistState, "http://localhost/player");

        // Act
        var cut = RenderComponent<PlayerView>();

        // Assert
        var alert = cut.Find(".alert-danger");
        Assert.Contains("Invalid Session", alert.TextContent);
    }

    [Fact]
    public void SessionParameter_WhenMismatchesCurrentSession_ShowsInvalidSessionMessage()
    {
        // Arrange - Session in state doesn't match URL parameter
        var sessionIdInState = Guid.NewGuid();
        var sessionIdInUrl = Guid.NewGuid();

        var session = new Session
        {
            SessionId = sessionIdInState,
            LibraryPath = "C:\\Karaoke",
            RequireSingerName = true
        };

        var sessionState = new SessionState { CurrentSession = session };
        var playlistState = new PlaylistState();
        var navManager = SetupFluxorWithStates(sessionState, playlistState, $"http://localhost/nextsong?session={sessionIdInUrl}");

        // Act
        var cut = RenderComponent<NextSongView>();

        // Assert - Should show invalid session because IDs don't match
        var alert = cut.Find(".alert-danger");
        Assert.Contains("Invalid Session", alert.TextContent);
    }

    [Fact]
    public void Home_DoesNotRequireSessionParameter()
    {
        // Arrange - Home page doesn't need session
        var sessionState = new SessionState { CurrentSession = null };
        var playlistState = new PlaylistState();
        var navManager = SetupFluxorWithStates(sessionState, playlistState, "http://localhost/");

        // Act
        var cut = RenderComponent<Home>();

        // Assert - Should render without errors
        var heading = cut.Find("h1");
        Assert.NotNull(heading);
    }

    #endregion

    #region Multiple Sessions Tests

    [Fact]
    public void DifferentSessionIds_WithMismatchShowsError()
    {
        // Arrange - Create session in state with one ID, provide different ID in URL
        var sessionId1 = Guid.NewGuid();
        var sessionId2 = Guid.NewGuid();

        var session1 = new Session
        {
            SessionId = sessionId1,
            LibraryPath = "C:\\Karaoke1",
            RequireSingerName = true
        };

        // Test Context - Session 1 in state, Session 2 in URL
        var sessionState1 = new SessionState { CurrentSession = session1 };
        var playlistState1 = new PlaylistState();
        var navManager1 = SetupFluxorWithStates(sessionState1, playlistState1, $"http://localhost/nextsong?session={sessionId2}");

        var cut1 = RenderComponent<NextSongView>();

        // Assert - Should show error because session IDs don't match
        var alerts1 = cut1.FindAll(".alert-danger");
        Assert.NotEmpty(alerts1);
        Assert.Contains("Invalid Session", alerts1[0].TextContent);
    }

    #endregion

    #region Helper Methods

    private FakeNavigationManager SetupFluxorWithStates(SessionState sessionState, PlaylistState playlistState, string currentUri = "http://localhost/")
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

        return fakeNavManager;
    }

    private class FakeNavigationManager : NavigationManager
    {
        public FakeNavigationManager(string uri = "http://localhost/")
        {
            Initialize("http://localhost/", uri);
        }

        protected override void NavigateToCore(string uri, bool forceLoad)
        {
            // Update the Uri property for navigation
            Uri = uri;
        }
    }

    #endregion
}
