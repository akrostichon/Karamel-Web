using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using Karamel.Backend.Repositories;
using Karamel.Backend.Models;
using Karamel.Backend.Services;
using static Karamel.Backend.Models.SessionConstants;

namespace Karamel.Backend.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class SessionsController : ControllerBase
    {
        private readonly ISessionRepository _repo;
        private readonly ITokenService _tokenService;
        private readonly ILogger<SessionsController> _logger;
        private readonly IConfiguration _configuration;

        public SessionsController(ISessionRepository repo, ITokenService tokenService, ILogger<SessionsController> logger, IConfiguration configuration)
        {
            _repo = repo;
            _tokenService = tokenService;
            _logger = logger;
            _configuration = configuration;
        }

        [HttpPost]
        [EnableRateLimiting("session-create")]
        public async Task<IActionResult> Create([FromBody] CreateSessionRequest req)
        {
            try
            {
                var maxActiveSessions = _configuration.GetValue("Karamel:MaxActiveSessions", 100);
                var activeSessionCount = (await _repo.ListAsync()).Count();
                if (activeSessionCount >= maxActiveSessions)
                {
                    Response.Headers["Retry-After"] = "60";
                    return StatusCode(StatusCodes.Status503ServiceUnavailable, "Server is busy, try again later");
                }

                var session = new Session
                {
                    Id = Guid.NewGuid(),
                    CreatedAt = DateTime.UtcNow,
                    ExpiresAt = DateTime.UtcNow.AddMinutes(DefaultTtlMinutes),
                    Config = new SessionConfig
                    {
                        RequireSingerName = req.RequireSingerName,
                        PauseBetweenSongsSeconds = req.PauseBetweenSongsSeconds,
                        AllowSingersToReorder = req.AllowSingersToReorder,
                        Theme = req.Theme
                    }
                };

                // Generate dual tokens
                session.AdminToken = _tokenService.GenerateAdminToken(session.Id);
                session.SingerToken = _tokenService.GenerateSingerToken(session.Id);

                await _repo.AddAsync(session);

                _logger.LogInformation("Created new session {SessionId} with AdminToken and SingerToken", session.Id);

                // Return flattened config (Option B - no frontend changes needed)
                return CreatedAtAction(nameof(Get), new { id = session.Id }, new 
                { 
                    session.Id, 
                    adminToken = session.AdminToken,
                    singerToken = session.SingerToken,
                    expiresAt = session.ExpiresAt,
                    requireSingerName = session.Config.RequireSingerName,
                    pauseBetweenSongsSeconds = session.Config.PauseBetweenSongsSeconds,
                    allowSingersToReorder = session.Config.AllowSingersToReorder,
                    theme = session.Config.Theme
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error creating session");
                return StatusCode(500, "Failed to create session");
            }
        }

        [HttpPost("{id:guid}/restore")]
        [EnableRateLimiting("session-create")]
        public async Task<IActionResult> Restore(Guid id, [FromBody] CreateSessionRequest req)
        {
            var token = Request.Headers["X-Link-Token"].FirstOrDefault();
            if (string.IsNullOrEmpty(token))
            {
                return Unauthorized("Missing X-Link-Token header");
            }

            var (role, isValid) = _tokenService.ValidateToken(token, id);
            if (!isValid || role != "admin")
            {
                return Unauthorized("Invalid or expired token");
            }

            try
            {
                var existingSession = await _repo.GetByIdAsync(id);
                if (existingSession is not null)
                {
                    existingSession.ExpiresAt = DateTime.UtcNow.AddMinutes(DefaultTtlMinutes);
                    await _repo.UpdateAsync(existingSession);

                    _logger.LogInformation("Restored session {SessionId}", id);

                    return Ok(new
                    {
                        existingSession.Id,
                        adminToken = existingSession.AdminToken,
                        singerToken = existingSession.SingerToken,
                        expiresAt = existingSession.ExpiresAt,
                        requireSingerName = existingSession.Config.RequireSingerName,
                        pauseBetweenSongsSeconds = existingSession.Config.PauseBetweenSongsSeconds,
                        allowSingersToReorder = existingSession.Config.AllowSingersToReorder,
                        theme = existingSession.Config.Theme,
                        restored = false
                    });
                }

                var maxActiveSessions = _configuration.GetValue("Karamel:MaxActiveSessions", 100);
                var activeSessionCount = (await _repo.ListAsync()).Count();
                if (activeSessionCount >= maxActiveSessions)
                {
                    Response.Headers["Retry-After"] = "60";
                    return StatusCode(StatusCodes.Status503ServiceUnavailable, "Server is busy, try again later");
                }

                // Tokens do not expire, so an old admin link can recreate an empty session for its ID. This is accepted because it grants no access beyond that ID and session creation is rate-limited and capped.
                var session = new Session
                {
                    Id = id,
                    AdminToken = token,
                    SingerToken = _tokenService.GenerateSingerToken(id),
                    CreatedAt = DateTime.UtcNow,
                    ExpiresAt = DateTime.UtcNow.AddMinutes(DefaultTtlMinutes),
                    Config = new SessionConfig
                    {
                        RequireSingerName = req.RequireSingerName,
                        PauseBetweenSongsSeconds = req.PauseBetweenSongsSeconds,
                        AllowSingersToReorder = req.AllowSingersToReorder,
                        Theme = req.Theme
                    }
                };

                await _repo.AddAsync(session);
                _logger.LogInformation("Restored session {SessionId}", id);

                return CreatedAtAction(nameof(Get), new { id }, new
                {
                    session.Id,
                    adminToken = session.AdminToken,
                    singerToken = session.SingerToken,
                    expiresAt = session.ExpiresAt,
                    requireSingerName = session.Config.RequireSingerName,
                    pauseBetweenSongsSeconds = session.Config.PauseBetweenSongsSeconds,
                    allowSingersToReorder = session.Config.AllowSingersToReorder,
                    theme = session.Config.Theme,
                    restored = true
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error restoring session {SessionId}", id);
                return StatusCode(500, "Failed to restore session");
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
                allowSingersToReorder = s.Config.AllowSingersToReorder,
                theme = s.Config.Theme
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

    public record CreateSessionRequest(bool RequireSingerName, int PauseBetweenSongsSeconds, bool AllowSingersToReorder, string? Theme);
    public record HeartbeatRequest(int ExtendMinutes);
    public record EndSessionRequest(bool Force);
}
