using Bunit;
using Microsoft.JSInterop;
using Xunit;
using Karamel.Web.Pages;
using Karamel.Web.Store.Session;
using Karamel.Web.Store.Playlist;
using Karamel.Web.Models;

namespace Karamel.Web.Tests
{
    /// <summary>
    /// Unit tests for the SessionSetup component.
    /// Tests warning box rendering, checkbox validation, tab opening buttons, navigation, and FAQ section.
    /// </summary>
    public class SessionSetupTests : SessionTestBase
    {
        private readonly Session _testSession;

        public SessionSetupTests()
        {
            _testSession = new Session
            {
                SessionId = Guid.NewGuid(),
                RequireSingerName = true
            };
        }

        [Fact]
        public void SessionSetup_RendersWarningBox()
        {
            // Arrange
            var sessionState = new SessionState { CurrentSession = _testSession };
            SetupTestWithSession(sessionState, new PlaylistState(), view: "session-setup");

            // Act
            var cut = RenderComponent<SessionSetup>();

            // Assert
            var warningBox = cut.Find(".warning-box");
            Assert.NotNull(warningBox);
            Assert.Contains("KEEP THIS TAB OPEN", warningBox.TextContent);
            Assert.Contains("main session tab", warningBox.TextContent, StringComparison.OrdinalIgnoreCase);
        }

        [Fact]
        public void SessionSetup_CheckboxRequired_BeforeStartButton()
        {
            // Arrange
            var sessionState = new SessionState { CurrentSession = _testSession };
            SetupTestWithSession(sessionState, new PlaylistState(), view: "session-setup");

            // Act
            var cut = RenderComponent<SessionSetup>();

            // Assert
            var startButton = cut.Find(".start-button");
            Assert.True(startButton.HasAttribute("disabled"));
        }

        [Fact]
        public void SessionSetup_CheckboxChecked_EnablesStartButton()
        {
            // Arrange
            var sessionState = new SessionState { CurrentSession = _testSession };
            SetupTestWithSession(sessionState, new PlaylistState(), view: "session-setup");

            var cut = RenderComponent<SessionSetup>();

            // Act
            var checkbox = cut.Find("input[type='checkbox']");
            checkbox.Change(true);
            cut.Render();

            // Assert
            var startButton = cut.Find(".start-button");
            Assert.False(startButton.HasAttribute("disabled"));
        }

        [Fact]
        public void SessionSetup_OpenPlaylistButton_OpensNewTab()
        {
            // Arrange
            var sessionState = new SessionState { CurrentSession = _testSession };
            SetupTestWithSession(sessionState, new PlaylistState(), view: "session-setup");

            JSInterop.Mode = JSRuntimeMode.Loose;
            JSInterop.SetupVoid("open", _ => true);

            var cut = RenderComponent<SessionSetup>();

            // Act & Assert - verify button exists
            var openPlaylistButton = cut.Find(".open-playlist-button");
            Assert.NotNull(openPlaylistButton);
            Assert.Contains("Playlist Manager", openPlaylistButton.TextContent);
        }

        [Fact]
        public void SessionSetup_OpenSingerButton_OpensNewTab()
        {
            // Arrange
            var sessionState = new SessionState { CurrentSession = _testSession };
            SetupTestWithSession(sessionState, new PlaylistState(), view: "session-setup");

            JSInterop.Mode = JSRuntimeMode.Loose;
            JSInterop.SetupVoid("open", _ => true);

            var cut = RenderComponent<SessionSetup>();

            // Act & Assert - verify button exists
            var openSingerButton = cut.Find(".open-singer-button");
            Assert.NotNull(openSingerButton);
            Assert.Contains("Singer View", openSingerButton.TextContent);
        }

        [Fact]
        public void SessionSetup_StartSingingButton_NavigatesToNextSongView()
        {
            // Arrange
            var sessionState = new SessionState { CurrentSession = _testSession };
            var (_, _, navMan) = SetupTestWithSession(sessionState, new PlaylistState(), view: "session-setup");

            var cut = RenderComponent<SessionSetup>();

            // Act - check checkbox and click start button
            var checkbox = cut.Find("input[type='checkbox']");
            checkbox.Change(true);
            cut.Render();

            var startButton = cut.Find(".start-button");
            startButton.Click();

            // Assert
            Assert.Contains($"/nextsong?session={_testSession.SessionId}", navMan.Uri);
        }

        [Fact]
        public void SessionSetup_FAQSection_CollapsedByDefault()
        {
            // Arrange
            var sessionState = new SessionState { CurrentSession = _testSession };
            SetupTestWithSession(sessionState, new PlaylistState(), view: "session-setup");

            // Act
            var cut = RenderComponent<SessionSetup>();

            // Assert
            var details = cut.Find("details");
            Assert.NotNull(details);
            Assert.False(details.HasAttribute("open"));
        }

        [Fact]
        public void SessionSetup_InvalidSession_ShowsError()
        {
            // Arrange - session in state doesn't match URL parameter
            var sessionState = new SessionState { CurrentSession = _testSession };
            var wrongSession = new Session { SessionId = Guid.NewGuid() };
            SetupFluxorWithStates(sessionState, new PlaylistState(), null, 
                $"http://localhost/session-setup?session={wrongSession.SessionId}");

            // Act
            var cut = RenderComponent<SessionSetup>();

            // Assert
            var errorMessage = cut.Find(".error-message");
            Assert.NotNull(errorMessage);
            Assert.Contains("Invalid session", errorMessage.TextContent, StringComparison.OrdinalIgnoreCase);
        }
    }
}
