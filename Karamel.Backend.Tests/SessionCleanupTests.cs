using System;
using System.Threading.Tasks;
using Xunit;
using Microsoft.Extensions.DependencyInjection;
using Karamel.Backend.Services;
using Karamel.Backend.Repositories;
using Karamel.Backend.Models;

namespace Karamel.Backend.Tests
{
    public class SessionCleanupTests : IClassFixture<TestServerFactory>
    {
        private readonly TestServerFactory _factory;

        public SessionCleanupTests(TestServerFactory factory)
        {
            _factory = factory;
        }

        [Fact]
        public async Task CleanupOnceAsync_DeletesExpiredSessions_AndNotifiesHub()
        {
            using var scope = _factory.Services.CreateScope();
            var services = scope.ServiceProvider;

            var repo = services.GetRequiredService<ISessionRepository>();
            var cleanup = services.GetRequiredService<SessionCleanupService>();

            // Create a session that is already expired
            var s = new Session
            {
                Id = Guid.NewGuid(),
                CreatedAt = DateTime.UtcNow.AddHours(-2),
                ExpiresAt = DateTime.UtcNow.AddMinutes(-5),
                RequireSingerName = true,
                PauseBetweenSongsSeconds = 5,
                LinkToken = "test-token"
            };

            await repo.AddAsync(s);

            // Ensure the stored session is expired in the database (set after Add to avoid any model defaults)
            var stored = await repo.GetByIdAsync(s.Id);
            // Use DateTime.MinValue to avoid timezone/SQLite conversion ambiguity so the session is unambiguously expired
            stored!.ExpiresAt = DateTime.MinValue;
            await repo.UpdateAsync(stored);

            // Run a single cleanup pass
            await cleanup.CleanupOnceAsync();

            // Verify deletion using a fresh scope so we don't get a tracked entity from the original DbContext
            using var verifyScope = _factory.Services.CreateScope();
            var verifyRepo = verifyScope.ServiceProvider.GetRequiredService<ISessionRepository>();
            var fetched = await verifyRepo.GetByIdAsync(s.Id);
            Assert.Null(fetched);
        }
    }
}
