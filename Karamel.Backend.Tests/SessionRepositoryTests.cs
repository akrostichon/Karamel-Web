using System;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using Xunit;
using Karamel.Backend.Data;
using Karamel.Backend.Models;
using Karamel.Backend.Repositories;

namespace Karamel.Backend.Tests
{
    public class SessionRepositoryTests
    {
        private BackendDbContext CreateInMemoryContext(string? dbName = null)
        {
            var options = new DbContextOptionsBuilder<BackendDbContext>()
                .UseInMemoryDatabase(dbName ?? Guid.NewGuid().ToString())
                .Options;
            return new BackendDbContext(options);
        }

        [Fact]
        public async Task Add_And_Get_Session_By_Id()
        {
            using var db = CreateInMemoryContext();
            var repo = new SessionRepository(db);

            var session = new Session { Id = Guid.NewGuid(), LinkToken = "token123", CreatedAt = DateTime.UtcNow };
            await repo.AddAsync(session);

            var fetched = await repo.GetByIdAsync(session.Id);
            Assert.NotNull(fetched);
            Assert.Equal("token123", fetched!.LinkToken);
        }

        [Fact]
        public async Task Get_By_LinkToken_Returns_Session()
        {
            using var db = CreateInMemoryContext();
            var repo = new SessionRepository(db);

            var session = new Session { Id = Guid.NewGuid(), LinkToken = "link-abc", CreatedAt = DateTime.UtcNow };
            await repo.AddAsync(session);

            var fetched = await repo.GetByLinkTokenAsync("link-abc");
            Assert.NotNull(fetched);
            Assert.Equal(session.Id, fetched!.Id);
        }
    }
}
