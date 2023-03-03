﻿using System.Linq;
using System;
using System.Threading.Tasks;
using API.Data;
using API.SignalR.Presence;
using Microsoft.AspNetCore.SignalR;

namespace API.SignalR;

/// <summary>
/// Responsible for ushering events to the UI and allowing simple DI hook to send data
/// </summary>
public interface IEventHub
{
    Task SendMessageAsync(string method, SignalRMessage message, bool onlyAdmins = true);
    Task SendMessageToAsync(string method, SignalRMessage message, int userId);
}

public class EventHub : IEventHub
{
    private readonly IHubContext<MessageHub> _messageHub;
    private readonly IPresenceTracker _presenceTracker;
    private readonly IUnitOfWork _unitOfWork;

    public EventHub(IHubContext<MessageHub> messageHub, IPresenceTracker presenceTracker, IUnitOfWork unitOfWork)
    {
        _messageHub = messageHub;
        _presenceTracker = presenceTracker;
        _unitOfWork = unitOfWork;

        // TODO: When sending a message, queue the message up and on re-connect, reply the queued messages. Queue messages expire on a rolling basis (rolling array)
    }

    public async Task SendMessageAsync(string method, SignalRMessage message, bool onlyAdmins = true)
    {
        // TODO: If libraryId and NOT onlyAdmins, then perform RBS check before sending the event

        var users = _messageHub.Clients.All;
        if (onlyAdmins)
        {
            var admins = await _presenceTracker.GetOnlineAdminIds();
            users = _messageHub.Clients.Users(admins.Select(i => i.ToString()).ToArray());
        }


        await users.SendAsync(method, message);
    }

    /// <summary>
    /// Sends a message directly to a user if they are connected
    /// </summary>
    /// <param name="method"></param>
    /// <param name="message"></param>
    /// <param name="userId"></param>
    /// <returns></returns>
    public async Task SendMessageToAsync(string method, SignalRMessage message, int userId)
    {
        var user = await _unitOfWork.UserRepository.GetUserByIdAsync(userId) ?? throw new InvalidOperationException();
        await _messageHub.Clients.User(user.UserName!).SendAsync(method, message);
    }

}
