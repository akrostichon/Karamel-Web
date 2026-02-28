using Bunit;
using Karamel.Web.Components;
using Karamel.Web.Contracts;
using Karamel.Web.Models;
using Karamel.Web.Store.Session;
using Karamel.Web.Store.Playlist;
using Karamel.Web.Tests.TestHelpers;
using Karamel.Web.Services;
using Microsoft.AspNetCore.Components;
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
            SessionState sessionState,
            PlaylistState? playlistState = null)
        {
            var mockSessionState = new Mock<IState<SessionState>>();
            mockSessionState.Setup(s => s.Value).Returns(sessionState);

            var mockPlaylistState = new Mock<IState<PlaylistState>>();
            mockPlaylistState.Setup(s => s.Value).Returns(playlistState ?? new PlaylistState());

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

        private static PlaylistItemDto MakeItem(string id, SongStatus status) =>
            new(id, null, "Artist", "Title", null, 0, (int)status);

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
        public void SessionControls_ClickNextButton_WhenNowPlayingExists_DispatchesCompleteCurrentSong()
        {
            var sessionState = new SessionState { CurrentSession = _testSession, IsPaused = false };
            // CurrentSong (not Items) represents the actively playing song
            var playlistState = new PlaylistState
            {
                CurrentSong = MakeItem("item-1", SongStatus.NowPlaying)
            };
            var (mockDispatcher, _) = SetupServices(sessionState, playlistState);

            var cut = RenderComponent<SessionControls>(p => p
                .Add(x => x.IsAdminTab, true));

            cut.Find(".btn-next").Click();

            // Should mark current song completed; PlayerView handles navigation itself
            mockDispatcher.Verify(
                d => d.Dispatch(It.IsAny<CompleteCurrentSongAction>()),
                Times.Once);
            mockDispatcher.Verify(
                d => d.Dispatch(It.IsAny<AdvanceToNextSongAction>()),
                Times.Never);
        }

        [Fact]
        public void SessionControls_ClickNextButton_WhenNoNowPlayingButQueuedExists_DispatchesAdvanceToNextSong()
        {
            var sessionState = new SessionState { CurrentSession = _testSession, IsPaused = false };
            var playlistState = new PlaylistState
            {
                Items = [MakeItem("item-1", SongStatus.UpNext)]
            };
            var (mockDispatcher, _) = SetupServices(sessionState, playlistState);

            var cut = RenderComponent<SessionControls>(p => p
                .Add(x => x.IsAdminTab, true));

            cut.Find(".btn-next").Click();

            // No song playing: promote next queued song to NowPlaying
            mockDispatcher.Verify(
                d => d.Dispatch(It.IsAny<CompleteCurrentSongAction>()),
                Times.Never);
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
            // Need at least one queued/upnext song for the button to be enabled
            var playlistState = new PlaylistState { Items = [MakeItem("item-1", SongStatus.UpNext)] };
            SetupServices(sessionState, playlistState);

            var cut = RenderComponent<SessionControls>(p => p
                .Add(x => x.IsAdminTab, true));

            var nextBtn = cut.Find(".btn-next");
            Assert.False(nextBtn.HasAttribute("disabled"));
        }

        [Fact]
        public void SessionControls_WhenQueueEmpty_NextButtonIsDisabled()
        {
            // No items in the playlist → nothing to advance to
            var sessionState = new SessionState { CurrentSession = _testSession, IsPaused = false };
            SetupServices(sessionState, new PlaylistState());

            var cut = RenderComponent<SessionControls>(p => p
                .Add(x => x.IsAdminTab, true));

            var nextBtn = cut.Find(".btn-next");
            Assert.True(nextBtn.HasAttribute("disabled"));
        }

        // ─── Config section visibility ────────────────────────────────────────────

        [Fact]
        public void SessionControls_WhenConfigDisabled_DoesNotRenderConfigSection()
        {
            var sessionState = new SessionState { CurrentSession = _testSession, IsPaused = false };
            SetupServices(sessionState);

            var cut = RenderComponent<SessionControls>(p => p
                .Add(x => x.IsAdminTab, true)
                .Add(x => x.ConfigEnabled, false));

            // Config section must be absent when ConfigEnabled=false
            Assert.Throws<ElementNotFoundException>(() => cut.Find(".session-config-section"));
        }

        [Fact]
        public void SessionControls_WhenConfigEnabled_RendersAllFourInputs()
        {
            var sessionState = new SessionState { CurrentSession = _testSession, IsPaused = false };
            SetupServices(sessionState);

            var cut = RenderComponent<SessionControls>(p => p
                .Add(x => x.IsAdminTab, true)
                .Add(x => x.ConfigEnabled, true));

            // All four config inputs must be visible
            Assert.NotNull(cut.Find(".config-input-require-singer-name"));
            Assert.NotNull(cut.Find(".config-input-allow-reorder"));
            Assert.NotNull(cut.Find(".config-input-pause-seconds"));
            Assert.NotNull(cut.Find(".config-input-theme"));
        }

        [Fact]
        public void SessionControls_WhenConfigEnabled_InputsPopulatedFromSessionState()
        {
            var session = new Session
            {
                SessionId = _testSession.SessionId,
                RequireSingerName = true,
                AllowSingersToReorder = true,
                PauseBetweenSongsSeconds = 30,
                Theme = "dark"
            };
            var sessionState = new SessionState { CurrentSession = session, IsPaused = false };
            SetupServices(sessionState);

            var cut = RenderComponent<SessionControls>(p => p
                .Add(x => x.IsAdminTab, true)
                .Add(x => x.ConfigEnabled, true));

            var requireCheck = cut.Find(".config-input-require-singer-name");
            Assert.True(requireCheck.HasAttribute("checked") || requireCheck.GetAttribute("checked") != null
                || requireCheck.ToMarkup().Contains("checked"));

            var pauseInput = cut.Find(".config-input-pause-seconds");
            Assert.Equal("30", pauseInput.GetAttribute("value"));

            var themeSelect = cut.Find(".config-input-theme");
            Assert.Equal("dark", themeSelect.GetAttribute("value") ?? themeSelect.InnerHtml);
        }

        [Fact]
        public void SessionControls_WhenConfigEnabled_SaveButton_DispatchesSaveSessionConfigAction()
        {
            var sessionState = new SessionState { CurrentSession = _testSession, IsPaused = false };
            var (mockDispatcher, _) = SetupServices(sessionState);

            var cut = RenderComponent<SessionControls>(p => p
                .Add(x => x.IsAdminTab, true)
                .Add(x => x.ConfigEnabled, true));

            // Click Save
            cut.Find(".btn-save-config").Click();

            mockDispatcher.Verify(
                d => d.Dispatch(It.IsAny<SaveSessionConfigAction>()),
                Times.Once);
        }

        // ─── Pause-between-songs validation ──────────────────────────────────────

        [Fact]
        public void SessionControls_PauseBetweenSongs_NegativeValue_ShowsValidationError()
        {
            var sessionState = new SessionState { CurrentSession = _testSession, IsPaused = false };
            SetupServices(sessionState);

            var cut = RenderComponent<SessionControls>(p => p
                .Add(x => x.IsAdminTab, true)
                .Add(x => x.ConfigEnabled, true));

            cut.Find(".config-input-pause-seconds").Change("-1");

            Assert.NotNull(cut.Find(".config-pause-validation-error"));
        }

        [Fact]
        public void SessionControls_PauseBetweenSongs_BetweenOneAndFour_ShowsValidationError()
        {
            var sessionState = new SessionState { CurrentSession = _testSession, IsPaused = false };
            SetupServices(sessionState);

            var cut = RenderComponent<SessionControls>(p => p
                .Add(x => x.IsAdminTab, true)
                .Add(x => x.ConfigEnabled, true));

            cut.Find(".config-input-pause-seconds").Change("3");

            Assert.NotNull(cut.Find(".config-pause-validation-error"));
        }

        [Fact]
        public void SessionControls_PauseBetweenSongs_AboveNinety_ShowsValidationError()
        {
            var sessionState = new SessionState { CurrentSession = _testSession, IsPaused = false };
            SetupServices(sessionState);

            var cut = RenderComponent<SessionControls>(p => p
                .Add(x => x.IsAdminTab, true)
                .Add(x => x.ConfigEnabled, true));

            cut.Find(".config-input-pause-seconds").Change("91");

            Assert.NotNull(cut.Find(".config-pause-validation-error"));
        }

        [Fact]
        public void SessionControls_PauseBetweenSongs_Zero_NoValidationError()
        {
            var sessionState = new SessionState { CurrentSession = _testSession, IsPaused = false };
            SetupServices(sessionState);

            var cut = RenderComponent<SessionControls>(p => p
                .Add(x => x.IsAdminTab, true)
                .Add(x => x.ConfigEnabled, true));

            cut.Find(".config-input-pause-seconds").Change("0");

            Assert.Throws<ElementNotFoundException>(() => cut.Find(".config-pause-validation-error"));
        }

        [Fact]
        public void SessionControls_PauseBetweenSongs_ValidValueFive_NoValidationError()
        {
            var sessionState = new SessionState { CurrentSession = _testSession, IsPaused = false };
            SetupServices(sessionState);

            var cut = RenderComponent<SessionControls>(p => p
                .Add(x => x.IsAdminTab, true)
                .Add(x => x.ConfigEnabled, true));

            cut.Find(".config-input-pause-seconds").Change("5");

            Assert.Throws<ElementNotFoundException>(() => cut.Find(".config-pause-validation-error"));
        }

        [Fact]
        public void SessionControls_PauseBetweenSongs_ValidValueNinety_NoValidationError()
        {
            var sessionState = new SessionState { CurrentSession = _testSession, IsPaused = false };
            SetupServices(sessionState);

            var cut = RenderComponent<SessionControls>(p => p
                .Add(x => x.IsAdminTab, true)
                .Add(x => x.ConfigEnabled, true));

            cut.Find(".config-input-pause-seconds").Change("90");

            Assert.Throws<ElementNotFoundException>(() => cut.Find(".config-pause-validation-error"));
        }

        [Fact]
        public void SessionControls_SaveButton_DisabledWhenPauseValidationError()
        {
            var sessionState = new SessionState { CurrentSession = _testSession, IsPaused = false };
            SetupServices(sessionState);

            var cut = RenderComponent<SessionControls>(p => p
                .Add(x => x.IsAdminTab, true)
                .Add(x => x.ConfigEnabled, true));

            // Trigger a validation error
            cut.Find(".config-input-pause-seconds").Change("3");

            var saveBtn = cut.Find(".btn-save-config");
            Assert.True(saveBtn.HasAttribute("disabled"));
        }

        [Fact]
        public void SessionControls_SaveButton_EnabledWhenPauseValueIsValid()
        {
            var sessionState = new SessionState { CurrentSession = _testSession, IsPaused = false };
            SetupServices(sessionState);

            var cut = RenderComponent<SessionControls>(p => p
                .Add(x => x.IsAdminTab, true)
                .Add(x => x.ConfigEnabled, true));

            // Valid value – no error
            cut.Find(".config-input-pause-seconds").Change("10");

            var saveBtn = cut.Find(".btn-save-config");
            Assert.False(saveBtn.HasAttribute("disabled"));
        }

        // ─── Back-to-playlist button (T025 / T026) ───────────────────────────────

        [Fact]
        public void SessionControls_WhenConfigDisabled_BackToPlaylistButton_NotRendered()
        {
            // Iteration 3.1: ConfigEnabled=false → back button must not appear
            var sessionState = new SessionState { CurrentSession = _testSession, IsPaused = false };
            SetupServices(sessionState);

            var cut = RenderComponent<SessionControls>(p => p
                .Add(x => x.IsAdminTab, true)
                .Add(x => x.ConfigEnabled, false));

            Assert.Throws<ElementNotFoundException>(() => cut.Find(".btn-back-to-playlist"));
        }

        [Fact]
        public void SessionControls_WhenConfigEnabled_WithoutNavigateBackCallback_BackToPlaylistButton_NotRendered()
        {
            // ConfigEnabled=true but no callback provided → button must not appear
            var sessionState = new SessionState { CurrentSession = _testSession, IsPaused = false };
            SetupServices(sessionState);

            var cut = RenderComponent<SessionControls>(p => p
                .Add(x => x.IsAdminTab, true)
                .Add(x => x.ConfigEnabled, true));
            // OnNavigateBack not added → HasDelegate is false

            Assert.Throws<ElementNotFoundException>(() => cut.Find(".btn-back-to-playlist"));
        }

        [Fact]
        public void SessionControls_WhenConfigEnabled_WithNavigateBackCallback_BackToPlaylistButton_Rendered()
        {
            // Iteration 3.2: ConfigEnabled=true + callback → button appears
            var sessionState = new SessionState { CurrentSession = _testSession, IsPaused = false };
            SetupServices(sessionState);

            var cut = RenderComponent<SessionControls>(p => p
                .Add(x => x.IsAdminTab, true)
                .Add(x => x.ConfigEnabled, true)
                .Add(x => x.OnNavigateBack, EventCallback.Factory.Create(this, () => { })));

            Assert.NotNull(cut.Find(".btn-back-to-playlist"));
        }

        [Fact]
        public void SessionControls_BackToPlaylistButton_WhenClicked_InvokesCallback()
        {
            // Clicking the back button must invoke the provided callback
            var sessionState = new SessionState { CurrentSession = _testSession, IsPaused = false };
            SetupServices(sessionState);

            var callbackInvoked = false;

            var cut = RenderComponent<SessionControls>(p => p
                .Add(x => x.IsAdminTab, true)
                .Add(x => x.ConfigEnabled, true)
                .Add(x => x.OnNavigateBack, EventCallback.Factory.Create(this, () => { callbackInvoked = true; })));

            cut.Find(".btn-back-to-playlist").Click();

            Assert.True(callbackInvoked);
        }
    }
}
