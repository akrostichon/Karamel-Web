using Microsoft.EntityFrameworkCore;
using Microsoft.AspNetCore.SignalR;
using Microsoft.AspNetCore.HttpOverrides;
using Karamel.Backend.Services;

var builder = WebApplication.CreateBuilder(args);
var connectionString = builder.Configuration.GetConnectionString("DefaultConnection") ?? builder.Configuration["DefaultConnection"];
builder.Services.AddDbContext<Karamel.Backend.Data.BackendDbContext>(options =>
    options.UseSqlite(string.IsNullOrWhiteSpace(connectionString) ? "Data Source=karamel.db" : connectionString));

// Register repositories
builder.Services.AddScoped<Karamel.Backend.Repositories.ISessionRepository, Karamel.Backend.Repositories.SessionRepository>();
builder.Services.AddScoped<Karamel.Backend.Repositories.IPlaylistRepository, Karamel.Backend.Repositories.PlaylistRepository>();
builder.Services.AddScoped<Karamel.Backend.Repositories.ISongRepository, Karamel.Backend.Repositories.EfSongRepository>();
builder.Services.AddSingleton<Karamel.Backend.Services.IFuzzySearchService, Karamel.Backend.Services.FuzzySearchService>();
// Register TokenService with secret from configuration (fallback for dev)
// Priority: Karamel:TokenSecret -> KARAMEL-TOKEN-SECRET environment var -> TokenSecret
var tokenSecret = builder.Configuration["Karamel:TokenSecret"]
                  ?? Environment.GetEnvironmentVariable("KARAMEL-TOKEN-SECRET")
                  ?? builder.Configuration["TokenSecret"]
                  ?? Environment.GetEnvironmentVariable("TOKEN_SECRET");

if (string.IsNullOrWhiteSpace(tokenSecret))
{
    if (!(builder.Environment.IsDevelopment() || builder.Environment.IsEnvironment("Testing")))
    {
        throw new InvalidOperationException("KARAMEL-TOKEN-SECRET (Karamel:TokenSecret) must be provided in non-development environments");
    }
    tokenSecret = "dev-secret-change-me";
}

if (tokenSecret.Length < 32)
{
    if (!(builder.Environment.IsDevelopment() || builder.Environment.IsEnvironment("Testing")))
    {
        throw new InvalidOperationException("KARAMEL-TOKEN-SECRET must be at least 32 characters long in non-development environments");
    }
}

builder.Services.AddSingleton<Karamel.Backend.Services.ITokenService>(_ => new Karamel.Backend.Services.TokenService(tokenSecret));
// Add SignalR
builder.Services.AddSignalR(options =>
{
    options.EnableDetailedErrors = builder.Environment.IsDevelopment();
    // Increase timeouts to handle Azure App Service cold starts and network latency
    options.ClientTimeoutInterval = TimeSpan.FromSeconds(60); // Default: 30s
    options.KeepAliveInterval = TimeSpan.FromSeconds(15);      // Default: 15s (send ping every 15s)
    options.HandshakeTimeout = TimeSpan.FromSeconds(30);       // Default: 15s
});

builder.WebHost.ConfigureKestrel(o => o.Limits.MaxRequestBodySize = 1_048_576);

// Allow cross-origin requests from the frontend during local development so
// the browser can POST/OPTIONS to the API when frontend is served from a
// different origin/port. This policy is permissive for localhost dev only.
builder.Services.AddCors(options =>
{
    options.AddPolicy("LocalDevCors", policy =>
    {
        policy.WithOrigins("http://localhost:5245", "https://localhost:5245")
              .AllowAnyHeader()
              .AllowAnyMethod()
              .AllowCredentials()
              .WithExposedHeaders("X-Total-Count")
              .SetIsOriginAllowedToAllowWildcardSubdomains();
    });
    
    // Add a more permissive policy for troubleshooting
    options.AddPolicy("DevCorsPermissive", policy =>
    {
        policy.SetIsOriginAllowed(origin =>
        {
            var uri = new Uri(origin);
            return uri.Host == "localhost" || uri.Host == "127.0.0.1";
        })
        .AllowAnyHeader()
        .AllowAnyMethod()
        .AllowCredentials()
        .WithExposedHeaders("X-Total-Count");
    });
    
    // Production CORS policy for public deployments.
    options.AddPolicy("ProductionCors", policy =>
    {
        var allowedOrigins = CorsOriginParser.Parse(builder.Configuration, builder.Environment);
        policy.WithOrigins(allowedOrigins)
              .AllowAnyHeader()
              .AllowAnyMethod()
              .AllowCredentials()
              .WithExposedHeaders("X-Total-Count");
    });
});
// Register controllers for API endpoints
builder.Services.AddControllers();

// Register Swagger services BEFORE building the app
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

// Register the session cleanup background service and the concrete instance so tests can resolve it
builder.Services.AddSingleton<Karamel.Backend.Services.SessionCleanupService>();
builder.Services.AddHostedService(sp => sp.GetRequiredService<Karamel.Backend.Services.SessionCleanupService>());

builder.Services.Configure<ForwardedHeadersOptions>(options =>
{
    // Render terminates TLS at the edge proxy, so we trust only the immediate proxy and
    // do not accept arbitrary forwarded headers from other network hops.
    options.ForwardedHeaders = ForwardedHeaders.XForwardedFor | ForwardedHeaders.XForwardedProto;
    options.ForwardLimit = 1;
    options.KnownIPNetworks.Clear();
    options.KnownProxies.Clear();
});

var app = builder.Build();

app.UseForwardedHeaders();
app.Use(async (context, next) =>
{
    context.Response.Headers["X-Content-Type-Options"] = "nosniff";
    context.Response.Headers["Referrer-Policy"] = "no-referrer";
    await next();
});

// Data is ephemeral (sessions expire after 30 minutes), so the SQLite schema is always created fresh from the model.
if (!app.Environment.IsEnvironment("Testing"))
{
    using var scope = app.Services.CreateScope();
    var db = scope.ServiceProvider.GetRequiredService<Karamel.Backend.Data.BackendDbContext>();
    db.Database.EnsureDeleted();
    db.Database.EnsureCreated();
    db.Database.ExecuteSqlRaw("PRAGMA journal_mode=WAL;");
}

if (app.Environment.IsDevelopment())
{
    app.UseDeveloperExceptionPage();
    // Only enable the Swagger UI when not running under the test environment.
    // Tests will set the environment to "Testing" to avoid TestServer pipewriter issues.
    if (!app.Environment.IsEnvironment("Testing"))
    {
        app.UseSwagger();
        app.UseSwaggerUI();
    }
}
else if (!app.Environment.IsEnvironment("Testing"))
{
    app.UseHsts();
}

// Enable CORS in the request pipeline for development environment so the
// frontend running on a different port can make API requests without preflight failures.
// MUST be called before MapControllers and MapHub
if (app.Environment.IsDevelopment())
{
    // Use the more permissive policy for troubleshooting
    app.UseCors("DevCorsPermissive");
    Console.WriteLine("CORS enabled for development environment");
}
else
{
    app.UseCors("ProductionCors");
    Console.WriteLine("CORS enabled for production environment");
}

app.MapGet("/health", () => Results.Text("Healthy", "text/plain"))
    .WithName("Health");

// Map controller routes (API endpoints)
app.MapControllers();

// Map SignalR hubs
app.MapHub<Karamel.Backend.Hubs.PlaylistHub>("/hubs/playlist");

// Root redirect removed to keep test server requests focused on /health

app.Run();

// Make Program class visible for WebApplicationFactory in tests
public partial class Program { }
