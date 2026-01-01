using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Karamel.Backend.Repositories;
using Karamel.Backend.Hubs;
using Microsoft.AspNetCore.SignalR;

namespace Karamel.Backend.Services
{
    /// <summary>
    /// Background service that periodically removes expired sessions and notifies connected clients.
    /// The core cleanup logic is exposed via <see cref="CleanupOnceAsync"/> to allow deterministic testing.
    /// </summary>
    public class SessionCleanupService : BackgroundService
    {
        private readonly IServiceProvider _services;
        private readonly ILogger<SessionCleanupService> _logger;
        private readonly TimeSpan _interval;

        public SessionCleanupService(IServiceProvider services, ILogger<SessionCleanupService> logger)
        {
            _services = services;
            _logger = logger;
            // Default run interval: 1 minute
            _interval = TimeSpan.FromMinutes(1);
        }

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            _logger.LogInformation("SessionCleanupService started");
            while (!stoppingToken.IsCancellationRequested)
            {
                try
                {
                    await CleanupOnceAsync(stoppingToken);
                }
                catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
                {
                    // graceful shutdown
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Error while running session cleanup");
                }

                await Task.Delay(_interval, stoppingToken);
            }
            _logger.LogInformation("SessionCleanupService stopping");
        }

        /// <summary>
        /// Performs one cleanup pass. This method is public to allow unit/integration tests to invoke cleanup deterministically.
        /// It finds sessions with ExpiresAt <= UtcNow and deletes them from repository. For each deleted session it broadcasts
        /// a "ReceiveSessionEnded" message to the SignalR group so clients can handle termination gracefully.
        /// </summary>
        public async Task CleanupOnceAsync(CancellationToken cancellationToken = default)
        {
            using var scope = _services.CreateScope();
            var repo = scope.ServiceProvider.GetRequiredService<Karamel.Backend.Repositories.ISessionRepository>();
            var hubContext = scope.ServiceProvider.GetService<IHubContext<Karamel.Backend.Hubs.PlaylistHub>>();

            var now = DateTime.UtcNow;
            var sessions = await repo.ListAsync();
            var expired = sessions.Where(s => s.ExpiresAt.HasValue && s.ExpiresAt.Value <= now).ToList();

            foreach (var s in expired)
            {
                try
                {
                    _logger.LogInformation("Expiring session {SessionId}", s.Id);
                    await repo.DeleteAsync(s.Id);

                    if (hubContext != null)
                    {
                        var group = Karamel.Backend.Hubs.PlaylistHub.GetSessionGroupName(s.Id.ToString());
                        await hubContext.Clients.Group(group).SendAsync("ReceiveSessionEnded", s.Id, cancellationToken);
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Failed to expire session {SessionId}", s.Id);
                }
            }
        }
    }
}
