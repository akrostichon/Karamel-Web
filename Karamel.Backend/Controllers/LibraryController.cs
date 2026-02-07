using Microsoft.AspNetCore.Mvc;
using Karamel.Backend.Repositories;

namespace Karamel.Backend.Controllers
{
    [ApiController]
    [Route("api/sessions/{sessionId:guid}/[controller]")]
    [Filters.LinkToken]
    public class LibraryController : ControllerBase
    {
        private readonly ISongRepository _songRepo;
        private readonly ILogger<LibraryController> _logger;

        public LibraryController(ISongRepository songRepo, ILogger<LibraryController> logger)
        {
            _songRepo = songRepo;
            _logger = logger;
        }

        // Bulk upload sanitized library entries for a session
        [HttpPost("bulk")]
        public async Task<IActionResult> BulkUpsert(Guid sessionId, [FromBody] IEnumerable<SongUploadDto> songs)
        {
            try
            {
                if (songs == null)
                {
                    _logger.LogWarning("BulkUpsert called with null payload for session {SessionId}", sessionId);
                    return BadRequest("Payload missing");
                }

                // Security: Ensure payload does not contain forbidden fields
                // Since model binding already mapped allowed fields, we defensively inspect raw JSON is not available here.
                // Basic validation: reject if count is excessive
                var list = songs.ToList();
                if (list.Count > 5000)
                {
                    _logger.LogWarning("BulkUpsert rejected: Too many songs ({Count}) for session {SessionId}", list.Count, sessionId);
                    return BadRequest("Too many songs in single upload");
                }

                _logger.LogInformation("Starting bulk upsert of {Count} songs for session {SessionId}", list.Count, sessionId);
                await _songRepo.BulkUpsertAsync(sessionId, list);
                _logger.LogInformation("Successfully completed bulk upsert for session {SessionId}", sessionId);
                
                return Accepted();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error during bulk upsert for session {SessionId}", sessionId);
                return StatusCode(500, "Failed to upload library");
            }
        }

        // GET paginated library
        [HttpGet]
        public async Task<IActionResult> GetPage(Guid sessionId, [FromQuery] int page = 1, [FromQuery] int pageSize = 50, [FromQuery] string? search = null, [FromQuery] string? sort = null)
        {
            var result = await _songRepo.GetPageAsync(sessionId, page, pageSize, search, sort);
            // Add X-Total-Count header
            Response.Headers["X-Total-Count"] = result.TotalCount.ToString();
            return Ok(result.Items);
        }

        [HttpGet("{songId:guid}")]
        public async Task<IActionResult> GetById(Guid sessionId, Guid songId)
        {
            var item = await _songRepo.GetByIdAsync(sessionId, songId);
            if (item == null) return NotFound();
            return Ok(item);
        }
    }
}
