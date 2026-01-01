using System;
using System.Collections.Generic;
using System.Net.Http;
using System.Net.Http.Json;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.AspNetCore.SignalR.Client;
using Xunit;

namespace Karamel.Backend.Tests
{
    public class PlaylistHubTests : IClassFixture<TestServerFactory>, IDisposable
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

        public void Dispose()
        {
            _connection?.DisposeAsync().AsTask().GetAwaiter().GetResult();
            _client.Dispose();
        }

        private record CreateResponse(Guid Id, string linkToken);
        private record PlaylistDto(Guid id, Guid sessionId);
        private record PlaylistItemDto(Guid Id, string Artist, string Title, string? SingerName, int Position);
        private record PlaylistUpdatedDto(Guid PlaylistId, Guid SessionId, List<PlaylistItemDto> Items);
    }
}
