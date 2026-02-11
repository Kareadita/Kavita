using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Kavita.API.Services.SignalR;
using Kavita.Models.DTOs.SignalR;
using Microsoft.AspNetCore.SignalR;

namespace Kavita.Services.SignalR;

public class EventHub(IHubContext<MessageHub> messageHub, IPresenceTracker presenceTracker)
    : IEventHub
{
    // TODO: When sending a message, queue the message up and on re-connect, reply the queued messages. Queue messages expire on a rolling basis (rolling array)

    public async Task SendMessageAsync(string method, SignalRMessage message, bool onlyAdmins = true)
    {
        // TODO: If libraryId and NOT onlyAdmins, then perform RBS check before sending the event

        var users = messageHub.Clients.All;
        if (onlyAdmins)
        {
            var admins = await presenceTracker.GetOnlineAdminIds();
            users = messageHub.Clients.Users(admins.Select(i => i.ToString()).ToArray());
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
        await messageHub.Clients.Users(new List<string>() {userId + string.Empty}).SendAsync(method, message);
    }

}
