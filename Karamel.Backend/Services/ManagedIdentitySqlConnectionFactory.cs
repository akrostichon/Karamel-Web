using System;
using System.Data.Common;
using Microsoft.Data.SqlClient;
using Azure.Identity;
using Azure.Core;

namespace Karamel.Backend.Services
{
    public static class ManagedIdentitySqlConnectionFactory
    {
        public static System.Data.Common.DbConnection Create(string connectionString)
        {
            // Normalize connection string: replace "Server=" with "Data Source=" for Azure AD compatibility
            var normalized = connectionString
                .Replace("Server=", "Data Source=", StringComparison.OrdinalIgnoreCase)
                .Replace("server=", "Data Source=", StringComparison.Ordinal);
            
            // Remove SQL auth properties when using AAD
            var builder = new SqlConnectionStringBuilder(normalized)
            {
                // Clear SQL authentication properties
                UserID = string.Empty,
                Password = string.Empty,
                IntegratedSecurity = false
            };
            
            var conn = new SqlConnection(builder.ConnectionString);
            try
            {
                var credential = new DefaultAzureCredential();
                var tokenRequestContext = new TokenRequestContext(new[] { "https://database.windows.net/.default" });
                var token = credential.GetToken(tokenRequestContext, default);
                conn.AccessToken = token.Token;
            }
            catch (Exception ex)
            {
                // Log the error for debugging but don't throw - allows fallback to connection string auth
                System.Diagnostics.Debug.WriteLine($"Failed to acquire Azure AD token: {ex.Message}");
            }
            return conn;
        }
    }
}
