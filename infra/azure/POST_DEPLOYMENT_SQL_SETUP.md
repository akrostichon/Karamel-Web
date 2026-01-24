# Post-Deployment SQL Setup

After deploying the infrastructure with Bicep, you must manually grant the App Service managed identity permissions in the SQL database.

## Prerequisites

- Infrastructure deployed via `infra/deploy.ps1`
- App Service has system-assigned managed identity enabled (done automatically by Bicep)
- SQL Server firewall allows your IP or you're using Azure Cloud Shell

## Steps

### 1. Get the App Service Principal ID

From deployment output or run:

```powershell
az webapp identity show --name rg-karamel-prod-api --resource-group rg-karamel-prod --query principalId -o tsv
```

### 2. Connect to SQL Database

Option A - Azure Portal Query Editor:
1. Go to Azure Portal
2. Navigate to `rg-karamel-prod-sqldb` database
3. Click "Query editor (preview)"
4. Login with SQL admin credentials

Option B - Azure Data Studio or SSMS:
```powershell
# Get connection string
az sql db show-connection-string --client ado.net --name rg-karamel-prod-sqldb --server rg-karamel-prod-sqlsrv
```

### 3. Create Database User and Grant Permissions

Run this SQL script in the database (replace `rg-karamel-prod-api` with your actual web app name):

```sql
-- Create user for the App Service managed identity
-- The name MUST match the App Service name exactly
CREATE USER [rg-karamel-prod-api] FROM EXTERNAL PROVIDER;

-- Grant necessary permissions for EF Core operations
ALTER ROLE db_datareader ADD MEMBER [rg-karamel-prod-api];
ALTER ROLE db_datawriter ADD MEMBER [rg-karamel-prod-api];
ALTER ROLE db_ddladmin ADD MEMBER [rg-karamel-prod-api];
GO

-- Verify the user was created
SELECT name, type_desc, create_date 
FROM sys.database_principals 
WHERE name = 'rg-karamel-prod-api';
```

### 4. Verify Connection

After granting permissions, restart the App Service and test:

```powershell
# Restart app
az webapp restart --name rg-karamel-prod-api --resource-group rg-karamel-prod

# Test health endpoint
Invoke-WebRequest -Uri "https://rg-karamel-prod-api.azurewebsites.net/health" -UseBasicParsing

# Monitor logs
az webapp log tail --name rg-karamel-prod-api --resource-group rg-karamel-prod
```

## Troubleshooting

### Error: "Login failed for user 'NT AUTHORITY\ANONYMOUS LOGON'"

The managed identity user wasn't created in the database. Repeat Step 3.

### Error: "Cannot create user from external provider"

You need to be logged in as an Azure AD admin to create external provider users. Either:
- Use the SQL admin account you configured during deployment
- Set yourself as the Azure AD admin for the SQL Server first

### Error: "Name or service not known"

Network connectivity issue. Check:
1. SQL Server firewall allows Azure services (0.0.0.0)
2. If using private endpoint, App Service must have VNet integration
3. Connection string uses correct FQDN

## Automation Note

This step cannot be fully automated in Bicep because:
- Bicep cannot execute SQL commands directly
- SQL admin password is required to create users
- Microsoft.Sql/servers/databases/users resource type doesn't exist

Consider using Azure DevOps pipeline with SQL scripts or a post-deployment script that uses `sqlcmd` if full automation is needed.

## Reference

- [Use managed identities with Azure SQL Database](https://learn.microsoft.com/en-us/azure/app-service/tutorial-connect-msi-sql-database)
- [EF Core with Azure SQL and Managed Identity](https://learn.microsoft.com/en-us/azure/azure-sql/database/authentication-azure-ad-user-assigned-managed-identity)
