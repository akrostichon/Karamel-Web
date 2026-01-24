# Azure Static Web App + Backend Deployment Guide

## Problem Solved

Fixed 405 (Method Not Allowed) errors when Static Web App frontend tries to call backend API. The issue was:

1. **No API proxy configured** - Static Web App had no route to backend
2. **No production CORS** - Backend only allowed localhost origins
3. **No production configuration** - Frontend didn't know backend URL

## Solution Implemented

Two complementary approaches:

### Approach 1: API Proxy (via staticwebapp.config.json) ✅ RECOMMENDED
- Static Web App now proxies `/api/*` requests to backend
- Requires linking backend API in Azure deployment
- **Advantage**: Frontend sees relative URLs like `/api/sessions`

### Approach 2: Direct CORS Calls (Fallback)
- Frontend has production appsettings with full backend URL
- Backend enables CORS for Static Web App origin
- **Advantage**: Works even if API proxy fails

## Files Changed

### Frontend (Karamel.Web)
1. **`wwwroot/staticwebapp.config.json`** (NEW)
   - Routes `/api/*` requests to backend
   - SPA fallback routing
   - MIME type configuration

2. **`wwwroot/appsettings.Production.json`** (NEW)
   ```json
   {
     "BackendBase": "https://karamel-prod-api.azurewebsites.net"
   }
   ```

### Backend (Karamel.Backend)
1. **`Program.cs`**
   - Added `ProductionCors` policy
   - Reads allowed origins from `appsettings.Production.json`
   - Enables CORS in production environment

2. **`appsettings.Production.json`**
   ```json
   {
     "Cors": {
       "AllowedOrigins": [
         "https://karamel-prod-static.azurestaticapps.net"
       ]
     }
   }
   ```

## Deployment Steps

### 1. Deploy Backend (App Service)

First, deploy the updated backend with CORS support:

```powershell
# Build backend
cd Karamel.Backend
dotnet publish -c Release -o ./publish

# Deploy to Azure App Service
az webapp deploy `
  --resource-group rg-karamel-prod `
  --name karamel-prod-api `
  --src-path ./publish `
  --type zip `
  --async true

# Verify CORS configuration is applied
az webapp config appsettings list `
  --resource-group rg-karamel-prod `
  --name karamel-prod-api `
  --query "[?name=='Cors__AllowedOrigins__0'].value"
```

**Important**: Ensure the following app settings exist:
- `APPLICATIONINSIGHTS_CONNECTION_STRING`
- `KARAMEL-TOKEN-SECRET` (from Key Vault)
- `DB_PROVIDER=SqlServer`
- `DB_USE_AAD=true`
- `DefaultConnection` (SQL connection string)

### 2. Link Backend API to Static Web App

Azure Static Web Apps can link to an external API (App Service):

```powershell
# Get backend API URL
$backendUrl = az webapp show `
  --resource-group rg-karamel-prod `
  --name karamel-prod-api `
  --query "defaultHostName" `
  --output tsv

# Link backend to Static Web App
# NOTE: This requires Azure CLI extension for Static Web Apps
az extension add --name staticwebapp

az staticwebapp backends link `
  --name karamel-prod-static `
  --resource-group rg-karamel-prod `
  --backend-resource-id "/subscriptions/<YOUR-SUB-ID>/resourceGroups/rg-karamel-prod/providers/Microsoft.Web/sites/karamel-prod-api" `
  --region northeurope
```

**Alternative**: Manually configure in Azure Portal:
1. Open Static Web App → Settings → APIs
2. Click **Link an existing API**
3. Select your App Service (`karamel-prod-api`)
4. Save

### 3. Deploy Frontend (Static Web App)

Deploy the frontend with the new configuration files:

```powershell
cd Karamel.Web
dotnet publish -c Release -o ./publish/wwwroot

# Deploy using Azure Static Web Apps CLI or GitHub Actions
# Option A: Manual upload (for testing)
az staticwebapp update `
  --name karamel-prod-static `
  --resource-group rg-karamel-prod `
  --source ./publish/wwwroot

# Option B: Use GitHub Actions (recommended)
# Push changes to main branch and let Actions deploy
```

### 4. Verify Deployment

#### Test Backend Health
```powershell
curl https://karamel-prod-api.azurewebsites.net/health
# Expected: "Healthy"
```

#### Test CORS Headers
```powershell
curl -I `
  -H "Origin: https://karamel-prod-static.azurestaticapps.net" `
  -H "Access-Control-Request-Method: POST" `
  -X OPTIONS `
  https://karamel-prod-api.azurewebsites.net/api/sessions
  
# Expected headers:
# Access-Control-Allow-Origin: https://karamel-prod-static.azurestaticapps.net
# Access-Control-Allow-Methods: POST, ...
```

#### Test Frontend
1. Open `https://karamel-prod-static.azurestaticapps.net`
2. Open browser DevTools → Network tab
3. Select a library folder
4. Click "Start Session"
5. Check Network tab:
   - Should see POST to `/api/sessions` (status 201)
   - Should NOT see 405 errors

## Troubleshooting

### Still Getting 405 Errors

**Check 1**: Verify API backend is linked
```powershell
az staticwebapp show `
  --name karamel-prod-static `
  --resource-group rg-karamel-prod `
  --query "linkedBackends"
```

**Check 2**: Verify staticwebapp.config.json is deployed
```powershell
# Download deployed files and check
az staticwebapp show `
  --name karamel-prod-static `
  --resource-group rg-karamel-prod `
  --query "customDomains"
```

**Check 3**: Test backend directly
```powershell
# Create session via curl
$body = '{"id": "test-session-id"}'
curl -X POST `
  -H "Content-Type: application/json" `
  -H "Origin: https://karamel-prod-static.azurestaticapps.net" `
  -d $body `
  https://karamel-prod-api.azurewebsites.net/api/sessions
```

### CORS Errors (403/401)

**Check 1**: Verify CORS policy is loaded
Look for log message in backend: `"CORS enabled for production environment"`

**Check 2**: Verify allowed origins match Static Web App URL
```powershell
az staticwebapp show `
  --name karamel-prod-static `
  --resource-group rg-karamel-prod `
  --query "defaultHostname"
  
# Should match the origin in appsettings.Production.json
```

**Check 3**: Test with wildcard temporarily (INSECURE - for debugging only)
```csharp
// In Program.cs, temporarily allow all origins:
policy.AllowAnyOrigin()
      .AllowAnyHeader()
      .AllowAnyMethod();
```

### Static Web App Not Using Backend Config

**Check**: Verify `appsettings.Production.json` is included in publish output
```powershell
Get-ChildItem -Path ./Karamel.Web/publish/wwwroot -Recurse -Filter "appsettings*.json"
```

If missing, check `.csproj` file for content exclusions.

## Monitoring with Application Insights

After deployment, monitor the API calls:

### View CORS Requests
```kql
requests
| where timestamp > ago(1h)
| where name contains "OPTIONS"
| project timestamp, name, url, resultCode, customDimensions
| order by timestamp desc
```

### View Session Creation
```kql
traces
| where timestamp > ago(1h)
| where message contains "Created new session"
| extend sessionId = tostring(customDimensions.SessionId)
| project timestamp, sessionId, customDimensions
```

### View CORS Failures
```kql
traces
| where timestamp > ago(1h)
| where severityLevel >= 2
| where message contains "CORS" or message contains "Invalid link token"
| project timestamp, severityLevel, message, customDimensions
```

## Security Considerations

1. **CORS Origins**: Only allow your Static Web App domain
   - ✅ `https://karamel-prod-static.azurestaticapps.net`
   - ❌ `https://*.azurestaticapps.net` (too permissive)

2. **Link Tokens**: Still validated by backend filters
   - Session must exist before library upload
   - Token has limited lifetime (configurable)

3. **Key Vault**: Token secret remains in Key Vault
   - Not exposed to Static Web App
   - Backend retrieves via managed identity

## Next Steps

1. **Custom Domain** (Optional):
   ```powershell
   az staticwebapp hostname set `
     --name karamel-prod-static `
     --resource-group rg-karamel-prod `
     --hostname karaoke.yourdomain.com
   ```
   Update CORS allowed origins accordingly.

2. **Production Testing**:
   - Test all features: library upload, playlist management, playback
   - Verify SignalR hub connections work
   - Check cross-tab synchronization

3. **Monitoring Setup**:
   - Create Application Insights dashboard
   - Set up alerts for 4xx/5xx errors
   - Monitor session creation patterns

## Cost Impact

- **Static Web App**: Free tier (100 GB bandwidth/month)
- **CORS Requests**: No additional cost
- **Application Insights**: Within free tier (5 GB/month)

Total expected additional cost: **$0/month**
