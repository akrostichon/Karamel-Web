using System;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
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
            var repo = new SessionRepository(db, NullLogger<SessionRepository>.Instance);

            var session = new Session 
            { 
                Id = Guid.NewGuid(), 
                LinkToken = "token123", 
                CreatedAt = DateTime.UtcNow,
                Config = new SessionConfig()
            };
            await repo.AddAsync(session);

            var fetched = await repo.GetByIdAsync(session.Id);
            Assert.NotNull(fetched);
            Assert.Equal("token123", fetched!.LinkToken);
        }

        [Fact]
        public async Task Get_By_LinkToken_Returns_Session()
        {
            using var db = CreateInMemoryContext();
            var repo = new SessionRepository(db, NullLogger<SessionRepository>.Instance);

            var session = new Session 
            { 
                Id = Guid.NewGuid(), 
                LinkToken = "link-abc", 
                CreatedAt = DateTime.UtcNow,
                Config = new SessionConfig()
            };
            await repo.AddAsync(session);

            var fetched = await repo.GetByLinkTokenAsync("link-abc");
            Assert.NotNull(fetched);
            Assert.Equal(session.Id, fetched!.Id);
        }

        // NEW: SessionConfig JSON persistence tests
        [Fact]
        public async Task AddAsync_WithSessionConfig_PersistsJson()
        {
            using var db = CreateInMemoryContext();
            var repo = new SessionRepository(db, NullLogger<SessionRepository>.Instance);

            var session = new Session
            {
                Id = Guid.NewGuid(),
                LinkToken = "token123",
                CreatedAt = DateTime.UtcNow,
                Config = new SessionConfig
                {
                    AllowSingersToReorder = true,
                    RequireSingerName = true,
                    PauseBetweenSongsSeconds = 10
                }
            };

            await repo.AddAsync(session);

            var fetched = await repo.GetByIdAsync(session.Id);
            Assert.NotNull(fetched);
            Assert.NotNull(fetched!.Config);
            Assert.True(fetched.Config.AllowSingersToReorder);
            Assert.True(fetched.Config.RequireSingerName);
            Assert.Equal(10, fetched.Config.PauseBetweenSongsSeconds);
        }

        [Fact]
        public async Task GetByIdAsync_WithExistingSession_DeserializesConfigCorrectly()
        {
            using var db = CreateInMemoryContext();
            var repo = new SessionRepository(db, NullLogger<SessionRepository>.Instance);

            var session = new Session
            {
                Id = Guid.NewGuid(),
                LinkToken = "token456",
                CreatedAt = DateTime.UtcNow,
                Config = new SessionConfig
                {
                    RequireSingerName = true,
                    PauseBetweenSongsSeconds = 5,
                    AllowSingersToReorder = false
                }
            };

            await repo.AddAsync(session);
            var fetched = await repo.GetByIdAsync(session.Id);

            Assert.NotNull(fetched);
            Assert.NotNull(fetched!.Config);
            Assert.True(fetched.Config.RequireSingerName);
            Assert.Equal(5, fetched.Config.PauseBetweenSongsSeconds);
            Assert.False(fetched.Config.AllowSingersToReorder);
        }

        [Fact]
        public async Task AddAsync_WithDefaultConfig_PersistsCorrectly()
        {
            using var db = CreateInMemoryContext();
            var repo = new SessionRepository(db, NullLogger<SessionRepository>.Instance);

            var session = new Session
            {
                Id = Guid.NewGuid(),
                LinkToken = "token789",
                CreatedAt = DateTime.UtcNow
                // Config will use default values
            };

            await repo.AddAsync(session);
            var fetched = await repo.GetByIdAsync(session.Id);

            Assert.NotNull(fetched);
            Assert.NotNull(fetched!.Config);
            Assert.False(fetched.Config.RequireSingerName); // default
            Assert.Equal(5, fetched.Config.PauseBetweenSongsSeconds); // default
            Assert.False(fetched.Config.AllowSingersToReorder); // default
        }
    }
}
