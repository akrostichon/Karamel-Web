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

### JavaScript Logging Infrastructure

**Centralized Logger** (`wwwroot/js/logger.js`):
- All JavaScript modules use a shared logging abstraction
- Automatic Application Insights integration (warnings → `trackEvent`, errors → `trackException`)
- Log level filtering based on environment (development vs production)
- Structured properties: `moduleName`, `timestamp`, `sessionId`

**Log Levels**:
- **Debug (0)**: Development-time diagnostics, state changes, message passing
- **Info (1)**: User actions, session initialization, important state transitions
- **Warn (2)**: Fallback scenarios, recoverable errors, validation failures
- **Error (3)**: Exceptions, critical failures, network errors

**Environment Configuration** (`wwwroot/index.html`):
```javascript
// Production: only warnings and errors in console and Application Insights
window.logLevel = 2; // 0=Debug, 1=Info, 2=Warn, 3=Error

// Development (localhost): all logs visible in console
if (window.location.hostname === 'localhost' || window.location.hostname === '127.0.0.1') {
    window.logLevel = 0;
}
```

### Usage Examples

**Create a logger instance**:
```javascript
import { createLogger } from './logger.js';

const logger = createLogger('ModuleName'); // e.g., 'SignalRBridge', 'Player', 'FileAccess'
```

**Log with structured properties**:
```javascript
// Debug: development-only diagnostics (filtered out in production)
logger.debug('Processing message', { messageType, sessionId });

// Info: user actions worth tracking
logger.info('Library scan started', { folderName, expectedFiles: 150 });

// Warn: recoverable errors (tracked in Application Insights)
logger.warn('Falling back to BroadcastChannel', { reason: 'SignalR connection failed' });

// Error: critical failures (tracked as exceptions in Application Insights)
logger.error('Failed to load song file', { mp3FileName, error: ex.message });
```

**When to use each log level**:
- **Debug**: Message passing, state updates, broadcast events, SignalR messages
- **Info**: Session creation, library upload, consent decisions, playback start/stop
- **Warn**: SignalR fallback, large file skipping, validation warnings, network issues
- **Error**: File load failures, playback errors, API failures, unhandled exceptions

### Production Observability

**What gets tracked in Application Insights**:
- ✅ **Warnings**: Custom events with `name` = module + message, `customDimensions` = properties
- ✅ **Errors**: Exceptions with stack trace, module context, structured properties
- ❌ **Debug/Info**: Filtered out in production (visible only in development console)

**Example telemetry**:
```javascript
// This line in production:
logger.warn('SignalR disconnected, using BroadcastChannel fallback', { sessionId, attempt: 3 });

// Creates this Application Insights event:
{
  name: "SignalRBridge: SignalR disconnected, using BroadcastChannel fallback",
  timestamp: "2026-02-12T14:30:00.000Z",
  customDimensions: {
    moduleName: "SignalRBridge",
    sessionId: "abc-123-def",
    attempt: 3
  }
}
```

### C# Blazor Components

**Development Logging**:
- Use `#if DEBUG` preprocessor directives for console output
- Avoid `Console.WriteLine` in production builds (logs are invisible in Blazor WASM)

**Example**:
```csharp
#if DEBUG
Console.WriteLine($"[SessionService] Restoring state for session {sessionId}");
#endif
```

**Production Logging**:
- Rely on JavaScript logger for frontend telemetry
- Backend API calls are already logged via `ILogger<T>` on the server
- Use ErrorBoundary component in App.razor for unhandled Blazor exceptions

### Client-Side Telemetry
- Application Insights JavaScript SDK in index.html (consent-aware via consentBanner.js)
- ErrorBoundary component in App.razor catches unhandled Blazor exceptions
- JavaScript logger automatically integrates with Application Insights for Warn/Error levels

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

#### Frontend JavaScript Events (Warnings)
```kusto
customEvents
| where timestamp > ago(30m)
| where name contains "SignalRBridge" or name contains "Player" or name contains "FileAccess"
| project timestamp, name, customDimensions
| order by timestamp desc
```

#### Frontend JavaScript Errors
```kusto
exceptions
| where timestamp > ago(30m)
| where outerMessage contains "SignalRBridge" or outerMessage contains "Player"
| project timestamp, type, outerMessage, customDimensions
| order by timestamp desc
```

#### Frontend Errors by Module
```kusto
exceptions
| where timestamp > ago(30m)
| extend moduleName = tostring(customDimensions.moduleName)
| where isnotempty(moduleName)
| summarize errorCount = count() by moduleName
| order by errorCount desc
```

#### SignalR Fallback Events
```kusto
customEvents
| where timestamp > ago(30m)
| where name contains "Falling back to BroadcastChannel" or name contains "SignalR"
| project timestamp, name, customDimensions.sessionId, customDimensions
| order by timestamp desc
```

#### File Load Errors
```kusto
exceptions
| where timestamp > ago(30m)
| where customDimensions.moduleName == "FileAccess" or customDimensions.moduleName == "Player"
| extend fileName = tostring(customDimensions.mp3FileName)
| project timestamp, outerMessage, fileName, customDimensions
| order by timestamp desc
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

### Backend (C#)

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

### Frontend (JavaScript)

1. **Import and Create Logger**
   ```javascript
   import { createLogger } from './logger.js';
   
   const logger = createLogger('YourModuleName');
   ```

2. **Log Key Operations with Structured Properties**
   ```javascript
   // User action
   logger.info('Feature activated', { featureId, userId });
   
   // Recoverable error
   logger.warn('API call failed, retrying', { endpoint, attempt: 2 });
   
   // Critical error
   logger.error('Failed to load resource', { resourceId, error: ex.message });
   ```

3. **Choose Appropriate Log Level**
   - Use `debug()` for development diagnostics (filtered in production)
   - Use `info()` for important user actions
   - Use `warn()` for recoverable errors (tracked in Application Insights)
   - Use `error()` for critical failures (tracked as exceptions)

4. **Test Log Filtering**
   - In development (localhost): verify all logs appear in console
   - In production simulation: set `window.logLevel = 2`, confirm only warnings/errors appear
   - Check browser console shows `[ModuleName]` prefix for easy filtering

### C# Blazor Components

1. **Wrap Debug Logs in Preprocessor Directives**
   ```csharp
   #if DEBUG
   Console.WriteLine($"[ComponentName] Debug information: {value}");
   #endif
   ```

2. **Use JavaScript Logger for Production Telemetry**
   - Prefer JavaScript logger for client-side observability
   - Backend API calls are logged via `ILogger<T>` on server
   - Use ErrorBoundary for unhandled Blazor exceptions

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
