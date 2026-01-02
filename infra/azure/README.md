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

2. Create a resource group (if not already created)

```powershell
az group create -n rg-karamel-dev -l westeurope
```

3. Deploy the Bicep template

```powershell
az deployment group create --resource-group rg-karamel-dev --template-file infra/azure/main.bicep --parameters @infra/azure/parameters.dev.json
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
