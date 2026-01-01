using Karamel.Backend.Models;

namespace Karamel.Backend.Repositories
{
    public interface ISessionRepository : IRepository<Session>
    {
        Task<Session?> GetByLinkTokenAsync(string token);
    }
}
