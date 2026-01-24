# Application Insights Deployment Guide

## Summary

Application Insights telemetry has been successfully integrated into Karamel-Web for production observability. The implementation provides:

- **Backend telemetry**: Requests, dependencies, exceptions, and custom logging
- **Frontend telemetry**: Page views, client-side exceptions, and performance metrics
- **Structured logging**: Added to all Controllers, SignalR Hubs, and authentication filters
- **Error boundaries**: Blazor ErrorBoundary component catches unhandled component exceptions

## Changes Made

### Backend (Karamel.Backend)

1. **NuGet Package**: Added `Microsoft.ApplicationInsights.AspNetCore` v2.23.0
2. **Program.cs**: Registered Application Insights telemetry service
3. **Logging Added**:
   - **PlaylistHub**: All mutation methods (AddItem, RemoveItem, Reorder) with try-catch blocks
   - **LibraryController**: BulkUpsert method with error handling
   - **SessionsController**: Create method with logging
   - **LinkTokenActionFilter**: Authentication failure logging with IP address
   - **LinkTokenHubFilter**: SignalR authentication failure logging

### Infrastructure (Bicep)

1. **appinsights.bicep**: Added `connectionString` output
2. **webapp.bicep**: 
   - Added `appInsightsConnectionString` parameter
   - Configured `APPLICATIONINSIGHTS_CONNECTION_STRING` app setting
3. **main.bicep**: Wired connection string from App Insights module to Web App module

### Frontend (Karamel.Web)

1. **index.html**: Added Application Insights JavaScript SDK snippet (minified v2.8.x)
2. **App.razor**: Wrapped Router with ErrorBoundary component for unhandled exception catching

## Verification Steps

### 1. Check Live Metrics

1. Open Azure Portal → Application Insights → `karamel-prod-ai`
2. Navigate to **Live Metrics**
3. Make a request to your app (e.g., create a session, upload library)
4. Verify you see:
   - **Incoming Requests** count increasing
   - **Request Duration** metrics
   - **Dependency Calls** (SQL queries)
   - **Servers** showing your App Service instance

### 2. Query Logs (Kusto)

Navigate to **Logs** and run queries:

#### View Recent Requests
```kql
requests
| where timestamp > ago(1h)
| project timestamp, name, url, resultCode, duration
| order by timestamp desc
| take 50
```

#### View Exceptions
```kql
exceptions
| where timestamp > ago(1h)
| project timestamp, type, outerMessage, innermostMessage, operation_Name
| order by timestamp desc
```

#### View Custom Logs (Structured Logging)
```kql
traces
| where timestamp > ago(1h)
| where message contains "Adding item to playlist" or message contains "BulkUpsert"
| project timestamp, severityLevel, message, customDimensions
| order by timestamp desc
```

#### View Authentication Failures
```kql
traces
| where timestamp > ago(1h)
| where message contains "Invalid link token" or message contains "Missing X-Link-Token"
| project timestamp, severityLevel, message, customDimensions
| order by timestamp desc
```

### 3. Test Error Scenarios

Trigger errors to verify logging works:

1. **Invalid Session ID**: Access `/api/sessions/00000000-0000-0000-0000-000000000000`
   - Should log 404 in requests table
   
2. **Invalid Link Token**: Make a request to `/api/sessions/{valid-guid}/library/bulk` with wrong token
   - Should see "Invalid link token" warning in traces
   
3. **SignalR Auth Failure**: Connect to PlaylistHub and call `AddItemAsync` without X-Link-Token
   - Should see hub exception in traces with connection ID

### 4. Frontend Telemetry

1. Open browser DevTools → Network tab
2. Navigate through the app (Home → Playlist → Singer View)
3. Look for requests to `https://dc.services.visualstudio.com/v2/track` (App Insights endpoint)
4. In Azure Portal, check:
   ```kql
   pageViews
   | where timestamp > ago(1h)
   | project timestamp, name, url, duration
   ```

## Using Application Insights for Debugging

### Real-Time Debugging (Live Metrics)

When you need immediate feedback:
1. Open **Live Metrics** in Azure Portal
2. Keep it open while reproducing the issue
3. Watch for:
   - Failed requests (red bars in charts)
   - Exceptions (listed in real-time)
   - Long-running requests (high duration spikes)

### Post-Mortem Analysis (Logs)

For issues that already occurred:

1. **Find failed requests**:
   ```kql
   requests
   | where timestamp > ago(24h)
   | where success == false
   | project timestamp, name, resultCode, duration, customDimensions
   ```

2. **Correlate with exceptions**:
   ```kql
   union requests, exceptions
   | where timestamp > ago(24h)
   | where operation_Id == "paste-operation-id-here"
   | order by timestamp asc
   ```

3. **View dependency failures** (SQL timeouts, etc.):
   ```kql
   dependencies
   | where timestamp > ago(24h)
   | where success == false
   | project timestamp, name, type, target, duration, resultCode
   ```

### Custom Queries for Karamel-Web

#### Session Creation Activity
```kql
traces
| where timestamp > ago(7d)
| where message contains "Created new session"
| extend sessionId = tostring(customDimensions.SessionId)
| summarize count() by bin(timestamp, 1h)
| render timechart
```

#### Library Upload Performance
```kql
traces
| where timestamp > ago(7d)
| where message contains "Starting bulk upsert"
| extend songCount = toint(customDimensions.Count)
| project timestamp, songCount, customDimensions.SessionId
```

#### Playlist Mutations (Most Active Users)
```kql
traces
| where timestamp > ago(7d)
| where message contains "Adding item to playlist" 
    or message contains "Removing item"
    or message contains "Reordering playlist"
| extend sessionId = tostring(customDimensions.SessionId)
| summarize mutations = count() by sessionId
| order by mutations desc
| take 10
```

## Cost Considerations

- **Free Tier**: First 5 GB/month of ingested data is free
- **Expected Usage**: With single-user load and structured logging, expect ~50-100 MB/day max
- **90-Day Retention**: Default retention (adjustable if needed)
- **Estimated Monthly Cost**: $0 (well within free tier)

If you start seeing costs:
1. Enable **Sampling** in appsettings.json:
   ```json
   "ApplicationInsights": {
     "InstrumentationKey": "...",
     "EnableAdaptiveSampling": true,
     "SamplingPercentage": 50.0
   }
   ```

2. Reduce console.log verbosity in JavaScript files (future cleanup task)

## Troubleshooting

### No Telemetry Appearing

1. **Check connection string**:
   ```powershell
   az webapp config appsettings list -g rg-karamel-prod -n karamel-prod-api | grep APPLICATIONINSIGHTS
   ```

2. **Check App Insights status**:
   ```powershell
   az monitor app-insights component show -g rg-karamel-prod -n karamel-prod-ai --query "ingestionMode"
   ```

3. **Check backend logs** (stdout):
   ```powershell
   az webapp log tail -g rg-karamel-prod -n karamel-prod-api
   ```
   Look for messages like `[Microsoft.ApplicationInsights]` on startup

### Frontend Telemetry Not Working

1. **Check browser console** for errors from `ai.2.min.js`
2. **Verify CORS**: App Insights endpoint should be accessible (no CORS errors)

### Logs Not Structured (No customDimensions)

Ensure you're using structured logging syntax:
```csharp
// ✅ Correct - structured
_logger.LogInformation("Adding item to playlist {PlaylistId} in session {SessionId}", playlistId, sessionId);

// ❌ Incorrect - string interpolation
_logger.LogInformation($"Adding item to playlist {playlistId} in session {sessionId}");
```

## Documentation Links

- [Application Insights Overview](https://learn.microsoft.com/azure/azure-monitor/app/app-insights-overview)
- [Kusto Query Language (KQL)](https://learn.microsoft.com/azure/data-explorer/kusto/query/)
- [Structured Logging Best Practices](https://learn.microsoft.com/aspnet/core/fundamentals/logging/)
- [JavaScript SDK Configuration](https://learn.microsoft.com/azure/azure-monitor/app/javascript)

