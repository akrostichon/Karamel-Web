using Microsoft.EntityFrameworkCore;
using Microsoft.AspNetCore.SignalR;

var builder = WebApplication.CreateBuilder(args);
// Configure EF Core DbContext with provider-agnostic options
var dbProvider = builder.Configuration["DB_PROVIDER"] ?? System.Environment.GetEnvironmentVariable("DB_PROVIDER") ?? "Sqlite";
var connectionString = builder.Configuration.GetConnectionString("DefaultConnection") ?? builder.Configuration["DefaultConnection"];

if (string.Equals(dbProvider, "SqlServer", StringComparison.OrdinalIgnoreCase))
{
    builder.Services.AddDbContext<Karamel.Backend.Data.BackendDbContext>(options =>
        options.UseSqlServer(connectionString ?? "Server=(local);Database=Karamel;Trusted_Connection=True;"));
}
else
{
    // Default to SQLite (file-based) unless overridden
    builder.Services.AddDbContext<Karamel.Backend.Data.BackendDbContext>(options =>
        options.UseSqlite(connectionString ?? "Data Source=karamel.db"));
}

// Register repositories
builder.Services.AddScoped<Karamel.Backend.Repositories.ISessionRepository, Karamel.Backend.Repositories.SessionRepository>();
builder.Services.AddScoped<Karamel.Backend.Repositories.IPlaylistRepository, Karamel.Backend.Repositories.PlaylistRepository>();
// Register TokenService with secret from configuration (fallback for dev)
// Priority: Karamel:TokenSecret -> KARAMEL_TOKEN_SECRET environment var -> TokenSecret
var tokenSecret = builder.Configuration["Karamel:TokenSecret"]
                  ?? Environment.GetEnvironmentVariable("KARAMEL_TOKEN_SECRET")
                  ?? builder.Configuration["TokenSecret"]
                  ?? Environment.GetEnvironmentVariable("TOKEN_SECRET");

if (string.IsNullOrWhiteSpace(tokenSecret))
{
    if (!(builder.Environment.IsDevelopment() || builder.Environment.IsEnvironment("Testing")))
    {
        throw new InvalidOperationException("KARAMEL_TOKEN_SECRET (Karamel:TokenSecret) must be provided in non-development environments");
    }
    tokenSecret = "dev-secret-change-me";
}

if (tokenSecret.Length < 32)
{
    if (!(builder.Environment.IsDevelopment() || builder.Environment.IsEnvironment("Testing")))
    {
        throw new InvalidOperationException("KARAMEL_TOKEN_SECRET must be at least 32 characters long in non-development environments");
    }
}

builder.Services.AddSingleton<Karamel.Backend.Services.ITokenService>(_ => new Karamel.Backend.Services.TokenService(tokenSecret));
// Add SignalR and register hub filter globally
builder.Services.AddSignalR(options =>
{
    options.EnableDetailedErrors = true;
}).AddHubOptions<Karamel.Backend.Hubs.PlaylistHub>(options =>
{
    options.AddFilter<Karamel.Backend.Filters.LinkTokenHubFilter>();
});
// Register controllers for API endpoints
builder.Services.AddControllers();

// Register the session cleanup background service and the concrete instance so tests can resolve it
builder.Services.AddSingleton<Karamel.Backend.Services.SessionCleanupService>();
builder.Services.AddHostedService(sp => sp.GetRequiredService<Karamel.Backend.Services.SessionCleanupService>());

var app = builder.Build();

// Register Swagger services so swagger JSON can be generated in development runs.
// Skip registration when the service collection is read-only (e.g. test host).
if (!builder.Services.IsReadOnly)
{
    builder.Services.AddEndpointsApiExplorer();
    builder.Services.AddSwaggerGen();
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
