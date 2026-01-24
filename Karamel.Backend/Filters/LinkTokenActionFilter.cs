using Microsoft.AspNetCore.Mvc.Filters;
using Microsoft.AspNetCore.Mvc;
using Karamel.Backend.Services;
using System;

namespace Karamel.Backend.Filters
{
    [AttributeUsage(AttributeTargets.Class | AttributeTargets.Method)]
    public sealed class LinkTokenAttribute : Attribute, IAsyncActionFilter
    {
        public async System.Threading.Tasks.Task OnActionExecutionAsync(ActionExecutingContext context, ActionExecutionDelegate next)
        {
            var tokenService = context.HttpContext.RequestServices.GetService(typeof(ITokenService)) as ITokenService;
            var logger = context.HttpContext.RequestServices.GetService(typeof(ILogger<LinkTokenAttribute>)) as ILogger<LinkTokenAttribute>;
            
            if (tokenService == null)
            {
                logger?.LogError("TokenService not available in DI container");
                context.Result = new StatusCodeResult(500);
                return;
            }

            // Extract token from header
            var token = context.HttpContext.Request.Headers["X-Link-Token"].FirstOrDefault();
            if (string.IsNullOrEmpty(token))
            {
                logger?.LogWarning("Missing X-Link-Token header for {Path}", context.HttpContext.Request.Path);
                context.Result = new UnauthorizedObjectResult(new { error = "Missing X-Link-Token header" });
                return;
            }

            // Extract sessionId route value
            if (!context.RouteData.Values.TryGetValue("sessionId", out var sidObj) || sidObj == null || !Guid.TryParse(sidObj.ToString(), out var sessionId))
            {
                logger?.LogWarning("Invalid or missing sessionId route parameter for {Path}", context.HttpContext.Request.Path);
                context.Result = new BadRequestObjectResult(new { error = "Invalid or missing sessionId route parameter" });
                return;
            }

            if (!tokenService.ValidateLinkToken(sessionId, token))
            {
                logger?.LogWarning("Invalid link token for session {SessionId} from {RemoteIP}", 
                    sessionId, context.HttpContext.Connection.RemoteIpAddress);
                context.Result = new UnauthorizedObjectResult(new { error = "Invalid or expired link token" });
                return;
            }

            await next();
        }
    }
}
