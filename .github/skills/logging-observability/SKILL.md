---
name: logging-observability
description: "Logging and observability patterns for Karamel-Web using stdout and the Render dashboard"
disable-model-invocation: true
---

# Logging & Observability

## Backend

- Use `ILogger<T>` with structured message templates and parameters.
- Logs go to stdout; inspect them in the Render dashboard.
- Never log tokens, secrets, or full client IP addresses. Avoid logging personal data.
- Use Information for normal operations, Warning for recoverable issues, and Error for failures.
- Log exceptions with context and preserve them when rethrowing.

```csharp
_logger.LogInformation("Session {SessionId} created", sessionId);
_logger.LogError(exception, "Failed to update session {SessionId}", sessionId);
```

## Browser

- Use `wwwroot/js/logger.js` for console logging; it does not send telemetry.
- Keep the configured log levels: Debug on localhost, Warn in production.
- Use structured properties, and omit tokens, personal data, and local file paths.

```javascript
const logger = createLogger('ModuleName');
logger.warn('SignalR reconnecting', { attempt });
logger.error('Failed to load song', { error: error.message });
```

## Validation

- Confirm backend log messages appear in the Render service dashboard.
- Verify logs contain no tokens, secrets, or full client IP addresses.
