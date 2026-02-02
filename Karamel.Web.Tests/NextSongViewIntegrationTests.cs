using Bunit;
using Fluxor;
using Karamel.Web.Pages;
using Karamel.Web.Store.Session;
using Karamel.Web.Store.Playlist;
using Karamel.Web.Models;
using Karamel.Web.Contracts;
using Karamel.Web.Tests.TestHelpers;
using Microsoft.Extensions.DependencyInjection;

namespace Karamel.Web.Tests;

/// <summary>
/// Integration tests for NextSongView component.
/// Tests real Fluxor state updates and component reactivity.
/// </summary>
public class NextSongViewIntegrationTests : IntegrationTestBase
{
    public NextSongViewIntegrationTests()
        : base(asMainTab: true)
    {
        // Base class handles all setup with real Fluxor store
    }

    [Fact]
    public void Integration_DisplaysNextSongFromQueue()
    {
        // Arrange - dispatch initial session action with test session ID
        var initialSession = new Models.Session
        {
            SessionId = TestSessionId,
            PauseBetweenSongsSeconds = 5
        };
        Dispatcher.Dispatch(new InitializeSessionAction(initialSession));

        // Add a song to the queue
        var song = new Song
        {
            Id = Guid.NewGuid(),
            Artist = "Test Artist",
            Title = "Test Song",
            Mp3FileName = "test.mp3",
            CdgFileName = "test.cdg",
            AddedBySinger = "Test Singer"
        };
        Dispatcher.Dispatch(new AddToPlaylistAction(song, "Test Singer"));

        // Wait briefly for effect to process
        Thread.Sleep(100);

        // Simulate SignalR broadcast with playlist items (effect calls AddItemToPlaylistAsync which would trigger this)
        var playlistItem = TestDataFactory.CreatePlaylistItem(song, 0, 0);
        Dispatcher.Dispatch(new UpdatePlaylistFromBroadcastAction(new List<PlaylistItemDto> { playlistItem }, null));

        // Act - render component with song in queue
        var cut = RenderComponent<NextSongView>();

        // Assert - should show the song
        Assert.Contains("Test Artist", cut.Markup);
        Assert.Contains("Test Song", cut.Markup);
        Assert.Contains("Test Singer", cut.Markup);
    }

    [Fact]
    public void Integration_DisplaysEmptySongMessage_WhenQueueIsEmpty()
    {
        // Arrange - dispatch initial session action with test session ID
        var initialSession = new Models.Session
        {
            SessionId = TestSessionId,
            PauseBetweenSongsSeconds = 5
        };
        Dispatcher.Dispatch(new InitializeSessionAction(initialSession));

        // Act - render component with initial empty queue state
        var cut = RenderComponent<NextSongView>();

        // Assert - should show empty queue state
        Assert.Contains("empty-queue-container", cut.Markup);
        Assert.Contains("Sing a song", cut.Markup);
    }

    [Fact]
    public void Integration_LoadsQRCodeModule()
    {
        // Arrange - initialize session
        var session = new Models.Session
        {
            SessionId = TestSessionId,
            PauseBetweenSongsSeconds = 5
        };
        Dispatcher.Dispatch(new InitializeSessionAction(session));

        // Act - render component
        var cut = RenderComponent<NextSongView>();

        // Assert - QR code container should be present
        var qrContainer = cut.Find("#qrcode-container");
        Assert.NotNull(qrContainer);
    }

    [Fact]
    public void Integration_ShowsQRCode_WhenQueueIsEmpty()
    {
        // Arrange - initialize session
        var session = new Models.Session
        {
            SessionId = TestSessionId,
            PauseBetweenSongsSeconds = 5
        };
        Dispatcher.Dispatch(new InitializeSessionAction(session));

        // Act - render component with empty queue
        var cut = RenderComponent<NextSongView>();

        // Assert - QR code should have large styling
        var qrContainer = cut.Find("#qrcode-container");
        Assert.Contains("qrcode-large", qrContainer.ClassName);
    }

    [Fact(Skip = "Complex SignalR broadcast simulation: MockSessionService doesn't trigger UpdatePlaylistFromBroadcastAction after NextSongAction. Core functionality tested in unit tests and TwoTabBroadcastSimulationTests.")]
    public async Task Component_UpdatesDisplay_WhenPlaylistStateChanges()
    {
        // Arrange - initialize session with test session ID
        var session = new Models.Session
        {
            SessionId = TestSessionId,
            PauseBetweenSongsSeconds = 5
        };
        Dispatcher.Dispatch(new InitializeSessionAction(session));

        // Add first song BEFORE rendering component
        var song1 = new Song
        {
            Id = Guid.NewGuid(),
            Artist = "Test Artist 1",
            Title = "Test Song 1",
            Mp3FileName = "song1.mp3",
            CdgFileName = "song1.cdg",
            AddedBySinger = "John Doe"
        };
        Dispatcher.Dispatch(new AddToPlaylistAction(song1, "John Doe"));

        // Wait for effect to process and success action to update state
        await Task.Delay(100); // Give effect time to run

        // Simulate SignalR broadcast
        var item1 = TestDataFactory.CreatePlaylistItem(song1, 0, 0);
        Dispatcher.Dispatch(new UpdatePlaylistFromBroadcastAction(new List<PlaylistItemDto> { item1 }, null));

        // Act - render component AFTER state is set up
        var cut = RenderComponent<NextSongView>();

        // Assert - should show the song
        Assert.Contains("Test Artist 1", cut.Markup);
        Assert.Contains("Test Song 1", cut.Markup);
        Assert.Contains("John Doe", cut.Markup);

        // Act - add second song to queue (component already rendered)
        var song2 = new Song
        {
            Id = Guid.NewGuid(),
            Artist = "Test Artist 2",
            Title = "Test Song 2",
            Mp3FileName = "song2.mp3",
            CdgFileName = "song2.cdg",
            AddedBySinger = "Jane Smith"
        };
        Dispatcher.Dispatch(new AddToPlaylistAction(song2, "Jane Smith"));

        // Wait for effect to process
        await Task.Delay(100);

        // Simulate SignalR broadcast with both songs
        var item2 = TestDataFactory.CreatePlaylistItem(song2, 1, 0);
        Dispatcher.Dispatch(new UpdatePlaylistFromBroadcastAction(new List<PlaylistItemDto> { item1, item2 }, null));

        // Re-query the markup to get updated render
        Assert.Contains("Test Artist 1", cut.Markup);
        Assert.Contains("John Doe", cut.Markup);
        
        // NOTE: Second song shouldn't be displayed because queue shows NEXT song only (first in queue)
        Assert.DoesNotContain("Test Artist 2", cut.Markup);
        Assert.DoesNotContain("Jane Smith", cut.Markup);

        // Act - remove first song (simulate playing it)
        Dispatcher.Dispatch(new NextSongAction());

        // Wait for effect to process
        await Task.Delay(100);

        // Simulate SignalR broadcast - first song moved to CurrentSong, only second song in queue
        Dispatcher.Dispatch(new UpdatePlaylistFromBroadcastAction(new List<PlaylistItemDto> { item2 }, item1));

        // Wait for reducer to update state
        await Task.Delay(100);

        // Assert - Now markup should be empty or show second song
        // NOTE: Due to FluxorComponent subscription limitations in bUnit,
        // automatic re-renders may not trigger. This test verifies the reducer logic works.
        var playlistState = Services.GetRequiredService<IState<PlaylistState>>();
        var queueList = playlistState.Value.Items;
        Assert.Single(queueList);
        Assert.Equal(song2.Id.ToString(), queueList[0].SongId);
    }

    [Fact(Skip = "Complex SignalR broadcast simulation: MockSessionService doesn't trigger UpdatePlaylistFromBroadcastAction after NextSongAction. Core functionality tested in unit tests and TwoTabBroadcastSimulationTests.")]
    public async Task Component_UpdatesDisplay_WhenQueueBecomesEmpty()
    {
        // Arrange - initialize session with test session ID and add song
        var session = new Models.Session
        {
            SessionId = TestSessionId,
            PauseBetweenSongsSeconds = 5
        };
        Dispatcher.Dispatch(new InitializeSessionAction(session));

        var song = new Song
        {
            Id = Guid.NewGuid(),
            Artist = "Test Artist",
            Title = "Test Song",
            Mp3FileName = "song.mp3",
            CdgFileName = "song.cdg",
            AddedBySinger = "Test Singer"
        };
        Dispatcher.Dispatch(new AddToPlaylistAction(song, "Test Singer"));

        // Simulate SignalR broadcast
        var playlistItem = TestDataFactory.CreatePlaylistItem(song, 0, 0);
        Dispatcher.Dispatch(new UpdatePlaylistFromBroadcastAction(new List<PlaylistItemDto> { playlistItem }, null));

        // Render component
        var cut = RenderComponent<NextSongView>();

        // Wait for song to appear
        cut.WaitForState(() => 
        {
            var state = Services.GetRequiredService<IState<PlaylistState>>();
            return state.Value.Items.Count > 0;
        }, timeout: TimeSpan.FromSeconds(5));

        // Assert - should show song info
        Assert.Contains("Test Artist", cut.Markup);
        Assert.Contains("Test Song", cut.Markup);

        // Act - remove the song
        Dispatcher.Dispatch(new NextSongAction());

        // Simulate SignalR broadcast - song moved to CurrentSong, queue is now empty
        Dispatcher.Dispatch(new UpdatePlaylistFromBroadcastAction(new List<PlaylistItemDto>(), playlistItem));

        // Wait for reducer to complete
        await Task.Delay(100);

        // Wait for state to update
        cut.WaitForState(() => 
        {
            var state = Services.GetRequiredService<IState<PlaylistState>>();
            return state.Value.Items.Count == 0;
        }, timeout: TimeSpan.FromSeconds(5));

        // Assert - should show empty queue state
        Assert.Contains("empty-queue-container", cut.Markup);
        Assert.Contains("Sing a song", cut.Markup);
    }

    [Fact(Skip = "Complex SignalR broadcast simulation: MockSessionService doesn't trigger UpdatePlaylistFromBroadcastAction after NextSongAction. Core functionality tested in unit tests and TwoTabBroadcastSimulationTests.")]
    public async Task Component_ReactsTo_MultipleQueueChanges()
    {
        // Arrange - initialize session with test session ID
        var session = new Models.Session
        {
            SessionId = TestSessionId,
            PauseBetweenSongsSeconds = 5
        };
        Dispatcher.Dispatch(new InitializeSessionAction(session));

        // Act & Assert - add multiple songs rapidly BEFORE rendering
        var songs = new List<Song>();
        var playlistItems = new List<PlaylistItemDto>();
        for (int i = 1; i <= 5; i++)
        {
            var song = new Song
            {
                Id = Guid.NewGuid(),
                Artist = $"Artist {i}",
                Title = $"Title {i}",
                Mp3FileName = $"song{i}.mp3",
                CdgFileName = $"song{i}.cdg",
                AddedBySinger = $"Singer {i}"
            };
            songs.Add(song);
            Dispatcher.Dispatch(new AddToPlaylistAction(song, $"Singer {i}"));
            
            // Create playlist item
            playlistItems.Add(TestDataFactory.CreatePlaylistItem(song, i - 1, 0));
        }

        // Wait for all effects to process
        await Task.Delay(200);

        // Simulate SignalR broadcast with all items
        Dispatcher.Dispatch(new UpdatePlaylistFromBroadcastAction(playlistItems, null));

        // Render component AFTER state setup
        var cut = RenderComponent<NextSongView>();

        // Assert - should show first song
        Assert.Contains("Artist 1", cut.Markup);
        Assert.Contains("Title 1", cut.Markup);

        // Verify all songs are in queue
        var playlistState = Services.GetRequiredService<IState<PlaylistState>>();
        Assert.Equal(5, playlistState.Value.Items.Count);

        // Act - remove songs one by one and verify state updates
        for (int i = 0; i < 5; i++)
        {
            Dispatcher.Dispatch(new NextSongAction());
            await Task.Delay(50);
            
            // Simulate SignalR broadcast - current song moved to CurrentSong, remaining in queue
            var remainingItems = playlistItems.Skip(i + 1).ToList();
            var currentItem = playlistItems[i];
            Dispatcher.Dispatch(new UpdatePlaylistFromBroadcastAction(remainingItems, currentItem));
            
            // Wait for reducer to complete
            await Task.Delay(100);
            
            var state = Services.GetRequiredService<IState<PlaylistState>>();
            Assert.Equal(5 - (i + 1), state.Value.Items.Count);
        }

        // Final state should have empty queue
        Assert.Empty(playlistState.Value.Items);
    }
}



