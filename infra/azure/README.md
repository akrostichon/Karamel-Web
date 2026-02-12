# Azure Infra (Bicep) — Karamel Phase 7

This folder contains Bicep templates to provision resources:

- Key Vault
- Azure SQL Server + Database (example SKU; adjust for serverless elsewhere if needed)
- App Service Plan + Web App (backend)
- Static Web App (frontend)
- Application Insights

Should still configure RBAC and Key Vault access policies for managed identities.

Quick deploy (examples):

1. Log in and select subscription

```powershell
az login
az account set --subscription "YOUR_SUBSCRIPTION_ID"
```

2. Create a production resource group

This repository is configured to use a single production environment to keep costs low.

```powershell
az group create -n rg-karamel-prod -l northeurope
```

3. Deploy the Bicep template (single-prod)

Use the consolidated `parameters.json` for production defaults. Replace secrets before running.

```powershell
az deployment group create --resource-group rg-karamel-prod --template-file infra/azure/main.bicep --parameters @infra/azure/parameters.json
```

4. After deployment:
- Store `KARAMEL_TOKEN_SECRET` in Key Vault `kv-karamel-dev` and grant the App Service managed identity access.
- The backend now supports Azure AD (managed identity) authentication to Azure SQL. Recommended steps:
	- Ensure the Web App has a system-assigned managed identity (the Bicep template enables this).
	- Create a contained database user mapped to the Web App's managed identity and grant it the necessary DB role (the `infra/deploy.ps1` script attempts to do this automatically):
		- `CREATE USER [<webAppName>] FROM EXTERNAL PROVIDER; ALTER ROLE db_owner ADD MEMBER [<webAppName>];`
	- Set the App Setting `DB_USE_AAD=true` on the Web App so the backend uses the managed identity flow at runtime.
	- Do NOT store admin DB passwords in production; use Key Vault only for temporary admin credentials during migration or for rotation.
- Configure app settings in the Web App (Key Vault references, `WEBSITES_ENABLE_WEBSOCKETS=1`).

Notes and caveats:
- The SQL SKU used above is a basic example; to use serverless you can change the SKU and settings accordingly (serverless requires specific SKUs and compute tier settings).
- Static Web Apps are created plainly — connect to GitHub Actions for automatic publish.
- This template is intentionally minimal to get started for dev. Review compliance, networking, and secrets policies for production.
## SignalR Troubleshooting

### Connection Timeout Errors

If you see errors like:
- `Server timeout elapsed without receiving a message from the server`
- `WebSocket failed to connect. The connection could not be found on the server`
- `There was an error with the transport 'WebSockets'`

**Root Causes:**
1. **WebSockets not enabled** in App Service
2. **ARR Affinity (sticky sessions) disabled** - required for stateful SignalR connections
3. **Free tier limitations** - cold starts and no AlwaysOn feature

**Quick Fix (without redeployment):**

```powershell
# Run the diagnostic script to check current configuration
.\infra\diagnose_signalr.ps1

# Apply fixes automatically (enables WebSockets + ARR Affinity)
.\infra\fix_signalr.ps1

# If issues persist, restart the app
az webapp restart -g rg-karamel-prod -n rg-karamel-prod-api
```

**Manual Fix:**

```powershell
# Enable WebSockets
az webapp config set -g rg-karamel-prod -n rg-karamel-prod-api --web-sockets-enabled true

# Enable sticky sessions (ARR Affinity)
az webapp update -g rg-karamel-prod -n rg-karamel-prod-api --client-affinity-enabled true

# Set app setting
az webapp config appsettings set -g rg-karamel-prod -n rg-karamel-prod-api --settings WEBSITES_ENABLE_WEBSOCKETS=true
```

**Long-term Fix:**

Redeploy the Bicep templates (already updated with proper settings):

```powershell
az deployment group create --resource-group rg-karamel-prod --template-file infra/azure/main.bicep --parameters @infra/azure/parameters.json
```

**Production Recommendations:**
- **Upgrade from Free tier to B1 or higher** to enable AlwaysOn and avoid cold starts
- **Monitor SignalR connections** in Application Insights:
  ```kusto
  exceptions
  | where timestamp > ago(1h)
  | where outerMessage contains "SignalR" or outerMessage contains "WebSocket"
  | project timestamp, outerMessage, customDimensions
  ```

### Verifying Configuration

Check if WebSockets and sticky sessions are enabled:

```powershell
# Check WebSockets
az webapp config show -g rg-karamel-prod -n rg-karamel-prod-api --query "webSocketsEnabled"

# Check ARR Affinity
az webapp show -g rg-karamel-prod -n rg-karamel-prod-api --query "clientAffinityEnabled"
```

Both should return `true`.