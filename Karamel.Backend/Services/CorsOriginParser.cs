using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Hosting;

namespace Karamel.Backend.Services;

public static class CorsOriginParser
{
    /// <summary>
    /// Parses and normalizes the configured CORS origins for the current environment.
    /// </summary>
    public static string[] Parse(IConfiguration configuration, IHostEnvironment environment)
    {
        var configuredOrigins = configuration
            .GetSection("Cors:AllowedOrigins")
            .Get<string[]>()
            ?? Array.Empty<string>();

        var origins = configuredOrigins
            .Where(origin => !string.IsNullOrWhiteSpace(origin))
            .Select(origin => origin.Trim().TrimEnd('/'))
            .Where(origin => !string.IsNullOrEmpty(origin))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToArray();

        if (environment.IsEnvironment("Testing"))
        {
            return origins.Length > 0 ? origins : new[] { "http://localhost" };
        }

        if (environment.IsProduction() && origins.Length == 0)
        {
            throw new InvalidOperationException("Cors:AllowedOrigins must contain at least one origin in Production");
        }

        return origins;
    }
}
