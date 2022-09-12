using System;
using System.Threading.Tasks;
using API.Extensions;
using API.SignalR.Presence;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.SignalR;

namespace API.SignalR;

/// <summary>
/// Generic hub for sending messages to UI
/// </summary>
[Authorize]
public class MessageHub : Hub
{
    private readonly IPresenceTracker _tracker;

    public MessageHub(IPresenceTracker tracker)
    {
        _tracker = tracker;
    }

    public override async Task OnConnectedAsync()
    {
        await _tracker.UserConnected(Context.User.GetUsername(), Context.ConnectionId);

        var currentUsers = await PresenceTracker.GetOnlineUsers();
        await Clients.All.SendAsync(MessageFactory.OnlineUsers, currentUsers);


        await base.OnConnectedAsync();
    }

    public override async Task OnDisconnectedAsync(Exception exception)
    {
        await _tracker.UserDisconnected(Context.User.GetUsername(), Context.ConnectionId);

        var currentUsers = await PresenceTracker.GetOnlineUsers();
        await Clients.All.SendAsync(MessageFactory.OnlineUsers, currentUsers);


        await base.OnDisconnectedAsync(exception);
    }
}

