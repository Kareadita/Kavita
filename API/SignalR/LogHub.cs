using System;
using System.Threading.Tasks;
using API.Extensions;
using API.SignalR.Presence;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.SignalR;

namespace API.SignalR;
#nullable enable

public interface ILogHub : Serilog.Sinks.AspNetCore.SignalR.Interfaces.IHub
{
}

[Authorize]
public class LogHub : Hub<ILogHub>
{
    private readonly IEventHub _eventHub;
    private readonly IPresenceTracker _tracker;

    public LogHub(IEventHub eventHub, IPresenceTracker tracker)
    {
        _eventHub = eventHub;
        _tracker = tracker;
    }


    public override async Task OnConnectedAsync()
    {
        await _tracker.UserConnected(Context.User!.GetUserId(), Context.ConnectionId);
        await base.OnConnectedAsync();
    }

    public override async Task OnDisconnectedAsync(Exception? exception)
    {
        await _tracker.UserDisconnected(Context.User!.GetUserId(), Context.ConnectionId);
        await base.OnDisconnectedAsync(exception);
    }

    public async Task SendLogAsString(string message)
    {
        await _eventHub.SendMessageAsync("LogString", new SignalRMessage()
        {
            Body = message,
            EventType = "LogString",
            Name = "LogString",
        }, true);
    }

    public async Task SendLogAsObject(object messageObject)
    {
        await _eventHub.SendMessageAsync("LogObject", new SignalRMessage()
        {
            Body = messageObject,
            EventType = "LogString",
            Name = "LogString",
        }, true);
    }
}
