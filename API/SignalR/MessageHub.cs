using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using API.Data;
using API.Extensions;
using API.SignalR.Presence;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.SignalR;

namespace API.SignalR
{

    /// <summary>
    /// Generic hub for sending messages to UI
    /// </summary>
    [Authorize]
    public class MessageHub : Hub
    {
        private readonly IPresenceTracker _tracker;
        private static readonly HashSet<string> Connections = new HashSet<string>();

        public MessageHub(IPresenceTracker tracker)
        {
            _tracker = tracker;
        }

        public static bool IsConnected
        {
            get
            {
                lock (Connections)
                {
                    return Connections.Count != 0;
                }
            }
        }

        public override async Task OnConnectedAsync()
        {

            lock (Connections)
            {
                Connections.Add(Context.ConnectionId);
            }

            await _tracker.UserConnected(Context.User.GetUsername(), Context.ConnectionId);

            var currentUsers = await PresenceTracker.GetOnlineUsers();
            await Clients.All.SendAsync(SignalREvents.OnlineUsers, currentUsers);


            await base.OnConnectedAsync();
        }

        public override async Task OnDisconnectedAsync(Exception exception)
        {
            lock (Connections)
            {
                Connections.Remove(Context.ConnectionId);
            }

            await _tracker.UserDisconnected(Context.User.GetUsername(), Context.ConnectionId);

            var currentUsers = await PresenceTracker.GetOnlineUsers();
            await Clients.All.SendAsync(SignalREvents.OnlineUsers, currentUsers);


            await base.OnDisconnectedAsync(exception);
        }
    }
}
