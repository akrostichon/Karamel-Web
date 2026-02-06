using System;
using System.Collections.Generic;
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
        private record PlaylistItemDto(Guid Id, string Artist, string Title, string? SingerName, int Position, Guid? SongId);
        private record PlaylistUpdatedDto(Guid PlaylistId, Guid SessionId, List<PlaylistItemDto> Items);
    }
}
