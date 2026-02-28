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
        private const string AdminRole = "admin";
        private const string TokenHeaderKey = "X-Link-Token";
        
        private static readonly HashSet<string> PublicMethods = new(StringComparer.Ordinal)
        {
            "JoinSession",
            "LeaveSession"
        };

        private static readonly HashSet<string> AdminOnlyMethods = new(StringComparer.Ordinal)
        {
            "ClearQueueAsync",
            "SetSongStatusAsync",
            "CompleteCurrentSongAsync",
            "AdvanceToNextSongAsync",
            "PauseSessionAsync",
            "ResumeSessionAsync",
            "UpdateSessionConfigAsync"
        };

        private static readonly HashSet<string> ConditionalAdminMethods = new(StringComparer.Ordinal)
        {
            "ReorderAsync",
            "RemoveItemAsync"
        };

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
            if (IsPublicMethod(invocationContext.HubMethodName))
            {
                return await next(invocationContext);
            }

            var sessionId = ExtractSessionId(invocationContext);
            var token = ExtractToken(invocationContext);
            var role = ValidateTokenAndExtractRole(token, sessionId, invocationContext);
            
            await ValidatePermissionsAsync(invocationContext.HubMethodName, role, sessionId);

            return await next(invocationContext);
        }

        private static bool IsPublicMethod(string methodName) => PublicMethods.Contains(methodName);

        private Guid ExtractSessionId(HubInvocationContext invocationContext)
        {
            if (invocationContext.HubMethodArguments.Count == 0 ||
                invocationContext.HubMethodArguments[0] is not Guid sessionId)
            {
                _logger.LogWarning("Invalid method signature for hub method {MethodName}: sessionId required as first parameter", 
                    invocationContext.HubMethodName);
                throw new HubException("Invalid method signature: sessionId required as first parameter");
            }

            return sessionId;
        }

        private string ExtractToken(HubInvocationContext invocationContext)
        {
            var token = invocationContext.Context.Items.TryGetValue(TokenHeaderKey, out var tokenObj) 
                ? tokenObj?.ToString() 
                : null;

            if (string.IsNullOrEmpty(token))
            {
                _logger.LogWarning("Missing {TokenHeader} for hub method {MethodName} from connection {ConnectionId}", 
                    TokenHeaderKey, invocationContext.HubMethodName, invocationContext.Context.ConnectionId);
                throw new HubException($"Missing {TokenHeaderKey} header");
            }

            return token;
        }

        private string ValidateTokenAndExtractRole(string token, Guid sessionId, HubInvocationContext invocationContext)
        {
            var (tokenSessionId, role, isValid) = _tokenService.ValidateLinkToken(token);

            if (!isValid || tokenSessionId != sessionId)
            {
                _logger.LogWarning("Invalid link token for session {SessionId} in hub method {MethodName} from connection {ConnectionId}", 
                    sessionId, invocationContext.HubMethodName, invocationContext.Context.ConnectionId);
                throw new HubException("Invalid or expired link token");
            }

            return role;
        }

        private async Task ValidatePermissionsAsync(string methodName, string role, Guid sessionId)
        {
            if (IsAdminRole(role))
            {
                return; // Admin has full access
            }

            if (AdminOnlyMethods.Contains(methodName))
            {
                _logger.LogWarning("Singer token attempted admin-only operation {MethodName} in session {SessionId}", 
                    methodName, sessionId);
                throw new HubException("This operation requires admin permissions");
            }

            if (ConditionalAdminMethods.Contains(methodName))
            {
                await ValidateConditionalPermissionAsync(methodName, sessionId);
            }
        }

        private async Task ValidateConditionalPermissionAsync(string methodName, Guid sessionId)
        {
            var session = await _sessionRepo.GetByIdAsync(sessionId);
            
            if (session == null || !session.Config.AllowSingersToReorder)
            {
                _logger.LogWarning("Singer token attempted {MethodName} but AllowSingersToReorder=false in session {SessionId}", 
                    methodName, sessionId);
                throw new HubException("Singer reordering/removal is not allowed for this session");
            }
        }

        private static bool IsAdminRole(string role) => role == AdminRole;
    }
}
