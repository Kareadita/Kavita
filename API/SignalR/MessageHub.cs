﻿using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.SignalR;

namespace API.SignalR
{

    [Authorize]
    public class MessageHub : Hub
    {
        private static readonly HashSet<string> _connections = new HashSet<string>();

        public static bool IsConnected
        {
            get
            {
                lock (_connections)
                {
                    return _connections.Count != 0;
                }
            }
        }

        public override async Task OnConnectedAsync()
        {
            lock (_connections)
            {
                _connections.Add(Context.ConnectionId);
            }

            await base.OnConnectedAsync();
        }

        public override async Task OnDisconnectedAsync(Exception exception)
        {
            lock (_connections)
            {
                _connections.Remove(Context.ConnectionId);
            }

            await base.OnDisconnectedAsync(exception);
        }

    }
}
