---
description: "Azure deployment guidance and resource naming conventions for Karamel-Web production environment"
applyTo: "**"
---

# Azure Deployment Guidelines

## Azure Deployment Warning

IMPORTANT: There is no development Azure environment for this repository. Do NOT deploy to the resource group `rg-karamel-dev` under any circumstances. All Azure deployments must target the production resource group `rg-karamel-prod` and must be reviewed before execution. Automated or manual deployments to `rg-karamel-dev` are strictly prohibited and can cause irreversible production-impacting configuration drift.

### Deployment Guidance
- **Always** set `--resource-group rg-karamel-prod` when running `az deployment group create` or similar commands.
- If you need a non-production environment, request the creation of a separate resource group and update the infra parameters accordingly.
- Double-check the parameters file (infra/azure/parameters.json) and ensure it references `rg-karamel-prod` before running any deployment commands.

## Azure Resource Names

**CRITICAL**: Use these exact resource names when interacting with Azure resources. All resource names use the `rg-karamel-prod` prefix pattern.

### Production Resources
- **Resource Group**: `rg-karamel-prod`
- **App Service (Backend API)**: `rg-karamel-prod-api`
  - URL: `https://rg-karamel-prod-api.azurewebsites.net`
- **App Service Plan**: `rg-karamel-prod-plan`
- **SQL Server**: `rg-karamel-prod-sqlsrv`
- **SQL Database**: `rg-karamel-prod-sqldb`
- **Application Insights**: `rg-karamel-prod-ai`
- **Key Vault**: `rg-karamel-prod-kv`
- **Virtual Network (SQL)**: `rg-karamel-prod-sql-vnet`
- **Static Web App**: (name varies, check Azure portal)
- **GitHub Runner**: `rg-karamel-prod-runner` (self-hosted)

### Common Mistakes to Avoid
- ❌ `karamel-prod-api` → ✅ `rg-karamel-prod-api`
- ❌ `karamel-prod-ai` → ✅ `rg-karamel-prod-ai`
- ❌ `karamel-prod-sqlsrv` → ✅ `rg-karamel-prod-sqlsrv`

### Resource Naming Convention
All resources follow the pattern: `${namePrefix}-${resourceType}` where `namePrefix = "rg-karamel-prod"`

This is defined in [infra/azure/main.bicep](../infra/azure/main.bicep):
```bicep
var sqlServerName = '${namePrefix}-sqlsrv'
var sqlDbName = '${namePrefix}-sqldb'
var webAppName = '${namePrefix}-api'
var appInsightsName = '${namePrefix}-ai'
```

### When These Rules Apply
These guidelines must be followed when:
- Running Azure CLI commands (`az`)
- Deploying infrastructure changes
- Modifying Azure resources
- Creating GitHub Actions workflows that deploy to Azure
- Reviewing pull requests that include Azure resource changes
