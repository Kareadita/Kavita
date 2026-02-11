using System.Collections.Generic;
using System.Threading.Tasks;

namespace Kavita.API.Services.SignalR;

public interface IPresenceTracker
{
    Task UserConnected(int userId, string connectionId);
    Task UserDisconnected(int userId, string connectionId);
    Task<int[]> GetOnlineAdminIds();
    Task<List<string>> GetConnectionsForUser(int userId);
}
