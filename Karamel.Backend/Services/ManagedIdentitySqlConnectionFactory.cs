using System;
using System.Data.Common;
using Microsoft.Data.SqlClient;
using Azure.Identity;

namespace Karamel.Backend.Services
{
    public static class ManagedIdentitySqlConnectionFactory
    {
        public static DbConnection Create(string connectionString)
        {
            var builder = new SqlConnectionStringBuilder(connectionString);
            var conn = new SqlConnection(builder.ConnectionString);
            // Acquire token using DefaultAzureCredential (managed identity in the App Service)
            var credential = new DefaultAzureCredential();
            var token = credential.GetToken(new Azure.Core.TokenRequestContext(new[] { "https://database.windows.net/.default" }));
            conn.AccessToken = token.Token;
            return conn;
        }
    }
}
