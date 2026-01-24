using Microsoft.EntityFrameworkCore;
using Karamel.Backend.Data;
using Karamel.Backend.Models;

namespace Karamel.Backend.Repositories
{
    public class SessionRepository : EfRepository<Session>, ISessionRepository
    {
        private readonly ILogger<SessionRepository> _logger;

        public SessionRepository(BackendDbContext db, ILogger<SessionRepository> logger) : base(db)
        {
            _logger = logger;
        }

        public override async Task AddAsync(Session entity)
        {
            try
            {
                _logger.LogInformation("Adding session {SessionId} to database", entity.Id);
                await base.AddAsync(entity);
                _logger.LogInformation("Successfully added session {SessionId}", entity.Id);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to add session {SessionId} to database", entity.Id);
                throw;
            }
        }

        public async Task<Session?> GetByLinkTokenAsync(string token)
        {
            return await _db.Sessions.FirstOrDefaultAsync(s => s.LinkToken == token);
        }
    }
}
