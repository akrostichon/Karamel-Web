using Fluxor;

namespace Karamel.Web.Store.Session;

public static class SessionReducers
{
    [ReducerMethod]
    public static SessionState ReduceInitializeSessionAction(SessionState state, InitializeSessionAction action) =>
        state with
        {
            CurrentSession = action.Session,
            IsInitialized = true
        };

    [ReducerMethod]
    public static SessionState ReduceUpdateSessionSettingsAction(SessionState state, UpdateSessionSettingsAction action)
    {
        if (state.CurrentSession == null)
            return state;

        var updatedSession = state.CurrentSession with
        {
            RequireSingerName = action.RequireSingerName,
            PauseBetweenSongsSeconds = action.PauseBetweenSongsSeconds,
            FilenamePattern = action.FilenamePattern
        };

        return state with
        {
            CurrentSession = updatedSession
        };
    }
}
