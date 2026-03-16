using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Kavita.API.Database;
using Kavita.API.Services.SignalR;
using Kavita.Models.DTOs.SignalR;
using Microsoft.AspNetCore.SignalR;

namespace Kavita.Services.SignalR;

public class EventHub(IHubContext<MessageHub> messageHub, IPresenceTracker presenceTracker, IUnitOfWork unitOfWork)
    : IEventHub
{
    // TODO: When sending a message, queue the message up and on re-connect, reply the queued messages. Queue messages expire on a rolling basis (rolling array)

    public async Task SendMessageAsync(string method, SignalRMessage message, bool onlyAdmins = true, CancellationToken ct = default)
    {
        var users = messageHub.Clients.All;
        if (onlyAdmins)
        {
            var admins = await presenceTracker.GetOnlineAdminIds();
            users = messageHub.Clients.Users(admins.Select(i => i.ToString()).ToArray());
        }
        else
        {
            users = await FilterClientsIfNeeded(users, message, ct);
        }

        await users.SendAsync(method, message, cancellationToken: ct);
    }

    private async Task<IClientProxy> FilterClientsIfNeeded(IClientProxy proxy, SignalRMessage message, CancellationToken ct)
    {
        var libraryId = GetBodyProperty<int?>(message.Body, "LibraryId");
        var seriesId = GetBodyProperty<int?>(message.Body, "SeriesId");

        if (!libraryId.HasValue && !seriesId.HasValue) return proxy;

        var admins = await presenceTracker.GetOnlineAdminIds();
        var nonAdmins = await presenceTracker.GetOnlineUserIds();

        List<int> usersWithAccess = [];

        if (seriesId.HasValue)
        {
            foreach (var user in nonAdmins)
            {
                if (await unitOfWork.UserRepository.HasAccessToSeries(user, seriesId.Value, ct))
                    usersWithAccess.Add(user);
            }
        }
        else if (libraryId.HasValue)
        {
            foreach (var user in nonAdmins)
            {
                if (await unitOfWork.UserRepository.HasAccessToLibrary(user, libraryId.Value, ct))
                    usersWithAccess.Add(user);
            }
        }

        usersWithAccess.AddRange(admins);

        return messageHub.Clients.Users(usersWithAccess.Select(i => i.ToString()).ToArray());
    }

    private static T? GetBodyProperty<T>(object? body, string propertyName)
    {
        if (body is null) return default;

        var value = body.GetType()
            .GetProperty(propertyName)
            ?.GetValue(body);

        return value is T typed ? typed : default;
    }


    /// <summary>
    /// Sends a message directly to a user if they are connected
    /// </summary>
    /// <param name="method"></param>
    /// <param name="message"></param>
    /// <param name="userId"></param>
    /// <param name="ct"></param>
    /// <returns></returns>
    public async Task SendMessageToAsync(string method, SignalRMessage message, int userId, CancellationToken ct = default)
    {
        await messageHub.Clients.Users([userId + string.Empty]).SendAsync(method, message, cancellationToken: ct);
    }

}
