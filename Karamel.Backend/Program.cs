using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

var builder = WebApplication.CreateBuilder(args);
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

// Root redirect removed to keep test server requests focused on /health

app.Run();

// Make Program class visible for WebApplicationFactory in tests
public partial class Program { }
