using System;
using System.Collections.Generic;
using System.Threading.Tasks;
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
        private static readonly HashSet<string> Connections = new HashSet<string>();

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

            await base.OnConnectedAsync();
        }

        public override async Task OnDisconnectedAsync(Exception exception)
        {
            lock (Connections)
            {
                Connections.Remove(Context.ConnectionId);
            }

            await base.OnDisconnectedAsync(exception);
        }
    }
}
