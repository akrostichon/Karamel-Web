using Bunit;
using Fluxor;
using Karamel.Web.Pages;
using Karamel.Web.Store.Playlist;
using Karamel.Web.Store.Session;
using Karamel.Web.Models;
using Karamel.Web.Tests.TestHelpers;
using Karamel.Web.Contracts;
using Karamel.Web.Services;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.JSInterop;
using Microsoft.AspNetCore.Components;
using Moq;

namespace Karamel.Web.Tests
{
    /// <summary>
    /// Simulates two tabs (Singer view + NextSongView) by creating two TestContexts.
    /// Singer tab dispatches AddToPlaylistAction; test simulates broadcast by
    /// applying UpdatePlaylistFromBroadcastAction to the secondary tab's store.
    /// </summary>
    public class TwoTabBroadcastSimulationTests
    {
        private class SimpleMockJSRuntime : IJSRuntime
        {
            public ValueTask<TValue> InvokeAsync<TValue>(string identifier, object?[]? args)
            {
                if (identifier == "import")
                {
                    return new ValueTask<TValue>((TValue)(object)new MockJSObjectReference());
                }
                return new ValueTask<TValue>(default(TValue)!);
            }

            public ValueTask<TValue> InvokeAsync<TValue>(string identifier, CancellationToken cancellationToken, object?[]? args)
                => InvokeAsync<TValue>(identifier, args);

            private class MockJSObjectReference : IJSObjectReference
            {
                public ValueTask<TValue> InvokeAsync<TValue>(string identifier, object?[]? args) => new ValueTask<TValue>(default(TValue)!);
                public ValueTask<TValue> InvokeAsync<TValue>(string identifier, CancellationToken cancellationToken, object?[]? args) => new ValueTask<TValue>(default(TValue)!);
                public ValueTask DisposeAsync() => ValueTask.CompletedTask;
            }
        }

        [Fact]
        public async Task SingerAddsSong_NextSongReceivesPlaylistUpdate()
        {
            var sessionId = Guid.NewGuid();

            // --- Setup Singer (main tab) context ---
            using var singerCtx = new TestContext();
            // Navigation URL includes session param
            var singerUrl = $"https://karaoke.example.com/singer?session={sessionId}";
            singerCtx.Services.AddSingleton<NavigationManager>(new FakeNavigationManager(singerUrl));
            singerCtx.Services.AddSingleton<IJSRuntime>(new SimpleMockJSRuntime());
        
        // Add service mocks BEFORE Fluxor
        var singerBridgeMock = new Mock<ISignalRPlaylistBridge>();
        singerBridgeMock.Setup(m => m.AddItemToPlaylistAsync(It.IsAny<Song>())).ReturnsAsync(true);
        singerCtx.Services.AddSingleton(singerBridgeMock.Object);
        
        var singerApiClientMock = new Mock<ISessionApiClient>();
        singerCtx.Services.AddSingleton(singerApiClientMock.Object);
        
        // Add ISignalRConnectionManager mock
        var singerConnectionMock = new Mock<ISignalRConnectionManager>();
        singerConnectionMock.Setup(m => m.IsMainTab).Returns(true);
        singerCtx.Services.AddSingleton(singerConnectionMock.Object);

        var singerStateSynchronizerMock = new Mock<IPlaylistStateSynchronizer>();
        singerCtx.Services.AddSingleton(singerStateSynchronizerMock.Object);
        
        var singerStorageMock = new Mock<ISessionStorageService>();
        singerCtx.Services.AddSingleton(singerStorageMock.Object);
        
        var singerEnrichmentMock = new Mock<ISongEnrichmentService>();
        singerCtx.Services.AddSingleton(singerEnrichmentMock.Object);
        
        singerCtx.Services.AddFluxor(options => options.ScanAssemblies(typeof(SessionState).Assembly));
        var singerStore = singerCtx.Services.GetRequiredService<IStore>();
        var singerDispatcher = singerCtx.Services.GetRequiredService<IDispatcher>();
        await singerStore.InitializeAsync();

            // Initialize session in singer context
            var initialSession = new Session { SessionId = sessionId };
            singerDispatcher.Dispatch(new InitializeSessionAction(initialSession));

            // Add a song from singer
            var song = new Song
            {
                Id = Guid.NewGuid(),
                Artist = "Sim Artist",
                Title = "Sim Song",
                Mp3FileName = "sim.mp3",
                CdgFileName = "sim.cdg",
                AddedBySinger = "Tester"
            };
            singerDispatcher.Dispatch(new AddToPlaylistAction(song, "Tester"));

            // Give effects time to run
            await Task.Delay(100);

            // Simulate SignalR broadcast in singer tab (effect would trigger this)
            var playlistItem = TestDataFactory.CreatePlaylistItem(song, 0, 0);
            singerDispatcher.Dispatch(new UpdatePlaylistFromBroadcastAction(new List<PlaylistItemDto> { playlistItem }, null));

            // Capture playlist state from singer context
            var singerPlaylistState = singerCtx.Services.GetRequiredService<IState<PlaylistState>>();
            var itemsList = singerPlaylistState.Value.Items;
            Assert.Single(itemsList); // ensure singer side has the song

            // --- Setup NextSong (secondary tab) context ---
            using var nextCtx = new TestContext();
            var nextUrl = $"https://karaoke.example.com/nextsong?session={sessionId}";
            nextCtx.Services.AddSingleton<NavigationManager>(new FakeNavigationManager(nextUrl));
            nextCtx.Services.AddSingleton<IJSRuntime>(new SimpleMockJSRuntime());
        
            // Add service mocks BEFORE Fluxor
            var nextBridgeMock = new Mock<ISignalRPlaylistBridge>();
            nextCtx.Services.AddSingleton(nextBridgeMock.Object);
            
            var nextApiClientMock = new Mock<ISessionApiClient>();
            nextCtx.Services.AddSingleton(nextApiClientMock.Object);
            
            // Add ISignalRConnectionManager mock
            var nextConnectionMock = new Mock<ISignalRConnectionManager>();
            nextConnectionMock.Setup(m => m.IsMainTab).Returns(false);
            nextCtx.Services.AddSingleton(nextConnectionMock.Object);

            var nextStateSynchronizerMock = new Mock<IPlaylistStateSynchronizer>();
            nextCtx.Services.AddSingleton(nextStateSynchronizerMock.Object);
            
            var nextStorageMock = new Mock<ISessionStorageService>();
            nextCtx.Services.AddSingleton(nextStorageMock.Object);
            
            var nextEnrichmentMock = new Mock<ISongEnrichmentService>();
            nextCtx.Services.AddSingleton(nextEnrichmentMock.Object);
            
            nextCtx.Services.AddFluxor(options => options.ScanAssemblies(typeof(SessionState).Assembly));
            var nextStore = nextCtx.Services.GetRequiredService<IStore>();
            var nextDispatcher = nextCtx.Services.GetRequiredService<IDispatcher>();
            await nextStore.InitializeAsync();

            // Initialize session in next context (secondary tab)
            nextDispatcher.Dispatch(new InitializeSessionAction(initialSession));

            // Simulate receiving broadcast by dispatching UpdatePlaylistFromBroadcastAction into next tab's store
            var broadcastItems = itemsList.ToList(); // Send playlist items as-is
            var broadcastCurrentSong = singerPlaylistState.Value.CurrentSong;

            nextDispatcher.Dispatch(new UpdatePlaylistFromBroadcastAction(broadcastItems, broadcastCurrentSong));

            // Render NextSongView in nextCtx
            var cut = nextCtx.RenderComponent<NextSongView>();

            // Assert the markup contains the song details
            Assert.Contains("Sim Artist", cut.Markup);
            Assert.Contains("Sim Song", cut.Markup);
            Assert.Contains("Tester", cut.Markup);
        }
    }
}

    // Local FakeNavigationManager for this test to avoid accessing protected nested classes
    internal class FakeNavigationManager : NavigationManager
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
            NavigationHistory.Add(uri);
            Uri = ToAbsoluteUri(uri).ToString();
        }
    }
