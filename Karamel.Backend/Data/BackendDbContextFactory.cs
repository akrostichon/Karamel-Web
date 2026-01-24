using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace Karamel.Backend.Data;

/// <summary>
/// Design-time factory for BackendDbContext. This allows EF Core tools (including migration bundles)
/// to create a DbContext without requiring the full application host and its dependencies
/// (like KARAMEL-TOKEN-SECRET). The factory only needs database connection information.
/// </summary>
public class BackendDbContextFactory : IDesignTimeDbContextFactory<BackendDbContext>
{
    public BackendDbContext CreateDbContext(string[] args)
    {
        var optionsBuilder = new DbContextOptionsBuilder<BackendDbContext>();

        // Read provider and connection string from environment variables
        // (set by the migration bundle workflow)
        var dbProvider = Environment.GetEnvironmentVariable("DB_PROVIDER") ?? "Sqlite";
        var connectionString = Environment.GetEnvironmentVariable("ConnectionStrings__DefaultConnection");

        // Suppress the pending model changes warning when applying migrations.
        // The model snapshot was created with SQLite type annotations, but migrations
        // are provider-agnostic and work correctly on both SQLite and SQL Server.
        optionsBuilder.ConfigureWarnings(warnings =>
            warnings.Ignore(Microsoft.EntityFrameworkCore.Diagnostics.RelationalEventId.PendingModelChangesWarning));

        if (string.Equals(dbProvider, "SqlServer", StringComparison.OrdinalIgnoreCase))
        {
            // For Azure SQL with managed identity, use the connection string directly
            // The migration bundle will run on the VM with managed identity access
            if (!string.IsNullOrWhiteSpace(connectionString))
            {
                optionsBuilder.UseSqlServer(connectionString);
            }
            else
            {
                // Fallback for local development
                optionsBuilder.UseSqlServer("Server=(local);Database=Karamel;Trusted_Connection=True;");
            }
        }
        else
        {
            // Default to SQLite for local development
            optionsBuilder.UseSqlite(connectionString ?? "Data Source=karamel.db");
        }

        return new BackendDbContext(optionsBuilder.Options);
    }
}
