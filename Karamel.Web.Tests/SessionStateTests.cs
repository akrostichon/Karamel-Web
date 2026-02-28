using Xunit;
using Karamel.Web.Store.Session;

namespace Karamel.Web.Tests
{
    public class SessionStateTests
    {
        [Fact]
        public void PauseSessionAction_SetsIsPausedTrue()
        {
            var initial = new SessionState { IsPaused = false };
            var result = SessionReducers.ReducePauseSessionAction(initial, new PauseSessionAction());
            Assert.True(result.IsPaused);
        }

        [Fact]
        public void ResumeSessionAction_SetsIsPausedFalse()
        {
            var initial = new SessionState { IsPaused = true };
            var result = SessionReducers.ReduceResumeSessionAction(initial, new ResumeSessionAction());
            Assert.False(result.IsPaused);
        }

        [Fact]
        public void SessionConfigUpdatedAction_UpdatesCurrentSessionFields()
        {
            var session = new Models.Session
            {
                RequireSingerName = false,
                AllowSingersToReorder = false,
                PauseBetweenSongsSeconds = 5,
                Theme = "light"
            };

            var initial = new SessionState { CurrentSession = session, IsInitialized = true };
            var action = new SessionConfigUpdatedAction(RequireSingerName: true, AllowSingersToReorder: true, PauseBetweenSongsSeconds: 10, Theme: "dark");

            var result = SessionReducers.ReduceSessionConfigUpdatedAction(initial, action);
            Assert.NotNull(result.CurrentSession);
            Assert.True(result.CurrentSession.RequireSingerName);
            Assert.True(result.CurrentSession.AllowSingersToReorder);
            Assert.Equal(10, result.CurrentSession.PauseBetweenSongsSeconds);
            Assert.Equal("dark", result.CurrentSession.Theme);
        }
    }
}
