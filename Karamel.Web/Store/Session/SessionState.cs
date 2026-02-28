using Fluxor;

namespace Karamel.Web.Store.Session;

[FeatureState]
public record SessionState
{
    public Models.Session? CurrentSession { get; init; }
    public bool IsInitialized { get; init; }
    
    /// <summary>
    /// Indicates whether the session is currently paused by an administrator.
    /// This flag is transient and is not persisted to the backend database.
    /// </summary>
    public bool IsPaused { get; init; }
}
