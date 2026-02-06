---
description: "Logging and observability patterns for Karamel-Web with Application Insights integration"
applyTo: "**"
---

# Logging & Observability

**Application Insights Integration**: Production telemetry enabled for both backend and frontend.

## Backend Logging (Karamel.Backend)

### Structured Logging
- Uses `ILogger<T>` with structured logging throughout
- Logged components: PlaylistHub, LibraryController, SessionsController, LinkTokenActionFilter, LinkTokenHubFilter
- **Pattern**: `_logger.LogInformation("Message with {Param1} and {Param2}", value1, value2)` 
  - Use structured logging (NOT string interpolation)
  - This enables proper indexing in Application Insights

### Error Handling
- Use try-catch blocks with `_logger.LogError(ex, "Context message", contextData)`
- Auth failures logged at Warning level with session IDs and IP addresses
- Never log sensitive data (passwords, full tokens, personal information)

### Log Levels
- **Information**: Normal flow, successful operations, expected state changes
- **Warning**: Validation failures, authentication issues, recoverable errors
- **Error**: Exceptions, critical failures, unrecoverable errors
- **Debug**: Development-time diagnostics (disabled in production)

## Frontend Logging (Karamel.Web)

### Client-Side Telemetry
- Application Insights JavaScript SDK in index.html (client-side telemetry)
- ErrorBoundary component in App.razor catches unhandled Blazor exceptions
- Console.WriteLine used in development environments

## Viewing Logs

### Azure Portal

**Live Metrics** (Real-Time Debugging):
- Navigate to Application Insights → **Live Metrics** for immediate feedback
- Watch for failed requests, exceptions, and long-running operations
- Use when actively reproducing issues

**Logs** (Post-Mortem Analysis):
- Navigate to Application Insights → **Logs** for Kusto Query Language (KQL) queries
- Query tables: `requests`, `exceptions`, `traces`, `dependencies`, `pageViews`

### Common Kusto Queries

#### Recent Requests
```kusto
requests
| where timestamp > ago(30m)
| project timestamp, name, url, resultCode, duration
| order by timestamp desc
| take 50
```

#### Recent Exceptions
```kusto
exceptions
| where timestamp > ago(30m)
| project timestamp, type, outerMessage, innermostMessage, operation_Name
| order by timestamp desc
```

#### Failed Requests
```kusto
requests
| where timestamp > ago(30m)
| where success == false
| project timestamp, name, resultCode, duration, customDimensions
```

#### Custom Traces (Structured Logs)
```kusto
traces
| where timestamp > ago(30m)
| where message contains "SessionId"
| project timestamp, severityLevel, message, customDimensions
| order by timestamp desc
```

#### Authentication Failures
```kusto
traces
| where timestamp > ago(30m)
| where message contains "Invalid link token" or message contains "Missing X-Link-Token"
| project timestamp, severityLevel, message, customDimensions
| order by timestamp desc
```

#### Dependency Failures (SQL Timeouts)
```kusto
dependencies
| where timestamp > ago(30m)
| where success == false
| project timestamp, name, type, target, duration, resultCode
```

### Karamel-Web Specific Queries

#### Session Creation Activity
```kusto
traces
| where timestamp > ago(30m)
| where message contains "Created new session"
| extend sessionId = tostring(customDimensions.SessionId)
| summarize count() by bin(timestamp, 1m)
| render timechart
```

#### Library Upload Performance
```kusto
traces
| where timestamp > ago(30m)
| where message contains "Starting bulk upsert"
| extend songCount = toint(customDimensions.Count)
| project timestamp, songCount, customDimensions.SessionId
```

#### Playlist Mutations (Most Active Sessions)
```kusto
traces
| where timestamp > ago(30m)
| where message contains "Adding item to playlist" 
    or message contains "Removing item"
    or message contains "Reordering playlist"
| extend sessionId = tostring(customDimensions.SessionId)
| summarize mutations = count() by sessionId
| order by mutations desc
| take 10
```

#### Correlate Requests with Exceptions
```kusto
union requests, exceptions
| where timestamp > ago(30m)
| where operation_Id == "paste-operation-id-here"
| order by timestamp asc
```

## When Adding New Features

Follow these practices when adding logging to new code:

1. **Inject Logger**
   ```csharp
   private readonly ILogger<YourClass> _logger;
   
   public YourClass(ILogger<YourClass> logger)
   {
       _logger = logger;
   }
   ```

2. **Log Key Operations**
   ```csharp
   _logger.LogInformation("Operation started with {ParameterId}", parameterId);
   ```

3. **Wrap Risky Operations**
   ```csharp
   try
   {
       // Risky operation
   }
   catch (Exception ex)
   {
       _logger.LogError(ex, "Operation failed for {ContextData}", contextData);
       throw;
   }
   ```

4. **Use Structured Parameters**
   - ✅ `_logger.LogInformation("User {UserId} logged in", userId)`
   - ❌ `_logger.LogInformation($"User {userId} logged in")`

5. **Never Log Sensitive Data**
   - Avoid: passwords, tokens, API keys, credit cards, personal identifiable information
   - Redact or hash if logging is required for debugging

## Performance Considerations

- Logging adds overhead; avoid excessive logging in tight loops
- Use appropriate log levels (Info for business events, Debug for detailed diagnostics)
- Application Insights automatically samples high-volume telemetry in production

## Troubleshooting

### Logs Not Structured (No customDimensions)

Ensure you're using structured logging syntax:

**✅ Correct - Structured**
```csharp
_logger.LogInformation("Adding item to playlist {PlaylistId} in session {SessionId}", playlistId, sessionId);
```

**❌ Incorrect - String Interpolation**
```csharp
_logger.LogInformation($"Adding item to playlist {playlistId} in session {sessionId}");
```

String interpolation prevents Application Insights from indexing parameters as `customDimensions`, making queries less effective.

### No Telemetry Appearing

Check the App Service configuration:
```powershell
az webapp config appsettings list -g rg-karamel-prod -n rg-karamel-prod-api | grep APPLICATIONINSIGHTS
```

Verify the connection string is set and the backend logs show Application Insights initialization:
```powershell
az webapp log tail -g rg-karamel-prod -n rg-karamel-prod-api
```
