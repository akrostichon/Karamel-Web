using System;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using Karamel.Backend.Repositories;
using Karamel.Backend.Models;
using Karamel.Backend.Services;

namespace Karamel.Backend.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class SessionsController : ControllerBase
    {
        private readonly ISessionRepository _repo;
        private readonly ITokenService _tokenService;

        public SessionsController(ISessionRepository repo, ITokenService tokenService)
        {
            _repo = repo;
            _tokenService = tokenService;
        }

        [HttpPost]
        public async Task<IActionResult> Create([FromBody] CreateSessionRequest req)
        {
            var session = new Session
            {
                Id = Guid.NewGuid(),
                CreatedAt = DateTime.UtcNow,
                RequireSingerName = req.RequireSingerName,
                PauseBetweenSongsSeconds = req.PauseBetweenSongsSeconds
            };

            session.LinkToken = _tokenService.GenerateLinkToken(session.Id);

            await _repo.AddAsync(session);

            return CreatedAtAction(nameof(Get), new { id = session.Id }, new { session.Id, linkToken = session.LinkToken });
        }

        [HttpGet("{id:guid}")]
        public async Task<IActionResult> Get(Guid id)
        {
            var s = await _repo.GetByIdAsync(id);
            if (s == null) return NotFound();
            return Ok(s);
        }

        [HttpPost("{id:guid}/heartbeat")]
        public async Task<IActionResult> Heartbeat(Guid id, [FromBody] HeartbeatRequest req)
        {
            var s = await _repo.GetByIdAsync(id);
            if (s == null) return NotFound();
            s.ExpiresAt = DateTime.UtcNow.AddMinutes(req.ExtendMinutes);
            await _repo.UpdateAsync(s);
            return Ok();
        }

        [HttpPost("{id:guid}/end")]
        public async Task<IActionResult> End(Guid id, [FromBody] EndSessionRequest req)
        {
            var s = await _repo.GetByIdAsync(id);
            if (s == null) return NotFound();
            await _repo.DeleteAsync(id);
            return Ok();
        }
    }

    public record CreateSessionRequest(bool RequireSingerName, int PauseBetweenSongsSeconds);
    public record HeartbeatRequest(int ExtendMinutes);
    public record EndSessionRequest(bool Force);
}
