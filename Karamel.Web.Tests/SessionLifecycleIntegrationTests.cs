using Bunit;
using Fluxor;
using Karamel.Web.Store.Playlist;
using Karamel.Web.Store.Session;
using Karamel.Web.Models;
using Karamel.Web.Services;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.JSInterop;
using Microsoft.AspNetCore.Components;
using Moq;

namespace Karamel.Web.Tests
{
    /// <summary>
    /// Cross-tab / integration tests verifying that SignalR session lifecycle events
    /// (pause / resume) propagate through PlaylistEffects and update SessionState.IsPaused.
    ///
    /// Also verifies that playlist advancement is suppressed while paused.
    /// </summary>
    public class SessionLifecycleIntegrationTests
    {
        // ─── Helper: minimal IJSRuntime that satisfies service resolution ─────────

        private sealed class NoOpJSRuntime : IJSRuntime
        {
            private sealed class NoOpJSRef : IJSObjectReference
            {
                public ValueTask<TValue> InvokeAsync<TValue>(string id, object?[]? a) => new(default(TValue)!);
                public ValueTask<TValue> InvokeAsync<TValue>(string id, CancellationToken ct, object?[]? a) => new(default(TValue)!);
                public ValueTask DisposeAsync() => ValueTask.CompletedTask;
            }

            public ValueTask<TValue> InvokeAsync<TValue>(string id, object?[]? a)
                => id == "import" ? new ValueTask<TValue>((TValue)(object)new NoOpJSRef()) : new(default(TValue)!);

            public ValueTask<TValue> InvokeAsync<TValue>(string id, CancellationToken ct, object?[]? a)
                => InvokeAsync<TValue>(id, a);
        }

        // ─── Helper: mock IPlaylistStateSynchronizer with a raiseable event ──────

        private sealed class ControllableStateSynchronizer : IPlaylistStateSynchronizer
        {
            public event Action<BroadcastStateUpdate>? StateUpdateReceived;

            public void Raise(BroadcastStateUpdate update) => StateUpdateReceived?.Invoke(update);

            public Task<(Session? session, List<Karamel.Web.Contracts.PlaylistItemDto>? playlist, Karamel.Web.Contracts.SongDto? currentSong)>
                RestoreSessionStateAsync(Guid sessionId) => Task.FromResult<(Session?, List<Karamel.Web.Contracts.PlaylistItemDto>?, Karamel.Web.Contracts.SongDto?)>((null, null, null));

            public Task SetupStateUpdateListenerAsync() => Task.CompletedTask;

            public void HandleBroadcastMessage(string type, System.Text.Json.JsonElement data) { }

            public ValueTask DisposeAsync() => ValueTask.CompletedTask;
        }

        // ─── Setup helper ─────────────────────────────────────────────────────────

        private static async Task<(IStore store, IDispatcher dispatcher, IState<SessionState> sessionState,
            IState<PlaylistState> playlistState, ControllableStateSynchronizer sync,
            Mock<ISignalRPlaylistBridge> bridgeMock)>
            BuildContextAsync(Guid sessionId)
        {
            var ctx = new TestContext();

            ctx.Services.AddSingleton<NavigationManager>(
                new FakeNavigationManagerForLifecycle($"http://localhost/playlist?session={sessionId}"));
            ctx.Services.AddSingleton<IJSRuntime>(new NoOpJSRuntime());

            var bridgeMock = new Mock<ISignalRPlaylistBridge>();
            ctx.Services.AddSingleton(bridgeMock.Object);

            var connMock = new Mock<ISignalRConnectionManager>();
            connMock.Setup(m => m.IsMainTab).Returns(true);
            ctx.Services.AddSingleton(connMock.Object);

            var sync = new ControllableStateSynchronizer();
            ctx.Services.AddSingleton<IPlaylistStateSynchronizer>(sync);

            ctx.Services.AddSingleton(new Mock<ISessionApiClient>().Object);
            ctx.Services.AddSingleton(new Mock<ISessionStorageService>().Object);
            ctx.Services.AddSingleton(new Mock<ISongEnrichmentService>().Object);

            ctx.Services.AddFluxor(o => o.ScanAssemblies(typeof(SessionState).Assembly));

            var store = ctx.Services.GetRequiredService<IStore>();
            await store.InitializeAsync();

            var dispatcher = ctx.Services.GetRequiredService<IDispatcher>();
            var sessionState = ctx.Services.GetRequiredService<IState<SessionState>>();
            var playlistState = ctx.Services.GetRequiredService<IState<PlaylistState>>();

            // Initialize session
            dispatcher.Dispatch(new InitializeSessionAction(new Session { SessionId = sessionId }));

            return (store, dispatcher, sessionState, playlistState, sync, bridgeMock);
        }

        // ─── Tests ────────────────────────────────────────────────────────────────

        [Fact]
        public async Task PauseBroadcast_UpdatesSessionState_IsPausedTrue()
        {
            var sessionId = Guid.NewGuid();
            var (_, _, sessionState, _, sync, _) = await BuildContextAsync(sessionId);

            Assert.False(sessionState.Value.IsPaused, "Precondition: not paused");

            // Simulate ReceiveSessionPaused arriving via SignalR → JS → HandleBroadcastMessage → StateUpdateReceived
            sync.Raise(new BroadcastStateUpdate("session-paused", null, null, null));

            // Allow async Fluxor effects to settle
            await Task.Delay(50);

            Assert.True(sessionState.Value.IsPaused, "SessionState.IsPaused should be true after receiving pause event");
        }

        [Fact]
        public async Task ResumeBroadcast_AfterPause_UpdatesSessionState_IsPausedFalse()
        {
            var sessionId = Guid.NewGuid();
            var (_, dispatcher, sessionState, _, sync, _) = await BuildContextAsync(sessionId);

            // First pause
            dispatcher.Dispatch(new PauseSessionAction(IsAdminInitiated: false));
            await Task.Delay(50);
            Assert.True(sessionState.Value.IsPaused, "Precondition: should be paused");

            // Now resume via broadcast
            sync.Raise(new BroadcastStateUpdate("session-resumed", null, null, null));
            await Task.Delay(50);

            Assert.False(sessionState.Value.IsPaused, "SessionState.IsPaused should be false after receiving resume event");
        }

        [Fact]
        public async Task AdvanceToNextSong_WhenPaused_DoesNotInvokeSignalRBridge()
        {
            var sessionId = Guid.NewGuid();
            var (_, dispatcher, sessionState, _, _, bridgeMock) = await BuildContextAsync(sessionId);

            // Pause the session
            dispatcher.Dispatch(new PauseSessionAction(IsAdminInitiated: false));
            await Task.Delay(50);
            Assert.True(sessionState.Value.IsPaused, "Precondition: should be paused");

            // Dispatch advance – the effect should suppress this while paused
            dispatcher.Dispatch(new AdvanceToNextSongAction());
            await Task.Delay(50);

            bridgeMock.Verify(b => b.AdvanceToNextSongAsync(), Times.Never,
                "AdvanceToNextSongAsync should NOT be called while session is paused");
        }

        [Fact]
        public async Task AdvanceToNextSong_WhenNotPaused_InvokesSignalRBridge()
        {
            var sessionId = Guid.NewGuid();
            var (_, dispatcher, sessionState, _, _, bridgeMock) = await BuildContextAsync(sessionId);

            Assert.False(sessionState.Value.IsPaused, "Precondition: not paused");

            dispatcher.Dispatch(new AdvanceToNextSongAction());
            await Task.Delay(50);

            bridgeMock.Verify(b => b.AdvanceToNextSongAsync(), Times.Once,
                "AdvanceToNextSongAsync should be invoked when not paused");
        }

        [Fact]
        public async Task AdminPauseAction_InvokesSignalRBridgePauseSession()
        {
            var sessionId = Guid.NewGuid();
            var (_, dispatcher, _, _, _, bridgeMock) = await BuildContextAsync(sessionId);

            // Admin clicks pause → IsAdminInitiated=true → effect should call PauseSessionAsync
            dispatcher.Dispatch(new PauseSessionAction(IsAdminInitiated: true));
            await Task.Delay(50);

            bridgeMock.Verify(b => b.PauseSessionAsync(), Times.Once,
                "PauseSessionAsync should be called when admin initiates pause");
        }

        [Fact]
        public async Task AdminResumeAction_InvokesSignalRBridgeResumeSession()
        {
            var sessionId = Guid.NewGuid();
            var (_, dispatcher, _, _, _, bridgeMock) = await BuildContextAsync(sessionId);

            // Admin clicks resume → IsAdminInitiated=true → effect should call ResumeSessionAsync
            dispatcher.Dispatch(new ResumeSessionAction(IsAdminInitiated: true));
            await Task.Delay(50);

            bridgeMock.Verify(b => b.ResumeSessionAsync(), Times.Once,
                "ResumeSessionAsync should be called when admin initiates resume");
        }

        [Fact]
        public async Task BroadcastPauseAction_DoesNotInvokeSignalRBridge()
        {
            var sessionId = Guid.NewGuid();
            var (_, _, _, _, sync, bridgeMock) = await BuildContextAsync(sessionId);

            // Simulate broadcast-triggered pause (IsAdminInitiated=false, no hub call expected)
            sync.Raise(new BroadcastStateUpdate("session-paused", null, null, null));
            await Task.Delay(50);

            bridgeMock.Verify(b => b.PauseSessionAsync(), Times.Never,
                "PauseSessionAsync should NOT be called for broadcast-triggered pause (prevents SignalR loop)");
        }
    }

    /// <summary>
    /// Local NavigationManager for session lifecycle tests.
    /// </summary>
    internal class FakeNavigationManagerForLifecycle : NavigationManager
    {
        public FakeNavigationManagerForLifecycle(string uri = "http://localhost/")
        {
            var baseUri = new Uri(uri);
            var baseUrl = $"{baseUri.Scheme}://{baseUri.Host}{(baseUri.IsDefaultPort ? "" : $":{baseUri.Port}")}/";
            Initialize(baseUrl, uri);
        }

        protected override void NavigateToCore(string uri, bool forceLoad) { }
    }
}
