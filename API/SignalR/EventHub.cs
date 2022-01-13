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
    }

    public async Task SendMessageAsync(string method, SignalRMessage message, bool onlyAdmins = true)
    {
        var users = _messageHub.Clients.All;
        if (onlyAdmins)
        {
            var admins = await _presenceTracker.GetOnlineAdmins();
            _messageHub.Clients.Users(admins);
        }

        await users.SendAsync(method, message);
    }
}
