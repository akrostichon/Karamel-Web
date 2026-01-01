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

        
    }

    
}
