using System;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using Karamel.Backend.Repositories;
using Karamel.Backend.Models;
using Karamel.Backend.Services;

namespace Karamel.Backend.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class PlaylistsController : ControllerBase
    {
        private readonly ISessionRepository _sessionRepo;
        private readonly IPlaylistRepository _playlistRepo;
        private readonly ITokenService _tokenService;
        private readonly Microsoft.AspNetCore.SignalR.IHubContext<Karamel.Backend.Hubs.PlaylistHub> _hubContext;

        public PlaylistsController(ISessionRepository sessionRepo, IPlaylistRepository playlistRepo, ITokenService tokenService,
            Microsoft.AspNetCore.SignalR.IHubContext<Karamel.Backend.Hubs.PlaylistHub> hubContext)
        {
            _sessionRepo = sessionRepo;
            _playlistRepo = playlistRepo;
            _tokenService = tokenService;
            _hubContext = hubContext;
        }

        private bool ValidateToken(Guid sessionId, out IActionResult? failure)
        {
            failure = null;
            var token = Request.Headers["X-Link-Token"].FirstOrDefault() ?? Request.Query["linkToken"].FirstOrDefault();
            if (string.IsNullOrEmpty(token))
            {
                failure = Unauthorized("Missing link token");
                return false;
            }
            if (!_tokenService.ValidateLinkToken(sessionId, token))
            {
                failure = Forbid();
                return false;
            }
            return true;
        }

        [HttpPost("{sessionId:guid}")]
        public async Task<IActionResult> Create(Guid sessionId)
        {
            var s = await _sessionRepo.GetByIdAsync(sessionId);
            if (s == null) return NotFound();
            // create playlist for session
            var playlist = new Playlist { Id = Guid.NewGuid(), SessionId = sessionId };
            await _playlistRepo.AddAsync(playlist);
            return CreatedAtAction(nameof(Get), new { sessionId = sessionId, id = playlist.Id }, playlist);
        }

        [HttpGet("{sessionId:guid}/{id:guid}")]
        public async Task<IActionResult> Get(Guid sessionId, Guid id)
        {
            var p = await _playlistRepo.GetAsync(id);
            if (p == null || p.SessionId != sessionId) return NotFound();
            return Ok(p);
        }

        [HttpPost("{sessionId:guid}/{id:guid}/items")]
        public async Task<IActionResult> AddItem(Guid sessionId, Guid id, [FromBody] AddPlaylistItemRequest req)
        {
            if (!ValidateToken(sessionId, out var fail)) return fail!;
            var playlist = await _playlistRepo.GetAsync(id);
            if (playlist == null) return NotFound();
            var item = new PlaylistItem { Id = Guid.NewGuid(), PlaylistId = playlist.Id, Position = playlist.Items.Count, Artist = req.Artist, Title = req.Title, SingerName = req.SingerName };
            playlist.Items.Add(item);
            await _playlistRepo.UpdateAsync(playlist);
            // Broadcast the updated playlist to connected clients in the session group
            var groupName = Karamel.Backend.Hubs.PlaylistHub.GetSessionGroupName(sessionId.ToString());
            var dto = new PlaylistUpdatedDto(playlist.Id, playlist.SessionId, playlist.Items.Select(i => new PlaylistItemDto(i.Id, i.Artist, i.Title, i.SingerName, i.Position)).ToList());
            await _hubContext.Clients.Group(groupName).SendCoreAsync("ReceivePlaylistUpdated", new object[] { dto });
            return CreatedAtAction(nameof(Get), new { sessionId = sessionId, id = playlist.Id }, item);
        }

        [HttpDelete("{sessionId:guid}/{id:guid}/items/{itemId:guid}")]
        public async Task<IActionResult> RemoveItem(Guid sessionId, Guid id, Guid itemId)
        {
            if (!ValidateToken(sessionId, out var fail)) return fail!;
            var playlist = await _playlistRepo.GetAsync(id);
            if (playlist == null) return NotFound();
            var item = playlist.Items.FirstOrDefault(i => i.Id == itemId);
            if (item == null) return NotFound();
            playlist.Items.Remove(item);
            // reindex positions
            for (int i = 0; i < playlist.Items.Count; i++) playlist.Items[i].Position = i;
            await _playlistRepo.UpdateAsync(playlist);
            var groupName = Karamel.Backend.Hubs.PlaylistHub.GetSessionGroupName(sessionId.ToString());
            var dto = new PlaylistUpdatedDto(playlist.Id, playlist.SessionId, playlist.Items.Select(i => new PlaylistItemDto(i.Id, i.Artist, i.Title, i.SingerName, i.Position)).ToList());
            await _hubContext.Clients.Group(groupName).SendCoreAsync("ReceivePlaylistUpdated", new object[] { dto });
            return Ok();
        }

        [HttpPost("{sessionId:guid}/{id:guid}/reorder")]
        public async Task<IActionResult> Reorder(Guid sessionId, Guid id, [FromBody] ReorderRequest req)
        {
            if (!ValidateToken(sessionId, out var fail)) return fail!;
            var playlist = await _playlistRepo.GetAsync(id);
            if (playlist == null) return NotFound();
            if (req.From < 0 || req.From >= playlist.Items.Count || req.To < 0 || req.To >= playlist.Items.Count)
            {
                return BadRequest("Invalid indices");
            }
            var item = playlist.Items[req.From];
            playlist.Items.RemoveAt(req.From);
            playlist.Items.Insert(req.To, item);
            for (int i = 0; i < playlist.Items.Count; i++) playlist.Items[i].Position = i;
            await _playlistRepo.UpdateAsync(playlist);
            var groupName = Karamel.Backend.Hubs.PlaylistHub.GetSessionGroupName(sessionId.ToString());
            var dto = new PlaylistUpdatedDto(playlist.Id, playlist.SessionId, playlist.Items.Select(i => new PlaylistItemDto(i.Id, i.Artist, i.Title, i.SingerName, i.Position)).ToList());
            await _hubContext.Clients.Group(groupName).SendCoreAsync("ReceivePlaylistUpdated", new object[] { dto });
            return Ok(playlist);
        }
    }

    public record AddPlaylistItemRequest(string Artist, string Title, string? SingerName);
    public record ReorderRequest(int From, int To);

    // DTOs for hub payloads
    public record PlaylistItemDto(Guid Id, string Artist, string Title, string? SingerName, int Position);
    public record PlaylistUpdatedDto(Guid PlaylistId, Guid SessionId, System.Collections.Generic.List<PlaylistItemDto> Items);
}
