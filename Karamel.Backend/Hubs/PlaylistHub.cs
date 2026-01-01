using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.SignalR;
using System.Threading.Tasks;

namespace Karamel.Backend.Hubs
{
    // Hub for playlist real-time updates. Authorization will be enforced in methods where needed.
    public class PlaylistHub : Hub
    {
        public async Task JoinSession(string sessionId)
        {
            // Add the caller to a group for the session
            await Groups.AddToGroupAsync(Context.ConnectionId, GetSessionGroupName(sessionId));
        }

        public async Task LeaveSession(string sessionId)
        {
            await Groups.RemoveFromGroupAsync(Context.ConnectionId, GetSessionGroupName(sessionId));
        }

        // Server will call this to broadcast updates to clients in a session
        public static string GetSessionGroupName(string sessionId) => $"session-{sessionId}";
    }
}
