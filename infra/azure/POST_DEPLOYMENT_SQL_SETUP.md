# Post-Deployment SQL Setup

After deploying the infrastructure with Bicep, you must manually grant the App Service managed identity permissions in the SQL database.

## Prerequisites

- Infrastructure deployed via `infra/deploy.ps1`
- App Service has system-assigned managed identity enabled (done automatically by Bicep)
- SQL Server firewall allows your IP or you're using Azure Cloud Shell

## Steps

### 1. In azure powershell cli

Install-Module -Name SqlServer -Scope CurrentUser -Force
$token = (az account get-access-token --resource https://database.windows.net --query accessToken -o tsv)
Invoke-Sqlcmd `
   -ServerInstance "rg-karamel-prod-sqlsrv.database.windows.net" `
   -Database "rg-karamel-prod-sqldb" `
   -AccessToken $token `
   -Query @"
 CREATE USER [rg-karamel-prod-api] FROM EXTERNAL PROVIDER;
 ALTER ROLE db_datareader ADD MEMBER [rg-karamel-prod-api];
 ALTER ROLE db_datawriter ADD MEMBER [rg-karamel-prod-api];
 ALTER ROLE db_ddladmin ADD MEMBER [rg-karamel-prod-api];
 "@

### 2. Verify Connection

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

