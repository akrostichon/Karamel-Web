using Bunit;
using Karamel.Web.Components;
using Karamel.Web.Models;
using Karamel.Web.Store.Session;
using Karamel.Web.Store.Playlist;
using Karamel.Web.Tests.TestHelpers;
using Karamel.Web.Services;
using Microsoft.Extensions.DependencyInjection;
using Fluxor;
using Moq;

namespace Karamel.Web.Tests
{
    /// <summary>
    /// Component tests for SessionControls.razor verifying:
    /// - Admin tabs see pause/resume/next buttons; non-admin tabs see nothing.
    /// - Pause / resume buttons toggle correctly based on SessionState.IsPaused.
    /// - Clicking each button dispatches the correct Fluxor action.
    /// - Next button is disabled while session is paused.
    /// </summary>
    public class SessionControlsTests : TestContext
    {
        private readonly Session _testSession;

        public SessionControlsTests()
        {
            _testSession = new Session
            {
                SessionId = Guid.NewGuid(),
                RequireSingerName = false
            };
        }

        // ─── Helpers ─────────────────────────────────────────────────────────────

        private (Mock<IDispatcher> dispatcher, Mock<ISignalRPlaylistBridge> bridge) SetupServices(
            SessionState sessionState)
        {
            var mockSessionState = new Mock<IState<SessionState>>();
            mockSessionState.Setup(s => s.Value).Returns(sessionState);

            var mockPlaylistState = new Mock<IState<PlaylistState>>();
            mockPlaylistState.Setup(s => s.Value).Returns(new PlaylistState());

            var mockDispatcher = new Mock<IDispatcher>();
            var mockActionSubscriber = new Mock<IActionSubscriber>();
            var mockBridge = new Mock<ISignalRPlaylistBridge>();

            Services.AddSingleton(mockSessionState.Object);
            Services.AddSingleton(mockPlaylistState.Object);
            Services.AddSingleton(mockDispatcher.Object);
            Services.AddSingleton(mockActionSubscriber.Object);
            Services.AddSingleton(mockBridge.Object);

            return (mockDispatcher, mockBridge);
        }

        // ─── Visibility ───────────────────────────────────────────────────────────

        [Fact]
        public void SessionControls_WhenNotAdminTab_RendersNothing()
        {
            var sessionState = new SessionState { CurrentSession = _testSession, IsPaused = false };
            SetupServices(sessionState);

            var cut = RenderComponent<SessionControls>(p => p
                .Add(x => x.IsAdminTab, false));

            // Non-admin tab: the panel must be absent
            Assert.Throws<ElementNotFoundException>(() => cut.Find(".session-controls-panel"));
        }

        [Fact]
        public void SessionControls_WhenAdminTabAndNotPaused_ShowsPauseButton()
        {
            var sessionState = new SessionState { CurrentSession = _testSession, IsPaused = false };
            SetupServices(sessionState);

            var cut = RenderComponent<SessionControls>(p => p
                .Add(x => x.IsAdminTab, true));

            // Pause button visible, resume button absent
            Assert.NotNull(cut.Find(".btn-pause"));
            Assert.Throws<ElementNotFoundException>(() => cut.Find(".btn-resume"));
        }

        [Fact]
        public void SessionControls_WhenAdminTabAndPaused_ShowsResumeButton()
        {
            var sessionState = new SessionState { CurrentSession = _testSession, IsPaused = true };
            SetupServices(sessionState);

            var cut = RenderComponent<SessionControls>(p => p
                .Add(x => x.IsAdminTab, true));

            // Resume button visible, pause button absent
            Assert.NotNull(cut.Find(".btn-resume"));
            Assert.Throws<ElementNotFoundException>(() => cut.Find(".btn-pause"));
        }

        // ─── Action dispatch ──────────────────────────────────────────────────────

        [Fact]
        public void SessionControls_ClickPauseButton_DispatchesPauseSessionActionWithIsAdminInitiatedTrue()
        {
            var sessionState = new SessionState { CurrentSession = _testSession, IsPaused = false };
            var (mockDispatcher, _) = SetupServices(sessionState);

            var cut = RenderComponent<SessionControls>(p => p
                .Add(x => x.IsAdminTab, true));

            cut.Find(".btn-pause").Click();

            mockDispatcher.Verify(
                d => d.Dispatch(It.Is<PauseSessionAction>(a => a.IsAdminInitiated == true)),
                Times.Once);
        }

        [Fact]
        public void SessionControls_ClickResumeButton_DispatchesResumeSessionActionWithIsAdminInitiatedTrue()
        {
            var sessionState = new SessionState { CurrentSession = _testSession, IsPaused = true };
            var (mockDispatcher, _) = SetupServices(sessionState);

            var cut = RenderComponent<SessionControls>(p => p
                .Add(x => x.IsAdminTab, true));

            cut.Find(".btn-resume").Click();

            mockDispatcher.Verify(
                d => d.Dispatch(It.Is<ResumeSessionAction>(a => a.IsAdminInitiated == true)),
                Times.Once);
        }

        [Fact]
        public void SessionControls_ClickNextButton_WhenNotPaused_DispatchesAdvanceToNextSongAction()
        {
            var sessionState = new SessionState { CurrentSession = _testSession, IsPaused = false };
            var (mockDispatcher, _) = SetupServices(sessionState);

            var cut = RenderComponent<SessionControls>(p => p
                .Add(x => x.IsAdminTab, true));

            cut.Find(".btn-next").Click();

            mockDispatcher.Verify(
                d => d.Dispatch(It.IsAny<AdvanceToNextSongAction>()),
                Times.Once);
        }

        // ─── Next button disabled while paused ───────────────────────────────────

        [Fact]
        public void SessionControls_WhenPaused_NextButtonIsDisabled()
        {
            var sessionState = new SessionState { CurrentSession = _testSession, IsPaused = true };
            SetupServices(sessionState);

            var cut = RenderComponent<SessionControls>(p => p
                .Add(x => x.IsAdminTab, true));

            var nextBtn = cut.Find(".btn-next");
            Assert.True(nextBtn.HasAttribute("disabled"));
        }

        [Fact]
        public void SessionControls_WhenNotPaused_NextButtonIsEnabled()
        {
            var sessionState = new SessionState { CurrentSession = _testSession, IsPaused = false };
            SetupServices(sessionState);

            var cut = RenderComponent<SessionControls>(p => p
                .Add(x => x.IsAdminTab, true));

            var nextBtn = cut.Find(".btn-next");
            Assert.False(nextBtn.HasAttribute("disabled"));
        }
    }
}
