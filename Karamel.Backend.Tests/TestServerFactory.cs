using System;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.EntityFrameworkCore;
using Microsoft.Data.Sqlite;
using Karamel.Backend.Data;

namespace Karamel.Backend.Tests;

public class TestServerFactory : WebApplicationFactory<Program>
{
    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        // Attach global handlers so unhandled exceptions during tests are logged
        // and unobserved task exceptions do not terminate the test process.
        AppDomain.CurrentDomain.UnhandledException += (s, e) =>
        {
            try
            {
                Console.Error.WriteLine($"[TestServerFactory] Unhandled exception: {e.ExceptionObject}");
            }
            catch { }
        };

        TaskScheduler.UnobservedTaskException += (s, e) =>
        {
            try
            {
                Console.Error.WriteLine($"[TestServerFactory] Unobserved task exception: {e.Exception}");
                e.SetObserved();
            }
            catch { }
        };

        builder.UseEnvironment("Testing");

            builder.ConfigureServices(services =>
            {
                // Register a startup filter so the exception-catching middleware
                // is added to the application's pipeline without replacing the
                // app configuration created in Program.cs.
                services.AddSingleton<IStartupFilter, TestExceptionStartupFilter>();
                // Remove existing BackendDbContext registration if present
                var descriptor = services.SingleOrDefault(d => d.ServiceType == typeof(DbContextOptions<BackendDbContext>));
                if (descriptor != null) services.Remove(descriptor);

                // Create a single shared in-memory SQLite connection and open it. Register it so
                // the same connection instance is used for all DbContext instances during tests.
                var connection = new SqliteConnection("DataSource=:memory:");
                connection.Open();
                services.AddSingleton(connection);

                // Use the shared connection for the DbContext so the in-memory schema persists.
                services.AddDbContext<BackendDbContext>(options =>
                {
                    options.UseSqlite(connection);
                });

                // Build a temporary provider to create the schema
                var sp = services.BuildServiceProvider();
                using var scope = sp.CreateScope();
                var db = scope.ServiceProvider.GetRequiredService<BackendDbContext>();
                db.Database.EnsureCreated();
            });
    }
}

internal class TestExceptionStartupFilter : IStartupFilter
{
    public Action<IApplicationBuilder> Configure(Action<IApplicationBuilder> next)
    {
        return builder =>
        {
            builder.Use(async (context, middlewareNext) =>
            {
                try
                {
                    await middlewareNext();
                }
                catch (Exception ex)
                {
                    try { Console.Error.WriteLine($"[TestExceptionStartupFilter] Unhandled request exception: {ex}"); } catch { }
                    context.Response.StatusCode = 500;
                    await context.Response.CompleteAsync();
                }
            });

            // Continue the normal startup pipeline
            next(builder);
        };
    }
}
