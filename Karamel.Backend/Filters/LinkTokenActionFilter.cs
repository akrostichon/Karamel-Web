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
            if (tokenService == null)
            {
                context.Result = new StatusCodeResult(500);
                return;
            }

            // Extract token from header
            var token = context.HttpContext.Request.Headers["X-Link-Token"].FirstOrDefault();
            if (string.IsNullOrEmpty(token))
            {
                context.Result = new UnauthorizedObjectResult(new { error = "Missing X-Link-Token header" });
                return;
            }

            // Extract sessionId route value
            if (!context.RouteData.Values.TryGetValue("sessionId", out var sidObj) || sidObj == null || !Guid.TryParse(sidObj.ToString(), out var sessionId))
            {
                context.Result = new BadRequestObjectResult(new { error = "Invalid or missing sessionId route parameter" });
                return;
            }

            if (!tokenService.ValidateLinkToken(sessionId, token))
            {
                context.Result = new UnauthorizedObjectResult(new { error = "Invalid or expired link token" });
                return;
            }

            await next();
        }
    }
}
