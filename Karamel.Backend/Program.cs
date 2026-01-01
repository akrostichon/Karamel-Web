using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.EntityFrameworkCore;

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
var tokenSecret = builder.Configuration["TokenSecret"] ?? Environment.GetEnvironmentVariable("TOKEN_SECRET") ?? "dev-secret-change-me";
builder.Services.AddSingleton<Karamel.Backend.Services.ITokenService>(_ => new Karamel.Backend.Services.TokenService(tokenSecret));
// Add SignalR
builder.Services.AddSignalR();
// Register controllers for API endpoints
builder.Services.AddControllers();

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
