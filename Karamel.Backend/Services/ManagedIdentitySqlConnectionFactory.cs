using System;
using System.Data.Common;
using Microsoft.Data.SqlClient;
using Microsoft.Azure.Services.AppAuthentication;

namespace Karamel.Backend.Services
{
    public static class ManagedIdentitySqlConnectionFactory
    {
        public static System.Data.Common.DbConnection Create(string connectionString)
        {
            var builder = new SqlConnectionStringBuilder(connectionString);
            var conn = new SqlConnection(builder.ConnectionString);
            try
            {
                var tokenProvider = new AzureServiceTokenProvider();
                var token = tokenProvider.GetAccessTokenAsync("https://database.windows.net/").GetAwaiter().GetResult();
                conn.AccessToken = token;
            }
            catch
            {
                // If token acquisition fails, leave connection without AccessToken so fallback path (user/pass) works
            }
            return conn;
        }
    }
}
