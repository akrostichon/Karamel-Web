namespace Karamel.Web.Store.Session;

// Actions
public record InitializeSessionAction(Models.Session Session);
public record UpdateSessionSettingsAction(bool RequireSingerName, int PauseBetweenSongsSeconds, string FilenamePattern);

// new actions for admin control
public record PauseSessionAction();
public record ResumeSessionAction();

/// <summary>
/// Dispatched when session configuration fields are updated via SignalR or API.
/// The payload contains the same fields that come from the backend DTO so the
/// frontend can update its local state accordingly.
/// </summary>
public record SessionConfigUpdatedAction(bool RequireSingerName, bool AllowSingersToReorder, int PauseBetweenSongsSeconds, string? Theme);
