using Microsoft.AspNetCore.SignalR;
using Karamel.Backend.Services;

namespace Karamel.Backend.Filters
{
    /// <summary>
    /// Hub filter that validates link tokens for session-based authorization.
    /// Extracts X-Link-Token from HTTP headers during connection and validates
    /// against the sessionId parameter in hub method invocations.
    /// </summary>
    public class LinkTokenHubFilter : IHubFilter
    {
        private readonly ITokenService _tokenService;
        private readonly ILogger<LinkTokenHubFilter> _logger;

        public LinkTokenHubFilter(ITokenService tokenService, ILogger<LinkTokenHubFilter> logger)
        {
            _tokenService = tokenService;
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

            if (!_tokenService.ValidateLinkToken(sessionId, token))
            {
                _logger.LogWarning("Invalid link token for session {SessionId} in hub method {MethodName} from connection {ConnectionId}", 
                    sessionId, invocationContext.HubMethodName, invocationContext.Context.ConnectionId);
                throw new HubException("Invalid or expired link token");
            }

            // Token validated, proceed with method invocation
            return await next(invocationContext);
        }
    }
}
