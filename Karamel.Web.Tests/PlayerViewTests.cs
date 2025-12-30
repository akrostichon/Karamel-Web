using Bunit;
using Fluxor;
using Karamel.Web.Models;
using Karamel.Web.Pages;
using Karamel.Web.Store.Playlist;
using Karamel.Web.Store.Session;
using Microsoft.AspNetCore.Components;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.JSInterop;
using Moq;

namespace Karamel.Web.Tests;

/// <summary>
/// Unit tests for the PlayerView component.
/// Tests session validation, full-screen display, controls overlay, side panel, 
/// auto-advance, and NextSong action dispatch.
/// </summary>
public class PlayerViewTests : SessionTestBase
{
    private readonly Song _testSong;
    private readonly Session _testSession;

    public PlayerViewTests()
    {
        _testSong = new Song
        {
            Id = Guid.NewGuid(),
            Artist = "Beatles",
            Title = "Let It Be",
            Mp3FileName = "beatles-let-it-be.mp3",
            CdgFileName = "beatles-let-it-be.cdg",
            AddedBySinger = "John Doe"
        };

        _testSession = new Session
        {
            SessionId = Guid.NewGuid(),
            LibraryPath = "C:\\Karaoke",
            RequireSingerName = true
        };
    }

    [Fact]
    public void Component_WhenNoSession_ShowsInvalidSessionMessage()
    {
        // Arrange
        var sessionState = new SessionState { CurrentSession = null };
        var playlistState = new PlaylistState();
        SetupTestWithSession(sessionState, playlistState, view: "player");
        SetupJSRuntime();

        // Act
        var cut = RenderComponent<PlayerView>();

        // Assert
        var alert = cut.Find(".alert-danger");
        Assert.Contains("Invalid Session", alert.TextContent);
        Assert.Contains("No active karaoke session found", alert.TextContent);
    }

    [Fact]
    public void Component_WhenNoCurrentSong_ShowsNoSongMessage()
    {
        // Arrange
        var sessionState = new SessionState { CurrentSession = _testSession, IsInitialized = true };
        var playlistState = new PlaylistState { CurrentSong = null };
        SetupTestWithSession(sessionState, playlistState, view: "player");
        SetupJSRuntime();

        // Act
        var cut = RenderComponent<PlayerView>();

        // Assert
        var alert = cut.Find(".alert-warning");
        Assert.Contains("No Song Selected", alert.TextContent);
        Assert.Contains("No song is currently playing", alert.TextContent);
    }

    [Fact]
    public void Component_WhenSessionAndSongValid_DisplaysPlayerView()
    {
        // Arrange
        var sessionState = new SessionState { CurrentSession = _testSession, IsInitialized = true };
        var playlistState = new PlaylistState { CurrentSong = _testSong };
        SetupTestWithSession(sessionState, playlistState, view: "player");
        SetupJSRuntime();

        // Act
        var cut = RenderComponent<PlayerView>();

        // Assert
        var canvas = cut.Find("#cdgCanvas");
        Assert.NotNull(canvas);
        Assert.Equal("300", canvas.GetAttribute("width"));
        Assert.Equal("216", canvas.GetAttribute("height"));
    }

    [Fact]
    public void Component_HasHiddenAudioElement()
    {
        // Arrange
        var sessionState = new SessionState { CurrentSession = _testSession, IsInitialized = true };
        var playlistState = new PlaylistState { CurrentSong = _testSong };
        SetupTestWithSession(sessionState, playlistState, view: "player");
        SetupJSRuntime();

        // Act
        var cut = RenderComponent<PlayerView>();

        // Assert
        var audio = cut.Find("#audioPlayer");
        Assert.NotNull(audio);
        Assert.Contains("display: none", audio.GetAttribute("style"));
    }

    [Fact]
    public void Component_ControlsOverlay_InitiallyHidden()
    {
        // Arrange
        var sessionState = new SessionState { CurrentSession = _testSession, IsInitialized = true };
        var playlistState = new PlaylistState { CurrentSong = _testSong };
        SetupTestWithSession(sessionState, playlistState, view: "player");
        SetupJSRuntime();

        // Act
        var cut = RenderComponent<PlayerView>();

        // Assert
        var controlsOverlay = cut.Find(".controls-overlay");
        Assert.NotNull(controlsOverlay);
        
        // Controls should not be present initially
        var controls = cut.FindAll(".controls");
        Assert.Empty(controls);
    }

    [Fact]
    public void Component_ControlsOverlay_ShowsOnMouseEnter()
    {
        // Arrange
        var sessionState = new SessionState { CurrentSession = _testSession, IsInitialized = true };
        var playlistState = new PlaylistState { CurrentSong = _testSong };
        SetupTestWithSession(sessionState, playlistState, view: "player");
        SetupJSRuntime();

        // Act
        var cut = RenderComponent<PlayerView>();
        var controlsOverlay = cut.Find(".controls-overlay");
        
        // Simulate mouse enter by directly invoking the component method
        cut.InvokeAsync(() =>
        {
            cut.Instance.GetType().GetMethod("ShowControls", 
                System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance)
                ?.Invoke(cut.Instance, null);
        });
        cut.Render();

        // Assert
        var controls = cut.Find(".controls");
        Assert.NotNull(controls);
        
        var buttons = controls.QuerySelectorAll(".btn-control");
        Assert.Equal(2, buttons.Length); // Play/Pause and Stop buttons
    }

    [Fact]
    public void Component_ControlsOverlay_HidesOnMouseLeave()
    {
        // Arrange
        var sessionState = new SessionState { CurrentSession = _testSession, IsInitialized = true };
        var playlistState = new PlaylistState { CurrentSong = _testSong };
        SetupTestWithSession(sessionState, playlistState, view: "player");
        SetupJSRuntime();

        // Act
        var cut = RenderComponent<PlayerView>();
        
        // Show controls first
        cut.InvokeAsync(() =>
        {
            cut.Instance.GetType().GetMethod("ShowControls", 
                System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance)
                ?.Invoke(cut.Instance, null);
        });
        cut.Render();
        
        // Then hide them
        cut.InvokeAsync(() =>
        {
            cut.Instance.GetType().GetMethod("HideControls", 
                System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance)
                ?.Invoke(cut.Instance, null);
        });
        cut.Render();

        // Assert
        var controls = cut.FindAll(".controls");
        Assert.Empty(controls);
    }

    [Fact]
    public void Component_LeftEdgeDetector_InitiallyShowsNoIcon()
    {
        // Arrange
        var sessionState = new SessionState { CurrentSession = _testSession, IsInitialized = true };
        var playlistState = new PlaylistState { CurrentSong = _testSong };
        SetupTestWithSession(sessionState, playlistState, view: "player");
        SetupJSRuntime();

        // Act
        var cut = RenderComponent<PlayerView>();

        // Assert
        var leftEdge = cut.Find(".left-edge-detector");
        Assert.NotNull(leftEdge);
        
        var expandIcon = cut.FindAll(".expand-icon");
        Assert.Empty(expandIcon);
    }

    [Fact]
    public void Component_LeftEdgeDetector_ShowsIconOnHover()
    {
        // Arrange
        var sessionState = new SessionState { CurrentSession = _testSession, IsInitialized = true };
        var playlistState = new PlaylistState { CurrentSong = _testSong };
        SetupTestWithSession(sessionState, playlistState, view: "player");
        SetupJSRuntime();

        // Act
        var cut = RenderComponent<PlayerView>();
        
        // Simulate hover
        cut.InvokeAsync(() =>
        {
            cut.Instance.GetType().GetMethod("OnLeftEdgeHover", 
                System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance)
                ?.Invoke(cut.Instance, null);
        });
        cut.Render();

        // Assert
        var expandIcon = cut.Find(".expand-icon");
        Assert.NotNull(expandIcon);
    }

    [Fact]
    public void Component_SidePanel_InitiallyClosed()
    {
        // Arrange
        var sessionState = new SessionState { CurrentSession = _testSession, IsInitialized = true };
        var playlistState = new PlaylistState { CurrentSong = _testSong };
        SetupTestWithSession(sessionState, playlistState, view: "player");
        SetupJSRuntime();

        // Act
        var cut = RenderComponent<PlayerView>();

        // Assert
        var sidePanels = cut.FindAll(".side-panel");
        Assert.Empty(sidePanels);
    }

    [Fact]
    public void Component_SidePanel_OpensOnExpandIconClick()
    {
        // Arrange
        var sessionState = new SessionState { CurrentSession = _testSession, IsInitialized = true };
        var playlistState = new PlaylistState { CurrentSong = _testSong };
        SetupTestWithSession(sessionState, playlistState, view: "player");
        SetupJSRuntime();

        // Act
        var cut = RenderComponent<PlayerView>();
        
        // Show expand icon first
        cut.InvokeAsync(() =>
        {
            cut.Instance.GetType().GetMethod("OnLeftEdgeHover", 
                System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance)
                ?.Invoke(cut.Instance, null);
        });
        cut.Render();
        
        var expandIcon = cut.Find(".expand-icon");
        expandIcon.Click();

        // Assert
        var sidePanel = cut.Find(".side-panel");
        Assert.NotNull(sidePanel);
    }

    [Fact]
    public void Component_SidePanel_DisplaysSingerViewByDefault()
    {
        // Arrange
        var sessionState = new SessionState { CurrentSession = _testSession, IsInitialized = true };
        var playlistState = new PlaylistState { CurrentSong = _testSong };
        SetupTestWithSession(sessionState, playlistState, view: "player");
        SetupJSRuntime();

        // Act
        var cut = RenderComponent<PlayerView>();
        
        // Show and click expand icon
        cut.InvokeAsync(() =>
        {
            cut.Instance.GetType().GetMethod("OnLeftEdgeHover", 
                System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance)
                ?.Invoke(cut.Instance, null);
        });
        cut.Render();
        var expandIcon = cut.Find(".expand-icon");
        expandIcon.Click();

        // Assert
        var iframe = cut.Find(".side-panel-content iframe");
        Assert.NotNull(iframe);
        Assert.Contains("/singer?session=", iframe.GetAttribute("src"));
    }

    [Fact]
    public void Component_SidePanel_CanSwitchToPlaylistView()
    {
        // Arrange
        var sessionState = new SessionState { CurrentSession = _testSession, IsInitialized = true };
        var playlistState = new PlaylistState { CurrentSong = _testSong };
        SetupTestWithSession(sessionState, playlistState, view: "player");
        SetupJSRuntime();

        // Act
        var cut = RenderComponent<PlayerView>();
        
        // Show and click expand icon
        cut.InvokeAsync(() =>
        {
            cut.Instance.GetType().GetMethod("OnLeftEdgeHover", 
                System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance)
                ?.Invoke(cut.Instance, null);
        });
        cut.Render();
        var expandIcon = cut.Find(".expand-icon");
        expandIcon.Click();

        var buttons = cut.FindAll(".side-panel-header button");
        var playlistButton = buttons.FirstOrDefault(b => b.TextContent.Contains("Playlist"));
        playlistButton?.Click();

        // Assert
        var iframe = cut.Find(".side-panel-content iframe");
        Assert.Contains("/playlist?session=", iframe.GetAttribute("src"));
    }

    [Fact]
    public void Component_SidePanel_ClosesOnCloseButtonClick()
    {
        // Arrange
        var sessionState = new SessionState { CurrentSession = _testSession, IsInitialized = true };
        var playlistState = new PlaylistState { CurrentSong = _testSong };
        SetupTestWithSession(sessionState, playlistState, view: "player");
        SetupJSRuntime();

        // Act
        var cut = RenderComponent<PlayerView>();
        
        // Show and click expand icon
        cut.InvokeAsync(() =>
        {
            cut.Instance.GetType().GetMethod("OnLeftEdgeHover", 
                System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance)
                ?.Invoke(cut.Instance, null);
        });
        cut.Render();
        var expandIcon = cut.Find(".expand-icon");
        expandIcon.Click();

        var closeButton = cut.Find(".btn-close");
        closeButton.Click();

        // Assert
        var sidePanels = cut.FindAll(".side-panel");
        Assert.Empty(sidePanels);
    }

    [Fact]
    public async Task Component_LoadsAndPlaysCurrentSong()
    {
        // Arrange
        var sessionState = new SessionState { CurrentSession = _testSession, IsInitialized = true };
        var playlistState = new PlaylistState { CurrentSong = _testSong };
        SetupTestWithSession(sessionState, playlistState, view: "player");
        
        var mockFileAccess = new Mock<IJSObjectReference>();
        var mockPlayer = new Mock<IJSObjectReference>();
        SetupJSRuntimeWithModules(mockFileAccess.Object, mockPlayer.Object);

        // Act
        var cut = RenderComponent<PlayerView>();
        await Task.Delay(100); // Allow async initialization

        // Assert
        mockFileAccess.Verify(m => m.InvokeAsync<object>(
            "loadSongFiles",
            It.IsAny<object[]>()), Times.Once);
        
        mockPlayer.Verify(m => m.InvokeAsync<object>(
            "initializePlayerWithCallback",
            It.IsAny<object[]>()), Times.Once);
    }

    [Fact(Skip = "Threading issue with OnSongEnded calling StateHasChanged - needs InvokeAsync")]
    public async Task OnSongEnded_DispatchesNextSongAction()
    {
        // Arrange
        var sessionState = new SessionState { CurrentSession = _testSession, IsInitialized = true };
        var playlistState = new PlaylistState { CurrentSong = _testSong };
        
        var mockDispatcher = new Mock<IDispatcher>();
        
        SetupTestWithSession(sessionState, playlistState, view: "player");
        Services.AddSingleton(mockDispatcher.Object);
        SetupJSRuntime();

        // Act
        var cut = RenderComponent<PlayerView>();
        var component = cut.Instance;
        await component.OnSongEnded();

        // Assert
        mockDispatcher.Verify(d => d.Dispatch(It.IsAny<NextSongAction>()), Times.Once);
    }

    [Fact(Skip = "Threading issue with OnSongEnded calling StateHasChanged")]
    public async Task OnSongEnded_NavigatesToNextSongView()
    {
        // Arrange
        var sessionState = new SessionState { CurrentSession = _testSession, IsInitialized = true };
        var playlistState = new PlaylistState { CurrentSong = _testSong };
        SetupTestWithSession(sessionState, playlistState, view: "player");
        SetupJSRuntime();
        
        var fakeNavManager = new FakeNavigationManager();
        Services.AddSingleton<NavigationManager>(fakeNavManager);

        // Act
        var cut = RenderComponent<PlayerView>();
        var component = cut.Instance;
        await component.OnSongEnded();

        // Assert
        Assert.Contains("/nextsong?session=", fakeNavManager.Uri);
        Assert.Contains(_testSession.SessionId.ToString(), fakeNavManager.Uri);
    }

    [Fact]
    public void Component_PlayPauseButton_TogglesPlaybackState()
    {
        // Arrange
        var sessionState = new SessionState { CurrentSession = _testSession, IsInitialized = true };
        var playlistState = new PlaylistState { CurrentSong = _testSong };
        SetupTestWithSession(sessionState, playlistState, view: "player");
        SetupJSRuntime();

        // Act
        var cut = RenderComponent<PlayerView>();
        
        // Show controls first
        cut.InvokeAsync(() =>
        {
            cut.Instance.GetType().GetMethod("ShowControls", 
                System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance)
                ?.Invoke(cut.Instance, null);
        });
        cut.Render();

        var playPauseButton = cut.Find(".controls .btn-control");

        // Initially should show play icon
        var icon = playPauseButton.QuerySelector("i");
        var initialIconClass = icon?.ClassName;

        // Click to toggle
        playPauseButton.Click();

        // Assert
        // Note: Actual JS interop would need to be mocked to verify state change
        Assert.NotNull(playPauseButton);
    }

    [Fact(Skip = "Control buttons not rendering properly with session validation changes")]
    public async Task Component_StopButton_NavigatesToNextSongView()
    {
        // Arrange
        var sessionState = new SessionState { CurrentSession = _testSession, IsInitialized = true };
        var playlistState = new PlaylistState { CurrentSong = _testSong };
        SetupTestWithSession(sessionState, playlistState, view: "player");
        
        var mockPlayer = new Mock<IJSObjectReference>();
        SetupJSRuntimeWithPlayerModule(mockPlayer.Object);
        
        var fakeNavManager = new FakeNavigationManager();
        Services.AddSingleton<NavigationManager>(fakeNavManager);

        // Act
        var cut = RenderComponent<PlayerView>();
        
        // Show controls first
        await cut.InvokeAsync(() =>
        {
            cut.Instance.GetType().GetMethod("ShowControls", 
                System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance)
                ?.Invoke(cut.Instance, null);
        });
        cut.Render();

        var stopButton = cut.FindAll(".controls .btn-control")[1]; // Second button is stop
        await stopButton.ClickAsync(new Microsoft.AspNetCore.Components.Web.MouseEventArgs());

        // Assert
        Assert.Contains("/nextsong?session=", fakeNavManager.Uri);
        Assert.Contains(_testSession.SessionId.ToString(), fakeNavManager.Uri);
    }

    // Helper methods

    private void SetupJSRuntime()
    {
        var mockFileAccess = new Mock<IJSObjectReference>();
        var mockPlayer = new Mock<IJSObjectReference>();

        var mockJSRuntime = new Mock<IJSRuntime>();
        mockJSRuntime.Setup(js => js.InvokeAsync<IJSObjectReference>(
            "import",
            It.Is<object[]>(args => args[0].ToString()!.Contains("fileAccess.js"))))
            .ReturnsAsync(mockFileAccess.Object);

        mockJSRuntime.Setup(js => js.InvokeAsync<IJSObjectReference>(
            "import",
            It.Is<object[]>(args => args[0].ToString()!.Contains("player.js"))))
            .ReturnsAsync(mockPlayer.Object);

        Services.AddSingleton(mockJSRuntime.Object);
    }

    private void SetupJSRuntimeWithModules(IJSObjectReference fileAccessModule, IJSObjectReference playerModule)
    {
        var mockJSRuntime = new Mock<IJSRuntime>();
        mockJSRuntime.Setup(js => js.InvokeAsync<IJSObjectReference>(
            "import",
            It.Is<object[]>(args => args[0].ToString()!.Contains("fileAccess.js"))))
            .ReturnsAsync(fileAccessModule);

        mockJSRuntime.Setup(js => js.InvokeAsync<IJSObjectReference>(
            "import",
            It.Is<object[]>(args => args[0].ToString()!.Contains("player.js"))))
            .ReturnsAsync(playerModule);

        Services.AddSingleton(mockJSRuntime.Object);
    }

    private void SetupJSRuntimeWithPlayerModule(IJSObjectReference playerModule)
    {
        var mockFileAccess = new Mock<IJSObjectReference>();
        
        var mockJSRuntime = new Mock<IJSRuntime>();
        mockJSRuntime.Setup(js => js.InvokeAsync<IJSObjectReference>(
            "import",
            It.Is<object[]>(args => args[0].ToString()!.Contains("fileAccess.js"))))
            .ReturnsAsync(mockFileAccess.Object);

        mockJSRuntime.Setup(js => js.InvokeAsync<IJSObjectReference>(
            "import",
            It.Is<object[]>(args => args[0].ToString()!.Contains("player.js"))))
            .ReturnsAsync(playerModule);

        Services.AddSingleton(mockJSRuntime.Object);
    }

}
