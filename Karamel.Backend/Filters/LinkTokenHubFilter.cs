using Microsoft.AspNetCore.SignalR;
using Karamel.Backend.Services;
using Karamel.Backend.Repositories;

namespace Karamel.Backend.Filters
{
    /// <summary>
    /// Hub filter that validates link tokens for session-based authorization.
    /// Extracts X-Link-Token from HTTP headers during connection and validates
    /// against the sessionId parameter in hub method invocations.
    /// Enforces role-based permissions (admin vs singer tokens).
    /// </summary>
    public class LinkTokenHubFilter : IHubFilter
    {
        private readonly ITokenService _tokenService;
        private readonly ISessionRepository _sessionRepo;
        private readonly ILogger<LinkTokenHubFilter> _logger;

        public LinkTokenHubFilter(ITokenService tokenService, ISessionRepository sessionRepo, ILogger<LinkTokenHubFilter> logger)
        {
            _tokenService = tokenService;
            _sessionRepo = sessionRepo;
            _logger = logger;
        }

        public async ValueTask<object?> InvokeMethodAsync(
            HubInvocationContext invocationContext,
            Func<HubInvocationContext, ValueTask<object?>> next)
        {
            // Skip validation for JoinSession and LeaveSession (public methods)
            if (invocationContext.HubMethodName == "JoinSession" ||
                invocationContext.HubMethodName == "LeaveSession")
            {
                return await next(invocationContext);
            }

            // For mutation methods, validate token from connection context
            var token = invocationContext.Context.Items.TryGetValue("X-Link-Token", out var tokenObj) 
                ? tokenObj?.ToString() 
                : null;

            if (string.IsNullOrEmpty(token))
            {
                _logger.LogWarning("Missing X-Link-Token for hub method {MethodName} from connection {ConnectionId}", 
                    invocationContext.HubMethodName, invocationContext.Context.ConnectionId);
                throw new HubException("Missing X-Link-Token header");
            }

            // Extract sessionId from first parameter (convention for all mutation methods)
            if (invocationContext.HubMethodArguments.Count == 0 ||
                invocationContext.HubMethodArguments[0] is not Guid sessionId)
            {
                _logger.LogWarning("Invalid method signature for hub method {MethodName}: sessionId required as first parameter", 
                    invocationContext.HubMethodName);
                throw new HubException("Invalid method signature: sessionId required as first parameter");
            }

            // Validate token and extract role
            var (tokenSessionId, role, isValid) = _tokenService.ValidateLinkToken(token);

            if (!isValid || tokenSessionId != sessionId)
            {
                _logger.LogWarning("Invalid link token for session {SessionId} in hub method {MethodName} from connection {ConnectionId}", 
                    sessionId, invocationContext.HubMethodName, invocationContext.Context.ConnectionId);
                throw new HubException("Invalid or expired link token");
            }

            // Define admin-only operations
            var adminOnlyMethods = new[] 
            { 
                "ClearQueueAsync", 
                "SetSongStatusAsync", 
                "CompleteCurrentSongAsync", 
                "AdvanceToNextSongAsync" 
            };

            // Check if operation is admin-only
            if (adminOnlyMethods.Contains(invocationContext.HubMethodName) && role != "admin")
            {
                _logger.LogWarning("Singer token attempted admin-only operation {MethodName} in session {SessionId}", 
                    invocationContext.HubMethodName, sessionId);
                throw new HubException("This operation requires admin permissions");
            }

            // Conditional operations: admin OR singer + AllowSingersToReorder=true
            var conditionalAdminMethods = new[] { "ReorderAsync", "RemoveItemAsync" };
            if (conditionalAdminMethods.Contains(invocationContext.HubMethodName) && role != "admin")
            {
                // Singer token: Check if AllowSingersToReorder is enabled
                var session = await _sessionRepo.GetByIdAsync(sessionId);
                if (session == null || !session.Config.AllowSingersToReorder)
                {
                    _logger.LogWarning("Singer token attempted {MethodName} but AllowSingersToReorder=false in session {SessionId}", 
                        invocationContext.HubMethodName, sessionId);
                    throw new HubException("Singer reordering/removal is not allowed for this session");
                }
            }

            // Token validated and permissions checked, proceed with method invocation
            return await next(invocationContext);
        }
    }
}
