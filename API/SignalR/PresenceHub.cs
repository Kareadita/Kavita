using System;
using System.Threading.Tasks;
using API.Extensions;
using API.SignalR.Presence;
using Microsoft.AspNetCore.SignalR;

namespace API.SignalR
{
    public class PresenceHub : Hub
    {
        private readonly PresenceTracker _tracker;

        public PresenceHub(PresenceTracker tracker)
        {
            _tracker = tracker;
        }

        public override async Task OnConnectedAsync()
        {
            await _tracker.UserConnected(Context.User.GetUsername(), Context.ConnectionId);

            var currentUsers = await _tracker.GetOnlineUsers();
            await Clients.All.SendAsync("GetOnlineUsers", currentUsers);


        }

        public override async Task OnDisconnectedAsync(Exception exception)
        {
            await _tracker.UserDisconnected(Context.User.GetUsername(), Context.ConnectionId);

            var currentUsers = await _tracker.GetOnlineUsers();
            await Clients.All.SendAsync("GetOnlineUsers", currentUsers);

            await base.OnDisconnectedAsync(exception);
        }
    }
}
