using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Net.Http.Json;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.AspNetCore.SignalR;
using Microsoft.AspNetCore.SignalR.Client;
using Microsoft.Extensions.DependencyInjection;
using Karamel.Backend.Repositories;
using Karamel.Backend.Models;
using Xunit;

namespace Karamel.Backend.Tests
{
    [Collection("SignalRTests")]
    public class PlaylistHubTests : IClassFixture<TestServerFactory>, IAsyncDisposable
    {
        private readonly TestServerFactory _factory;
        private readonly HttpClient _client;
        private HubConnection? _connection;

        public PlaylistHubTests(TestServerFactory factory)
        {
            _factory = factory;
            _client = _factory.CreateDefaultClient();
        }

        [Fact]
        public async Task Adding_Item_Broadcasts_PlaylistUpdate()
        {
            // create session
            var createReq = new { RequireSingerName = false, PauseBetweenSongsSeconds = 1, AllowSingersToReorder = false };
            var resp = await _client.PostAsJsonAsync("/api/sessions", createReq);
            resp.EnsureSuccessStatusCode();
            var created = await resp.Content.ReadFromJsonAsync<CreateResponse>();
            Assert.NotNull(created);

            // create playlist (repository-backed helper)
            var playlist = await CreatePlaylistAsync(created.Id, created.linkToken);
            Assert.NotNull(playlist);

            // Create a song in the database to get a valid songId
            var songId = await CreateSongAsync(created.Id, "X", "Y");

            // start a SignalR client and join the session group
            var baseUrl = _factory.Server.BaseAddress!.ToString().TrimEnd('/');
            _connection = new HubConnectionBuilder()
                .WithUrl(baseUrl + "/hubs/playlist", options =>
                {
                    options.HttpMessageHandlerFactory = _ => _factory.Server.CreateHandler();
                    options.Headers.Add("X-Link-Token", created.linkToken);
                })
                .Build();

            var tcs = new TaskCompletionSource<PlaylistUpdatedDto?>();
            _connection.On<PlaylistUpdatedDto>("ReceivePlaylistUpdated", dto => tcs.TrySetResult(dto));

            await _connection.StartAsync();
            await _connection.InvokeAsync("JoinSession", created.Id.ToString());

            // Add an item via the hub mutation (with token provided on connection)
            await _connection.InvokeAsync("AddItemAsync", created.Id, songId, "Z");

            // Expect a broadcast
            var cts = new CancellationTokenSource(TimeSpan.FromSeconds(5));
            var received = await tcs.Task.WaitAsync(cts.Token);
            Assert.NotNull(received);
            Assert.Equal(playlist!.id, received!.PlaylistId);
        }

        [Fact]
        public async Task Connects_To_PlaylistHub_And_Can_JoinSession()
        {
            var baseUrl = _factory.Server.BaseAddress?.ToString().TrimEnd('/') ?? "http://localhost";
            var hubUrl = new Uri(new Uri(baseUrl), "/hubs/playlist").ToString();

            var connection = new HubConnectionBuilder()
                .WithUrl(hubUrl, options =>
                {
                    options.HttpMessageHandlerFactory = _ => _factory.Server.CreateHandler();
                })
                .Build();

            await connection.StartAsync();

            // Join a fake session - server will accept and add to group. No exception == success.
            await connection.InvokeAsync("JoinSession", Guid.NewGuid().ToString());

            await connection.StopAsync();
            await connection.DisposeAsync();
        }

        [Fact]
        public async Task Hub_AddItemAsync_WithValidToken_Succeeds_And_Broadcasts()
        {
            // Create session and playlist
            var session = await CreateSessionAsync();
            var playlist = await CreatePlaylistAsync(session.Id, session.linkToken);

            // Create a song in the database to get a valid songId
            var songId = await CreateSongAsync(session.Id, "Artist1", "Title1");

            // Connect to hub with token
            var baseUrl = _factory.Server.BaseAddress!.ToString().TrimEnd('/');
            _connection = new HubConnectionBuilder()
                .WithUrl(baseUrl + "/hubs/playlist", options =>
                {
                    options.HttpMessageHandlerFactory = _ => _factory.Server.CreateHandler();
                    options.Headers.Add("X-Link-Token", session.linkToken);
                })
                .Build();

            var tcs = new TaskCompletionSource<PlaylistUpdatedDto?>();
            _connection.On<PlaylistUpdatedDto>("ReceivePlaylistUpdated", dto => tcs.TrySetResult(dto));

            await _connection.StartAsync();
            await _connection.InvokeAsync("JoinSession", session.Id.ToString());

            // Call hub mutation method
            await _connection.InvokeAsync("AddItemAsync", session.Id, songId, "Singer1");

            // Verify broadcast received and includes SongId
            var cts = new CancellationTokenSource(TimeSpan.FromSeconds(5));
            var received = await tcs.Task.WaitAsync(cts.Token);
            Assert.NotNull(received);
            Assert.Equal(playlist.id, received!.PlaylistId);
            Assert.Single(received.Items);
            Assert.Equal("Artist1", received.Items[0].Artist);
            Assert.Equal("Title1", received.Items[0].Title);
            Assert.Equal("Singer1", received.Items[0].SingerName);
            Assert.Equal(songId, received.Items[0].SongId);
        }

        [Fact]
        public async Task Hub_AddItemAsync_WithoutToken_ThrowsHubException()
        {
            // Create session and playlist
            var session = await CreateSessionAsync();
            var playlist = await CreatePlaylistAsync(session.Id, session.linkToken);

            // Create a song in the database to get a valid songId
            var songId = await CreateSongAsync(session.Id, "Artist1", "Title1");

            // Connect to hub WITHOUT token
            var baseUrl = _factory.Server.BaseAddress!.ToString().TrimEnd('/');
            _connection = new HubConnectionBuilder()
                .WithUrl(baseUrl + "/hubs/playlist", options =>
                {
                    options.HttpMessageHandlerFactory = _ => _factory.Server.CreateHandler();
                    // No X-Link-Token header
                })
                .Build();

            await _connection.StartAsync();
            await _connection.InvokeAsync("JoinSession", session.Id.ToString());

            // Attempt to call mutation method without token should throw
            var exception = await Assert.ThrowsAsync<HubException>(async () =>
                await _connection.InvokeAsync("AddItemAsync", session.Id, songId, "Singer1"));

            Assert.Contains("Missing X-Link-Token", exception.Message);
        }

        [Fact]
        public async Task Hub_AddItemAsync_WithInvalidToken_ThrowsHubException()
        {
            // Create session and playlist
            var session = await CreateSessionAsync();
            var playlist = await CreatePlaylistAsync(session.Id, session.linkToken);

            // Create a song in the database to get a valid songId
            var songId = await CreateSongAsync(session.Id, "Artist1", "Title1");

            // Connect to hub with INVALID token
            var baseUrl = _factory.Server.BaseAddress!.ToString().TrimEnd('/');
            _connection = new HubConnectionBuilder()
                .WithUrl(baseUrl + "/hubs/playlist", options =>
                {
                    options.HttpMessageHandlerFactory = _ => _factory.Server.CreateHandler();
                    options.Headers.Add("X-Link-Token", "invalid-token-12345");
                })
                .Build();

            await _connection.StartAsync();
            await _connection.InvokeAsync("JoinSession", session.Id.ToString());

            // Attempt to call mutation method with invalid token should throw
            var exception = await Assert.ThrowsAsync<HubException>(async () =>
                await _connection.InvokeAsync("AddItemAsync", session.Id, songId, "Singer1"));

            Assert.Contains("Invalid or expired link token", exception.Message);
        }

        [Fact]
        public async Task Hub_AddItemAsync_WithInvalidSongId_ThrowsHubException()
        {
            // Create session and playlist
            var session = await CreateSessionAsync();
            var playlist = await CreatePlaylistAsync(session.Id, session.linkToken);

            // Connect to hub with valid token
            var baseUrl = _factory.Server.BaseAddress!.ToString().TrimEnd('/');
            _connection = new HubConnectionBuilder()
                .WithUrl(baseUrl + "/hubs/playlist", options =>
                {
                    options.HttpMessageHandlerFactory = _ => _factory.Server.CreateHandler();
                    options.Headers.Add("X-Link-Token", session.linkToken);
                })
                .Build();

            await _connection.StartAsync();
            await _connection.InvokeAsync("JoinSession", session.Id.ToString());

            // Attempt to add item with non-existent songId
            var nonExistentSongId = Guid.NewGuid();
            var exception = await Assert.ThrowsAsync<HubException>(async () =>
                await _connection.InvokeAsync("AddItemAsync", session.Id, nonExistentSongId, "Singer1"));

            Assert.Contains("Song not found in session library", exception.Message);
        }

        [Fact]
        public async Task Hub_RemoveItemAsync_WithValidToken_Succeeds_And_Broadcasts()
        {
            // Create session and playlist, add one item via repository
            var session = await CreateSessionAsync();
            var playlist = await CreatePlaylistAsync(session.Id, session.linkToken);
            var songId = await CreateSongAsync(session.Id, "Artist1", "Title1");
            var itemId = await AddPlaylistItemAsync(session.Id, playlist.id, session.linkToken, songId, "Singer1");

            // Connect to hub with token
            var baseUrl = _factory.Server.BaseAddress!.ToString().TrimEnd('/');
            _connection = new HubConnectionBuilder()
                .WithUrl(baseUrl + "/hubs/playlist", options =>
                {
                    options.HttpMessageHandlerFactory = _ => _factory.Server.CreateHandler();
                    options.Headers.Add("X-Link-Token", session.linkToken);
                })
                .Build();

            var tcs = new TaskCompletionSource<PlaylistUpdatedDto?>();
            _connection.On<PlaylistUpdatedDto>("ReceivePlaylistUpdated", dto => tcs.TrySetResult(dto));

            await _connection.StartAsync();
            await _connection.InvokeAsync("JoinSession", session.Id.ToString());

            // Remove item via hub (no playlistId parameter)
            await _connection.InvokeAsync("RemoveItemAsync", session.Id, itemId);

            // Verify broadcast received and playlist is empty
            var cts = new CancellationTokenSource(TimeSpan.FromSeconds(5));
            var received = await tcs.Task.WaitAsync(cts.Token);
            Assert.NotNull(received);
            Assert.Empty(received!.Items);
        }

        [Fact]
        public async Task Hub_ReorderAsync_WithValidToken_Succeeds_And_Broadcasts()
        {
            // Create session and playlist, add two items
            var session = await CreateSessionAsync();
            var playlist = await CreatePlaylistAsync(session.Id, session.linkToken);
            var songId1 = await CreateSongAsync(session.Id, "Artist1", "Title1");
            var songId2 = await CreateSongAsync(session.Id, "Artist2", "Title2");
            await AddPlaylistItemAsync(session.Id, playlist.id, session.linkToken, songId1, "Singer1");
            await AddPlaylistItemAsync(session.Id, playlist.id, session.linkToken, songId2, "Singer2");

            // Connect to hub with token
            var baseUrl = _factory.Server.BaseAddress!.ToString().TrimEnd('/');
            _connection = new HubConnectionBuilder()
                .WithUrl(baseUrl + "/hubs/playlist", options =>
                {
                    options.HttpMessageHandlerFactory = _ => _factory.Server.CreateHandler();
                    options.Headers.Add("X-Link-Token", session.linkToken);
                })
                .Build();

            var tcs = new TaskCompletionSource<PlaylistUpdatedDto?>();
            _connection.On<PlaylistUpdatedDto>("ReceivePlaylistUpdated", dto => tcs.TrySetResult(dto));

            await _connection.StartAsync();
            await _connection.InvokeAsync("JoinSession", session.Id.ToString());

            // Reorder: move item at position 1 to position 0 (no playlistId parameter)
            await _connection.InvokeAsync("ReorderAsync", session.Id, 1, 0);

            // Verify broadcast received and order changed
            var cts = new CancellationTokenSource(TimeSpan.FromSeconds(5));
            var received = await tcs.Task.WaitAsync(cts.Token);
            Assert.NotNull(received);
            Assert.Equal(2, received!.Items.Count);
            Assert.Equal("Artist2", received.Items[0].Artist); // Second item now first
            Assert.Equal("Artist1", received.Items[1].Artist); // First item now second
            Assert.Equal(0, received.Items[0].Position);
            Assert.Equal(1, received.Items[1].Position);
        }

        [Fact]
        public async Task Hub_ReorderAsync_OnlyReordersActiveItems_PreservesNowPlayingAndCompleted()
        {
            // Arrange: Create session with multiple playlist items in different states
            var session = await CreateSessionAsync();
            var playlist = await CreatePlaylistAsync(session.Id, session.linkToken);
            
            // Create 7 songs
            var songIds = new List<Guid>();
            for (int i = 1; i <= 7; i++)
            {
                songIds.Add(await CreateSongAsync(session.Id, $"Artist{i}", $"Title{i}"));
            }

            // Add items directly to repository and set different statuses to simulate real scenario
            using (var scope = _factory.Services.CreateScope())
            {
                var repo = scope.ServiceProvider.GetRequiredService<IPlaylistRepository>();
                var songRepo = scope.ServiceProvider.GetRequiredService<ISongRepository>();
                var pl = await repo.GetAsync(playlist.id);
                
                // Add items in order:
                // [0] Completed (should be filtered out in broadcast, won't be reordered)
                // [1] NowPlaying (should appear as CurrentSong, won't be in active items)
                // [2-6] Queued/UpNext (these 5 items will be reordered: indices 0-4 in active filtering)
                
                var songs = new List<(Guid songId, string artist, string title, SongStatus status, int position)>
                {
                    (songIds[0], "Artist1", "Title1", SongStatus.Completed, 0),
                    (songIds[1], "Artist2", "Title2", SongStatus.NowPlaying, 1),
                    (songIds[2], "Artist3", "Title3", SongStatus.UpNext, 2),     // Active index 0
                    (songIds[3], "Artist4", "Title4", SongStatus.Queued, 3),     // Active index 1
                    (songIds[4], "Artist5", "Title5", SongStatus.Queued, 4),     // Active index 2
                    (songIds[5], "Artist6", "Title6", SongStatus.Queued, 5),     // Active index 3
                    (songIds[6], "Artist7", "Title7", SongStatus.Queued, 6)      // Active index 4 (will be moved to active index 1)
                };

                foreach (var song in songs)
                {
                    var songDto = await songRepo.GetByIdAsync(session.Id, song.songId);
                    var item = new PlaylistItem
                    {
                        Id = Guid.NewGuid(),
                        PlaylistId = playlist.id,
                        Position = song.position,
                        Artist = song.artist,
                        Title = song.title,
                        SingerName = null,
                        SongId = song.songId,
                        Status = song.status
                    };
                    pl!.Items.Add(item);
                }
                await repo.UpdateAsync(pl!);
            }

            // Connect to hub with token
            var baseUrl = _factory.Server.BaseAddress!.ToString().TrimEnd('/');
            _connection = new HubConnectionBuilder()
                .WithUrl(baseUrl + "/hubs/playlist", options =>
                {
                    options.HttpMessageHandlerFactory = _ => _factory.Server.CreateHandler();
                    options.Headers.Add("X-Link-Token", session.linkToken);
                })
                .Build();

            var tcs = new TaskCompletionSource<PlaylistUpdatedDto?>();
            _connection.On<PlaylistUpdatedDto>("ReceivePlaylistUpdated", dto => tcs.TrySetResult(dto));

            await _connection.StartAsync();
            await _connection.InvokeAsync("JoinSession", session.Id.ToString());

            // Act: Reorder active item at index 4 to index 1 (dragging Artist7 between Artist3 and Artist4)
            // This simulates the user scenario: dragging the 5th active item to position 2
            await _connection.InvokeAsync("ReorderAsync", session.Id, 4, 1);

            // Assert: Verify broadcast received and active items reordered correctly
            var cts = new CancellationTokenSource(TimeSpan.FromSeconds(5));
            var received = await tcs.Task.WaitAsync(cts.Token);
            Assert.NotNull(received);
            
            // Verify CurrentSong is the NowPlaying item (not in Items list)
            Assert.NotNull(received!.CurrentSong);
            Assert.Equal("Artist2", received.CurrentSong!.Artist);
            Assert.Equal("Title2", received.CurrentSong.Title);

            // Verify only active items (Queued/UpNext) are in the Items list (5 items total)
            Assert.Equal(5, received.Items.Count);
            
            // Expected order after reorder (active items only):
            // [0] Artist3 (was active[0], stays at 0)
            // [1] Artist7 (was active[4], moved to 1) <-- MOVED HERE
            // [2] Artist4 (was active[1], shifted to 2)
            // [3] Artist5 (was active[2], shifted to 3)
            // [4] Artist6 (was active[3], shifted to 4)
            Assert.Equal("Artist3", received.Items[0].Artist);
            Assert.Equal("Artist7", received.Items[1].Artist); // Moved item
            Assert.Equal("Artist4", received.Items[2].Artist);
            Assert.Equal("Artist5", received.Items[3].Artist);
            Assert.Equal("Artist6", received.Items[4].Artist);
            
            // Verify positions are sequential
            Assert.Equal(0, received.Items[0].Position);
            Assert.Equal(1, received.Items[1].Position);
            Assert.Equal(2, received.Items[2].Position);
            Assert.Equal(3, received.Items[3].Position);
            Assert.Equal(4, received.Items[4].Position);
        }

        [Fact]
        public async Task Hub_MultipleAdds_BroadcastsCumulativeState()
        {
            // Create session and playlist
            var session = await CreateSessionAsync();
            var playlist = await CreatePlaylistAsync(session.Id, session.linkToken);

            // Create songs in the database
            var songId1 = await CreateSongAsync(session.Id, "Artist1", "Title1");
            var songId2 = await CreateSongAsync(session.Id, "Artist2", "Title2");

            // Connect to hub with token
            var baseUrl = _factory.Server.BaseAddress!.ToString().TrimEnd('/');
            _connection = new HubConnectionBuilder()
                .WithUrl(baseUrl + "/hubs/playlist", options =>
                {
                    options.HttpMessageHandlerFactory = _ => _factory.Server.CreateHandler();
                    options.Headers.Add("X-Link-Token", session.linkToken);
                })
                .Build();

            var receivedBroadcasts = new List<PlaylistUpdatedDto>();
            _connection.On<PlaylistUpdatedDto>("ReceivePlaylistUpdated", dto => receivedBroadcasts.Add(dto));

            await _connection.StartAsync();
            await _connection.InvokeAsync("JoinSession", session.Id.ToString());

            // Add first item
            await _connection.InvokeAsync("AddItemAsync", session.Id, songId1, "Singer1");
            await Task.Delay(500); // Wait for broadcast

            // Add second item
            await _connection.InvokeAsync("AddItemAsync", session.Id, songId2, "Singer2");
            await Task.Delay(500); // Wait for broadcast

            // Verify we got 2 broadcasts with cumulative state
            Assert.Equal(2, receivedBroadcasts.Count);
            Assert.Single(receivedBroadcasts[0].Items); // First broadcast: 1 item
            Assert.Equal(2, receivedBroadcasts[1].Items.Count); // Second broadcast: 2 items total
        }

        // NEW: Role-based permission tests
        [Fact]
        public async Task Hub_ReorderAsync_WithAdminToken_Succeeds()
        {
            var session = await CreateSessionAsync();
            var playlist = await CreatePlaylistAsync(session.Id, session.linkToken);
            var song1 = await CreateSongAsync(session.Id, "A1", "T1");
            var song2 = await CreateSongAsync(session.Id, "A2", "T2");
            var song3 = await CreateSongAsync(session.Id, "A3", "T3");
            
            // Add items using repository
            await AddPlaylistItemAsync(session.Id, playlist.id, session.linkToken, song1, null);
            await AddPlaylistItemAsync(session.Id, playlist.id, session.linkToken, song2, null);
            await AddPlaylistItemAsync(session.Id, playlist.id, session.linkToken, song3, null);

            // Connect with admin token (for now, linkToken acts as admin token)
            var baseUrl = _factory.Server.BaseAddress!.ToString().TrimEnd('/');
            _connection = new HubConnectionBuilder()
                .WithUrl(baseUrl + "/hubs/playlist", options =>
                {
                    options.HttpMessageHandlerFactory = _ => _factory.Server.CreateHandler();
                    options.Headers.Add("X-Link-Token", session.linkToken);
                })
                .Build();

            await _connection.StartAsync();
            await _connection.InvokeAsync("JoinSession", session.Id.ToString());

            // Act - reorder from position 0 to position 2
            await _connection.InvokeAsync("ReorderAsync", session.Id, 0, 2);

            // Assert - no exception means success (admin token allowed)
            // Detailed verification would require fetching the playlist and checking positions
        }

        [Fact(Skip = "Requires dual-token implementation - singer token not yet available")]
        public async Task Hub_ReorderAsync_WithSingerToken_AndAllowSingersToReorderFalse_ThrowsHubException()
        {
            // This test will be enabled once we have AdminToken and SingerToken in CreateResponse
            // and Config.AllowSingersToReorder in Session model
            
            // Arrange: Create session with AllowSingersToReorder = false
            // var session = await CreateSessionAsync(allowSingersToReorder: false);
            // ... create playlist and items
            
            // Connect with SINGER token
            // _connection = new HubConnectionBuilder()
            //     .WithUrl(..., options => { options.Headers.Add("X-Link-Token", session.singerToken); })
            
            // Act & Assert
            // var exception = await Assert.ThrowsAsync<HubException>(async () =>
            //     await _connection.InvokeAsync("ReorderAsync", session.Id, 0, 1));
            // Assert.Contains("admin permissions", exception.Message, StringComparison.OrdinalIgnoreCase);
        }

        [Fact(Skip = "Requires dual-token implementation - singer token not yet available")]
        public async Task Hub_ReorderAsync_WithSingerToken_AndAllowSingersToReorderTrue_Succeeds()
        {
            // Arrange: Create session with AllowSingersToReorder = true
            // var session = await CreateSessionAsync(allowSingersToReorder: true);
            // ... create playlist and items
            
            // Connect with SINGER token
            // _connection = new HubConnectionBuilder()
            //     .WithUrl(..., options => { options.Headers.Add("X-Link-Token", session.singerToken); })
            
            // Act
            // await _connection.InvokeAsync("ReorderAsync", session.Id, 0, 1);
            
            // Assert - no exception (singer token + AllowSingersToReorder=true allows reorder)
        }

        [Fact(Skip = "Requires dual-token implementation - singer token not yet available")]
        public async Task Hub_AddItemAsync_WithSingerToken_Succeeds()
        {
            // Arrange
            // var session = await CreateSessionAsync();
            // var playlist = await CreatePlaylistAsync(session.Id, session.singerToken);
            // var song = await CreateSongAsync(session.Id, "Artist", "Title");
            
            // Connect with SINGER token
            // _connection = new HubConnectionBuilder()
            //     .WithUrl(..., options => { options.Headers.Add("X-Link-Token", session.singerToken); })
            
            // Act
            // await _connection.InvokeAsync("AddItemAsync", session.Id, song, "Singer1");
            
            // Assert - singers can always add songs
        }

        [Fact(Skip = "Requires dual-token implementation - singer token not yet available")]
        public async Task Hub_ClearQueueAsync_WithSingerToken_ThrowsHubException()
        {
            // Arrange
            // var session = await CreateSessionAsync();
            // var playlist = await CreatePlaylistAsync(session.Id, session.singerToken);
            
            // Connect with SINGER token
            // _connection = new HubConnectionBuilder()
            //     .WithUrl(..., options => { options.Headers.Add("X-Link-Token", session.singerToken); })
            
            // Act & Assert
            // var exception = await Assert.ThrowsAsync<HubException>(async () =>
            //     await _connection.InvokeAsync("ClearQueueAsync", session.Id));
            // Assert.Contains("admin", exception.Message, StringComparison.OrdinalIgnoreCase);
        }

        [Fact(Skip = "Requires dual-token implementation - singer token not yet available")]
        public async Task Hub_RemoveItemAsync_WithSingerToken_AndAllowSingersToReorderTrue_Succeeds()
        {
            // Arrange: Session with AllowSingersToReorder = true
            // var session = await CreateSessionAsync(allowSingersToReorder: true);
            // var playlist = await CreatePlaylistAsync(session.Id, session.singerToken);
            // var song = await CreateSongAsync(session.Id, "A", "T");
            // var itemId = await AddPlaylistItemAsync(session.Id, playlist.id, session.singerToken, song, null);
            
            // Connect with SINGER token
            // _connection = new HubConnectionBuilder()
            //     .WithUrl(..., options => { options.Headers.Add("X-Link-Token", session.singerToken); })
            
            // Act
            // await _connection.InvokeAsync("RemoveItemAsync", session.Id, itemId);
            
            // Assert - no exception (AllowSingersToReorder=true allows removal)
        }

        [Fact]
        public async Task Hub_SetStopAfterCurrentAsync_SetsPlaybackModeAndBroadcasts()
        {
            // Arrange
            var session = await CreateSessionAsync();
            var playlist = await CreatePlaylistAsync(session.Id, session.linkToken);

            // Connect to hub with token
            var baseUrl = _factory.Server.BaseAddress!.ToString().TrimEnd('/');
            _connection = new HubConnectionBuilder()
                .WithUrl(baseUrl + "/hubs/playlist", options =>
                {
                    options.HttpMessageHandlerFactory = _ => _factory.Server.CreateHandler();
                    options.Headers.Add("X-Link-Token", session.linkToken);
                })
                .Build();

            var tcs = new TaskCompletionSource<PlaylistUpdatedDto?>();
            _connection.On<PlaylistUpdatedDto>("ReceivePlaylistUpdated", dto => tcs.TrySetResult(dto));

            await _connection.StartAsync();
            await _connection.InvokeAsync("JoinSession", session.Id.ToString());

            // Act - call SetStopAfterCurrentAsync
            await _connection.InvokeAsync("SetStopAfterCurrentAsync", session.Id);

            // Assert - verify broadcast received with PlaybackMode = StopAfterCurrent (1)
            var cts = new CancellationTokenSource(TimeSpan.FromSeconds(5));
            var received = await tcs.Task.WaitAsync(cts.Token);
            Assert.NotNull(received);
            Assert.Equal(1, received!.PlaybackMode); // StopAfterCurrent = 1
        }

        [Fact]
        public async Task Hub_AdvanceToNextSongAsync_WithStopAfterCurrent_TransitionsToStopped()
        {
            // Arrange
            var session = await CreateSessionAsync();
            var playlist = await CreatePlaylistAsync(session.Id, session.linkToken);
            
            // Add songs to queue
            var song1Id = await CreateSongAsync(session.Id, "Artist1", "Title1");
            var song2Id = await CreateSongAsync(session.Id, "Artist2", "Title2");
            var item1Id = await AddPlaylistItemAsync(session.Id, playlist.id, session.linkToken, song1Id, "Singer1");
            var item2Id = await AddPlaylistItemAsync(session.Id, playlist.id, session.linkToken, song2Id, "Singer2");

            // Set first item as NowPlaying
            using (var scope = _factory.Services.CreateScope())
            {
                var repo = scope.ServiceProvider.GetRequiredService<IPlaylistRepository>();
                var pl = await repo.GetAsync(playlist.id);
                var item = pl!.Items.First(i => i.Id == item1Id);
                item.Status = Models.SongStatus.NowPlaying;
                await repo.UpdateAsync(pl);
            }

            // Connect to hub
            var baseUrl = _factory.Server.BaseAddress!.ToString().TrimEnd('/');
            _connection = new HubConnectionBuilder()
                .WithUrl(baseUrl + "/hubs/playlist", options =>
                {
                    options.HttpMessageHandlerFactory = _ => _factory.Server.CreateHandler();
                    options.Headers.Add("X-Link-Token", session.linkToken);
                })
                .Build();

            var broadcasts = new List<PlaylistUpdatedDto>();
            _connection.On<PlaylistUpdatedDto>("ReceivePlaylistUpdated", dto => broadcasts.Add(dto));

            await _connection.StartAsync();
            await _connection.InvokeAsync("JoinSession", session.Id.ToString());

            // Set StopAfterCurrent mode
            await _connection.InvokeAsync("SetStopAfterCurrentAsync", session.Id);
            await Task.Delay(100); // Allow broadcast

            // Act - advance to next song
            await _connection.InvokeAsync("AdvanceToNextSongAsync", session.Id);
            await Task.Delay(100); // Allow broadcast

            // Assert - verify PlaybackMode transitioned to Stopped (2) and no new song is playing
            var latestBroadcast = broadcasts.Last();
            Assert.Equal(2, latestBroadcast.PlaybackMode); // Stopped = 2
            Assert.Null(latestBroadcast.CurrentSong); // No song playing
        }

        [Fact]
        public async Task Hub_ProceedPlaybackAsync_AdvancesToNextSongAndSetsNormalMode()
        {
            // Arrange
            var session = await CreateSessionAsync();
            var playlist = await CreatePlaylistAsync(session.Id, session.linkToken);
            
            // Add songs to queue
            var song1Id = await CreateSongAsync(session.Id, "Artist1", "Title1");
            var song2Id = await CreateSongAsync(session.Id, "Artist2", "Title2");
            await AddPlaylistItemAsync(session.Id, playlist.id, session.linkToken, song1Id, "Singer1");
            await AddPlaylistItemAsync(session.Id, playlist.id, session.linkToken, song2Id, "Singer2");

            // Set session to Stopped mode
            using (var scope = _factory.Services.CreateScope())
            {
                var sessionRepo = scope.ServiceProvider.GetRequiredService<ISessionRepository>();
                var sess = await sessionRepo.GetByIdAsync(session.Id);
                sess!.Config.PlaybackMode = Models.PlaybackMode.Stopped;
                await sessionRepo.UpdateAsync(sess);
            }

            // Connect to hub
            var baseUrl = _factory.Server.BaseAddress!.ToString().TrimEnd('/');
            _connection = new HubConnectionBuilder()
                .WithUrl(baseUrl + "/hubs/playlist", options =>
                {
                    options.HttpMessageHandlerFactory = _ => _factory.Server.CreateHandler();
                    options.Headers.Add("X-Link-Token", session.linkToken);
                })
                .Build();

            var tcs = new TaskCompletionSource<PlaylistUpdatedDto?>();
            _connection.On<PlaylistUpdatedDto>("ReceivePlaylistUpdated", dto => tcs.TrySetResult(dto));

            await _connection.StartAsync();
            await _connection.InvokeAsync("JoinSession", session.Id.ToString());

            // Act - proceed playback
            await _connection.InvokeAsync("ProceedPlaybackAsync", session.Id);

            // Assert - verify broadcast with Normal mode (0) and a current song
            var cts = new CancellationTokenSource(TimeSpan.FromSeconds(5));
            var received = await tcs.Task.WaitAsync(cts.Token);
            Assert.NotNull(received);
            Assert.Equal(0, received!.PlaybackMode); // Normal = 0
            Assert.NotNull(received.CurrentSong); // Song is now playing
            Assert.Equal("Artist1", received.CurrentSong!.Artist);
        }

        [Fact]
        public async Task Hub_PlaylistBroadcast_IncludesPlaybackMode()
        {
            // Arrange
            var session = await CreateSessionAsync();
            var playlist = await CreatePlaylistAsync(session.Id, session.linkToken);
            var songId = await CreateSongAsync(session.Id, "TestArtist", "TestTitle");

            // Connect to hub
            var baseUrl = _factory.Server.BaseAddress!.ToString().TrimEnd('/');
            _connection = new HubConnectionBuilder()
                .WithUrl(baseUrl + "/hubs/playlist", options =>
                {
                    options.HttpMessageHandlerFactory = _ => _factory.Server.CreateHandler();
                    options.Headers.Add("X-Link-Token", session.linkToken);
                })
                .Build();

            var tcs = new TaskCompletionSource<PlaylistUpdatedDto?>();
            _connection.On<PlaylistUpdatedDto>("ReceivePlaylistUpdated", dto => tcs.TrySetResult(dto));

            await _connection.StartAsync();
            await _connection.InvokeAsync("JoinSession", session.Id.ToString());

            // Act - trigger any mutation that broadcasts
            await _connection.InvokeAsync("AddItemAsync", session.Id, songId, "TestSinger");

            // Assert - verify broadcast includes PlaybackMode (defaults to Normal = 0)
            var cts = new CancellationTokenSource(TimeSpan.FromSeconds(5));
            var received = await tcs.Task.WaitAsync(cts.Token);
            Assert.NotNull(received);
            Assert.Equal(0, received!.PlaybackMode); // Normal = 0 (default)
        }

        // Helper methods
        private async Task<CreateResponse> CreateSessionAsync()
        {
            var createReq = new { RequireSingerName = false, PauseBetweenSongsSeconds = 1, AllowSingersToReorder = false };
            var resp = await _client.PostAsJsonAsync("/api/sessions", createReq);
            resp.EnsureSuccessStatusCode();
            var created = await resp.Content.ReadFromJsonAsync<CreateResponse>();
            Assert.NotNull(created);
            return created!;
        }

        private async Task<PlaylistDto> CreatePlaylistAsync(Guid sessionId, string token)
        {
            // Create playlist directly in the test database via repository (controller removed in product)
            // CRITICAL: playlistId MUST equal sessionId (one-to-one relationship)
            using var scope = _factory.Services.CreateScope();
            var repo = scope.ServiceProvider.GetRequiredService<IPlaylistRepository>();
            var playlist = new Playlist { Id = sessionId, SessionId = sessionId };
            await repo.AddAsync(playlist);
            return new PlaylistDto(playlist.Id, playlist.SessionId);
        }

        private async Task<Guid> AddPlaylistItemAsync(Guid sessionId, Guid playlistId, string token, Guid songId, string? singerName)
        {
            // Add item directly using repository (simulates an external actor adding an item)
            using var scope = _factory.Services.CreateScope();
            var repo = scope.ServiceProvider.GetRequiredService<IPlaylistRepository>();
            var songRepo = scope.ServiceProvider.GetRequiredService<ISongRepository>();
            var playlist = await repo.GetAsync(playlistId);
            Assert.NotNull(playlist);
            
            // Get song details (GetByIdAsync returns SongListItemDto)
            var songDto = await songRepo.GetByIdAsync(sessionId, songId);
            Assert.NotNull(songDto);
            
            var item = new PlaylistItem
            {
                Id = Guid.NewGuid(),
                PlaylistId = playlistId,
                Position = playlist!.Items.Count,
                Artist = songDto!.Artist,
                Title = songDto.Title,
                SingerName = singerName,
                SongId = songId
            };
            playlist.Items.Add(item);
            await repo.UpdateAsync(playlist!);
            return item.Id;
        }

        private async Task<Guid> CreateSongAsync(Guid sessionId, string artist, string title)
        {
            // Create song in database to get a valid songId using BulkUpsertAsync
            using var scope = _factory.Services.CreateScope();
            var songRepo = scope.ServiceProvider.GetRequiredService<ISongRepository>();
            var songId = Guid.NewGuid();
            var songDto = new Karamel.Backend.Controllers.SongUploadDto(songId, artist, title, null);
            await songRepo.BulkUpsertAsync(sessionId, new[] { songDto });
            return songId;
        }

        public async ValueTask DisposeAsync()
        {
            if (_connection != null)
            {
                await _connection.DisposeAsync();
            }
            _client.Dispose();
        }

        private record CreateResponse(Guid Id, string linkToken);
        private record PlaylistDto(Guid id, Guid sessionId);
        private record PlaylistItemDto(Guid Id, string Artist, string Title, string? SingerName, int Position, Guid? SongId, int Status);
        private record PlaylistUpdatedDto(Guid PlaylistId, Guid SessionId, List<PlaylistItemDto> Items, PlaylistItemDto? CurrentSong, int PlaybackMode);
    }
}
