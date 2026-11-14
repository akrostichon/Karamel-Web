using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Karamel.Backend.Data;
using Xunit;

namespace Karamel.Backend.Tests;

public class DatabaseConfigurationTests
{
    [Fact]
    public void ProductionConfiguration_UsesExpectedLoggingAndRetainsSecretsAndCors()
    {
        var path = Path.Combine(AppContext.BaseDirectory, "appsettings.Production.json");
        using var document = JsonDocument.Parse(File.ReadAllText(path));
        var root = document.RootElement;
        var logLevels = root.GetProperty("Logging").GetProperty("LogLevel");
        var corsOrigins = root.GetProperty("Cors").GetProperty("AllowedOrigins");

        Assert.False(root.TryGetProperty("ConnectionStrings", out _));
        Assert.Equal("Warning", logLevels.GetProperty("Default").GetString());
        Assert.Equal("Information", logLevels.GetProperty("Karamel").GetString());
        Assert.Equal("Warning", logLevels.GetProperty("Microsoft.AspNetCore").GetString());
        Assert.Equal("Warning", logLevels.GetProperty("Microsoft.EntityFrameworkCore").GetString());
        Assert.Equal("Information", logLevels.GetProperty("Microsoft.Hosting.Lifetime").GetString());
        Assert.False(logLevels.TryGetProperty("Microsoft.EntityFrameworkCore.Database.Command", out _));
        Assert.Equal(string.Empty, root.GetProperty("Karamel").GetProperty("TokenSecret").GetString());
        Assert.Equal(0, corsOrigins.GetArrayLength());
    }

    [Fact]
    public void DatabaseContext_LegacySqlServerSettingAndEmptyConnectionString_UsesSqliteDefault()
    {
        using var factory = new SqliteConfigurationFactory();
        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<BackendDbContext>();

        Assert.Equal("Microsoft.EntityFrameworkCore.Sqlite", db.Database.ProviderName);
        Assert.Contains("Data Source=karamel.db", db.Database.GetDbConnection().ConnectionString);
    }

    private sealed class SqliteConfigurationFactory : WebApplicationFactory<Program>
    {
        protected override void ConfigureWebHost(IWebHostBuilder builder)
        {
            builder.UseEnvironment("Testing");
            builder.ConfigureAppConfiguration((_, configuration) =>
            {
                configuration.AddInMemoryCollection(new Dictionary<string, string?>
                {
                    ["DB_PROVIDER"] = "SqlServer",
                    ["ConnectionStrings:DefaultConnection"] = string.Empty
                });
            });
        }
    }
}
