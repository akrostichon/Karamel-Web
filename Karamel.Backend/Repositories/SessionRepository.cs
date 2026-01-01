using Microsoft.EntityFrameworkCore;
using Karamel.Backend.Data;
using Karamel.Backend.Models;

namespace Karamel.Backend.Repositories
{
    public class SessionRepository : EfRepository<Session>, ISessionRepository
    {
        public SessionRepository(BackendDbContext db) : base(db) { }

        public async Task<Session?> GetByLinkTokenAsync(string token)
        {
            return await _db.Sessions.FirstOrDefaultAsync(s => s.LinkToken == token);
        }
    }
}
