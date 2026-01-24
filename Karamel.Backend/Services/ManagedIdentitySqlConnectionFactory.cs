using Microsoft.Data.SqlClient;
using Azure.Identity;
using Azure.Core;

namespace Karamel.Backend.Services
{
    public static class ManagedIdentitySqlConnectionFactory
    {
        public static System.Data.Common.DbConnection Create(string connectionString)
        {
            // Use the connection string directly - it's already correctly formatted from Bicep/Azure config
            // Do NOT use SqlConnectionStringBuilder as it throws ArgumentException when parsing
            // a connection string with "Authentication=Active Directory Default" that has any
            // SQL auth keywords (UserID/Password), even if empty. The constructor validates
            // this before we can set properties to clear them.
            var conn = new SqlConnection(connectionString);
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
