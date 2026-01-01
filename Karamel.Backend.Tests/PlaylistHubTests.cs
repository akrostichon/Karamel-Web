using System;
using System.Collections.Generic;
using System.Net.Http;
using System.Net.Http.Json;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.AspNetCore.SignalR;
using Microsoft.AspNetCore.SignalR.Client;
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
            var createReq = new { RequireSingerName = false, PauseBetweenSongsSeconds = 1 };
            var resp = await _client.PostAsJsonAsync("/api/sessions", createReq);
            resp.EnsureSuccessStatusCode();
            var created = await resp.Content.ReadFromJsonAsync<CreateResponse>();
            Assert.NotNull(created);

            // create playlist
            var createPlaylistResp = await _client.PostAsync($"/api/playlists/{created.Id}", null);
            createPlaylistResp.EnsureSuccessStatusCode();
            var playlist = await createPlaylistResp.Content.ReadFromJsonAsync<PlaylistDto>();
            Assert.NotNull(playlist);

            // start a SignalR client and join the session group
            var baseUrl = _factory.Server.BaseAddress!.ToString().TrimEnd('/');
            _connection = new HubConnectionBuilder()
                .WithUrl(baseUrl + "/hubs/playlist", options => { options.HttpMessageHandlerFactory = _ => _factory.Server.CreateHandler(); })
                .Build();

            var tcs = new TaskCompletionSource<PlaylistUpdatedDto?>();
            _connection.On<PlaylistUpdatedDto>("ReceivePlaylistUpdated", dto => tcs.TrySetResult(dto));

            await _connection.StartAsync();
            await _connection.InvokeAsync("JoinSession", created.Id.ToString());

            // Add an item with token header
            var addItem = new { Artist = "X", Title = "Y", SingerName = "Z" };
            var request = new HttpRequestMessage(HttpMethod.Post, $"/api/playlists/{created.Id}/{playlist!.id}/items");
            request.Headers.Add("X-Link-Token", created.linkToken);
            request.Content = JsonContent.Create(addItem);
            var addResp = await _client.SendAsync(request);
            addResp.EnsureSuccessStatusCode();

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
            await _connection.InvokeAsync("AddItemAsync", session.Id, playlist.id, "Artist1", "Title1", "Singer1");

            // Verify broadcast received
            var cts = new CancellationTokenSource(TimeSpan.FromSeconds(5));
            var received = await tcs.Task.WaitAsync(cts.Token);
            Assert.NotNull(received);
            Assert.Equal(playlist.id, received!.PlaylistId);
            Assert.Single(received.Items);
            Assert.Equal("Artist1", received.Items[0].Artist);
            Assert.Equal("Title1", received.Items[0].Title);
            Assert.Equal("Singer1", received.Items[0].SingerName);
        }

        [Fact]
        public async Task Hub_AddItemAsync_WithoutToken_ThrowsHubException()
        {
            // Create session and playlist
            var session = await CreateSessionAsync();
            var playlist = await CreatePlaylistAsync(session.Id, session.linkToken);

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
                await _connection.InvokeAsync("AddItemAsync", session.Id, playlist.id, "Artist1", "Title1", "Singer1"));

            Assert.Contains("Missing X-Link-Token", exception.Message);
        }

        [Fact]
        public async Task Hub_AddItemAsync_WithInvalidToken_ThrowsHubException()
        {
            // Create session and playlist
            var session = await CreateSessionAsync();
            var playlist = await CreatePlaylistAsync(session.Id, session.linkToken);

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
                await _connection.InvokeAsync("AddItemAsync", session.Id, playlist.id, "Artist1", "Title1", "Singer1"));

            Assert.Contains("Invalid or expired link token", exception.Message);
        }

        [Fact]
        public async Task Hub_RemoveItemAsync_WithValidToken_Succeeds_And_Broadcasts()
        {
            // Create session and playlist, add one item via REST
            var session = await CreateSessionAsync();
            var playlist = await CreatePlaylistAsync(session.Id, session.linkToken);
            var itemId = await AddItemViaRestAsync(session.Id, playlist.id, session.linkToken, "Artist1", "Title1", "Singer1");

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

            // Remove item via hub
            await _connection.InvokeAsync("RemoveItemAsync", session.Id, playlist.id, itemId);

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
            await AddItemViaRestAsync(session.Id, playlist.id, session.linkToken, "Artist1", "Title1", "Singer1");
            await AddItemViaRestAsync(session.Id, playlist.id, session.linkToken, "Artist2", "Title2", "Singer2");

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

            // Reorder: move item at position 1 to position 0
            await _connection.InvokeAsync("ReorderAsync", session.Id, playlist.id, 1, 0);

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
            await _connection.InvokeAsync("AddItemAsync", session.Id, playlist.id, "Artist1", "Title1", "Singer1");
            await Task.Delay(500); // Wait for broadcast

            // Add second item
            await _connection.InvokeAsync("AddItemAsync", session.Id, playlist.id, "Artist2", "Title2", "Singer2");
            await Task.Delay(500); // Wait for broadcast

            // Verify we got 2 broadcasts with cumulative state
            Assert.Equal(2, receivedBroadcasts.Count);
            Assert.Single(receivedBroadcasts[0].Items); // First broadcast: 1 item
            Assert.Equal(2, receivedBroadcasts[1].Items.Count); // Second broadcast: 2 items total
        }

        // Helper methods
        private async Task<CreateResponse> CreateSessionAsync()
        {
            var createReq = new { RequireSingerName = false, PauseBetweenSongsSeconds = 1 };
            var resp = await _client.PostAsJsonAsync("/api/sessions", createReq);
            resp.EnsureSuccessStatusCode();
            var created = await resp.Content.ReadFromJsonAsync<CreateResponse>();
            Assert.NotNull(created);
            return created!;
        }

        private async Task<PlaylistDto> CreatePlaylistAsync(Guid sessionId, string token)
        {
            var createPlaylistResp = await _client.PostAsync($"/api/playlists/{sessionId}", null);
            createPlaylistResp.EnsureSuccessStatusCode();
            var playlist = await createPlaylistResp.Content.ReadFromJsonAsync<PlaylistDto>();
            Assert.NotNull(playlist);
            return playlist!;
        }

        private async Task<Guid> AddItemViaRestAsync(Guid sessionId, Guid playlistId, string token, string artist, string title, string? singerName)
        {
            var addItem = new { Artist = artist, Title = title, SingerName = singerName };
            var request = new HttpRequestMessage(HttpMethod.Post, $"/api/playlists/{sessionId}/{playlistId}/items");
            request.Headers.Add("X-Link-Token", token);
            request.Content = JsonContent.Create(addItem);
            var addResp = await _client.SendAsync(request);
            addResp.EnsureSuccessStatusCode();
            var item = await addResp.Content.ReadFromJsonAsync<PlaylistItemDto>();
            Assert.NotNull(item);
            return item!.Id;
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
        private record PlaylistItemDto(Guid Id, string Artist, string Title, string? SingerName, int Position);
        private record PlaylistUpdatedDto(Guid PlaylistId, Guid SessionId, List<PlaylistItemDto> Items);
    }
}
