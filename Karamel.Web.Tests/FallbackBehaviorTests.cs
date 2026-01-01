using Bunit;
using Karamel.Web.Pages;
using Karamel.Web.Store.Session;
using Karamel.Web.Store.Playlist;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.JSInterop;
using Moq;

namespace Karamel.Web.Tests;

public class FallbackBehaviorTests : SessionTestBase
{
    [Fact]
    public async Task PlayerView_WhenFileAccessDenied_ShowsFileAccessError()
    {
        // Arrange
        var testSession = CreateTestSession();
        var testSong = CreateTestSong();
        var sessionState = new SessionState { CurrentSession = testSession, IsInitialized = true };
        var playlistState = new PlaylistState { CurrentSong = testSong };
        SetupTestWithSession(sessionState, playlistState, view: "player");

        // Mock JSRuntime import to throw for fileAccess to simulate denied access
        var mockJSRuntime = new Mock<IJSRuntime>();
        mockJSRuntime.Setup(js => js.InvokeAsync<IJSObjectReference>(
            "import",
            It.Is<object[]>(args => args[0].ToString()!.Contains("fileAccess.js"))))
            .ThrowsAsync(new JSException("File System Access API not available"));

        // Other imports return a harmless module
        var mockPlayerModule = new Mock<IJSObjectReference>();
        mockJSRuntime.Setup(js => js.InvokeAsync<IJSObjectReference>(
            "import",
            It.Is<object[]>(args => args[0].ToString()!.Contains("player.js"))))
            .ReturnsAsync(mockPlayerModule.Object);

        // Remove any previously-registered IJSRuntime (from SetupTestWithSession) so our mock is used
        var existing = Services.FirstOrDefault(sd => sd.ServiceType == typeof(IJSRuntime));
        if (existing != null) Services.Remove(existing);
        Services.AddSingleton(mockJSRuntime.Object);

        // Act
        var cut = RenderComponent<PlayerView>();

        // Allow async initialization to complete
        await Task.Delay(100);

        // Assert: should render error overlay with initialization failure (friendly browser message)
        var errorOverlays = cut.FindAll(".error-overlay");
        Assert.NotEmpty(errorOverlays);
        Assert.Contains("Browser does not support required File System Access API", errorOverlays[0].TextContent);
    }

    [Fact]
    public async Task PlayerView_WhenNoCdg_ShowsMissingCdgFallback()
    {
        // Arrange
        var testSession = CreateTestSession();
        var testSong = CreateTestSong();
        var sessionState = new SessionState { CurrentSession = testSession, IsInitialized = true };
        var playlistState = new PlaylistState { CurrentSong = testSong };
        SetupTestWithSession(sessionState, playlistState, view: "player");

        // Mock fileAccess module to return a loadSongFiles result indicating missing CDG
        var mockFileAccessModule = new Mock<IJSObjectReference>();
        // Setup InvokeAsync<object> to throw to simulate missing CDG during load
        mockFileAccessModule.Setup(m => m.InvokeAsync<object>(
            "loadSongFiles",
            It.IsAny<object[]>() )).ThrowsAsync(new JSException("missing_cdg"));

        var mockPlayerModule = new Mock<IJSObjectReference>();

        var mockJSRuntime = new Mock<IJSRuntime>();
        mockJSRuntime.Setup(js => js.InvokeAsync<IJSObjectReference>(
            "import",
            It.Is<object[]>(args => args[0].ToString()!.Contains("fileAccess.js"))))
            .ReturnsAsync(mockFileAccessModule.Object);

        mockJSRuntime.Setup(js => js.InvokeAsync<IJSObjectReference>(
            "import",
            It.Is<object[]>(args => args[0].ToString()!.Contains("player.js"))))
            .ReturnsAsync(mockPlayerModule.Object);

        // Remove any previously-registered IJSRuntime (from SetupTestWithSession) so our mock is used
        var existing = Services.FirstOrDefault(sd => sd.ServiceType == typeof(IJSRuntime));
        if (existing != null) Services.Remove(existing);
        Services.AddSingleton(mockJSRuntime.Object);

        // Act
        var cut = RenderComponent<PlayerView>();

        // Allow async initialization to complete and for loadSongFiles to be invoked
        await Task.Delay(100);

        // Assert: error overlay should show friendly missing-CDG message
        var errors = cut.FindAll(".error-overlay");
        Assert.NotEmpty(errors);
        Assert.Contains("Missing or corrupt CDG file", errors[0].TextContent);
    }

    [Fact]
    public async Task PlayerView_WhenCdgCorrupt_ShowsCorruptCdgFallback()
    {
        // Arrange
        var testSession = CreateTestSession();
        var testSong = CreateTestSong();
        var sessionState = new SessionState { CurrentSession = testSession, IsInitialized = true };
        var playlistState = new PlaylistState { CurrentSong = testSong };
        SetupTestWithSession(sessionState, playlistState, view: "player");

        // Mock fileAccess module to throw to simulate corrupt CDG during load
        var mockFileAccessModule = new Mock<IJSObjectReference>();
        mockFileAccessModule.Setup(m => m.InvokeAsync<object>(
            "loadSongFiles",
            It.IsAny<object[]>() )).ThrowsAsync(new JSException("corrupt_cdg"));

        var mockPlayerModule = new Mock<IJSObjectReference>();

        var mockJSRuntime = new Mock<IJSRuntime>();
        mockJSRuntime.Setup(js => js.InvokeAsync<IJSObjectReference>(
            "import",
            It.Is<object[]>(args => args[0].ToString()!.Contains("fileAccess.js"))))
            .ReturnsAsync(mockFileAccessModule.Object);

        mockJSRuntime.Setup(js => js.InvokeAsync<IJSObjectReference>(
            "import",
            It.Is<object[]>(args => args[0].ToString()!.Contains("player.js"))))
            .ReturnsAsync(mockPlayerModule.Object);

        // Remove any previously-registered IJSRuntime (from SetupTestWithSession) so our mock is used
        var existing = Services.FirstOrDefault(sd => sd.ServiceType == typeof(IJSRuntime));
        if (existing != null) Services.Remove(existing);
        Services.AddSingleton(mockJSRuntime.Object);

        // Act
        var cut = RenderComponent<PlayerView>();

        // Allow async initialization to complete and for loadSongFiles to be invoked
        await Task.Delay(100);

        // Assert: error overlay should show friendly corrupt-CDG message
        var errors = cut.FindAll(".error-overlay");
        Assert.NotEmpty(errors);
        Assert.Contains("Missing or corrupt CDG file", errors[0].TextContent);
    }
}
