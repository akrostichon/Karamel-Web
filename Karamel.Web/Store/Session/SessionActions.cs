namespace Karamel.Web.Store.Session;

// Actions
public record InitializeSessionAction(Models.Session Session);
public record UpdateSessionSettingsAction(bool RequireSingerName, int PauseBetweenSongsSeconds, string FilenamePattern);
