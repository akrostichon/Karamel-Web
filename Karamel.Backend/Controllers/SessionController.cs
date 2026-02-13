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
        private readonly ILogger<SessionsController> _logger;

        public SessionsController(ISessionRepository repo, ITokenService tokenService, ILogger<SessionsController> logger)
        {
            _repo = repo;
            _tokenService = tokenService;
            _logger = logger;
        }

        [HttpPost]
        public async Task<IActionResult> Create([FromBody] CreateSessionRequest req)
        {
            try
            {
                var session = new Session
                {
                    Id = Guid.NewGuid(),
                    CreatedAt = DateTime.UtcNow,
                    ExpiresAt = DateTime.UtcNow.AddMinutes(30), // Guaranteed 30-minute TTL
                    Config = new SessionConfig
                    {
                        RequireSingerName = req.RequireSingerName,
                        PauseBetweenSongsSeconds = req.PauseBetweenSongsSeconds,
                        AllowSingersToReorder = req.AllowSingersToReorder
                    }
                };

                // Generate dual tokens
                session.AdminToken = _tokenService.GenerateLinkToken(session.Id, "admin");
                session.SingerToken = _tokenService.GenerateLinkToken(session.Id, "singer");
                session.LinkToken = session.AdminToken; // Backward compat

                await _repo.AddAsync(session);

                _logger.LogInformation("Created new session {SessionId} with RequireSingerName={RequireSingerName}, AllowSingersToReorder={AllowSingersToReorder}", 
                    session.Id, req.RequireSingerName, req.AllowSingersToReorder);

                // Return flattened config (Option B - no frontend changes needed)
                return CreatedAtAction(nameof(Get), new { id = session.Id }, new 
                { 
                    session.Id, 
                    adminToken = session.AdminToken,
                    singerToken = session.SingerToken,
                    linkToken = session.AdminToken, // Deprecated
                    expiresAt = session.ExpiresAt, // 30-minute TTL guarantee
                    requireSingerName = session.Config.RequireSingerName,
                    pauseBetweenSongsSeconds = session.Config.PauseBetweenSongsSeconds,
                    allowSingersToReorder = session.Config.AllowSingersToReorder
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error creating session");
                return StatusCode(500, "Failed to create session");
            }
        }

        [HttpGet("{id:guid}")]
        public async Task<IActionResult> Get(Guid id)
        {
            var s = await _repo.GetByIdAsync(id);
            if (s == null) return NotFound();
            
            // Return flattened config (Option B)
            return Ok(new
            {
                s.Id,
                requireSingerName = s.Config.RequireSingerName,
                pauseBetweenSongsSeconds = s.Config.PauseBetweenSongsSeconds,
                allowSingersToReorder = s.Config.AllowSingersToReorder
            });
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

    public record CreateSessionRequest(bool RequireSingerName, int PauseBetweenSongsSeconds, bool AllowSingersToReorder);
    public record HeartbeatRequest(int ExtendMinutes);
    public record EndSessionRequest(bool Force);
}
