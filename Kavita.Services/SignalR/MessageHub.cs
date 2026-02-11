using System;
using System.Threading.Tasks;
using Kavita.API.Attributes;
using Kavita.API.Services.SignalR;
using Kavita.Common.Extensions;
using Kavita.Models.DTOs.SignalR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.SignalR;

namespace Kavita.Services.SignalR;

/// <summary>
/// Generic hub for sending messages to UI
/// </summary>
[Authorize]
[SkipDeviceTracking]
public class MessageHub : Hub
{
    private readonly IPresenceTracker _tracker;

    public MessageHub(IPresenceTracker tracker)
    {
        _tracker = tracker;
    }

    public override async Task OnConnectedAsync()
    {
        var userId = Context.User!.GetUserId();
        await _tracker.UserConnected(userId, Context.ConnectionId);

        var currentUsers = await PresenceTracker.GetOnlineUsers();
        await Clients.All.SendAsync(MessageFactory.OnlineUsers, currentUsers);


        await base.OnConnectedAsync();
    }

    public override async Task OnDisconnectedAsync(Exception? exception)
    {
        await _tracker.UserDisconnected(Context.User!.GetUserId(), Context.ConnectionId);

        var currentUsers = await PresenceTracker.GetOnlineUsers();
        await Clients.All.SendAsync(MessageFactory.OnlineUsers, currentUsers);


        await base.OnDisconnectedAsync(exception);
    }
}

