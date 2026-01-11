using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using API.Data;

namespace API.SignalR.Presence;
#nullable enable

public interface IPresenceTracker
{
    Task UserConnected(int userId, string connectionId);
    Task UserDisconnected(int userId, string connectionId);
    Task<int[]> GetOnlineAdminIds();
    Task<List<string>> GetConnectionsForUser(int userId);
}

internal sealed record ConnectionDetail
{
    public required string UserName { get; init; }
    public List<string> ConnectionIds { get; init; } = [];
    public bool IsAdmin { get; init; }
}

/// <summary>
/// This is a singleton service for tracking what users have a SignalR connection and their difference connectionIds
/// </summary>
public class PresenceTracker(IUnitOfWork unitOfWork) : IPresenceTracker
{
    private static readonly Dictionary<int, ConnectionDetail> OnlineUsers = [];

    public async Task UserConnected(int userId, string connectionId)
    {
        var user = await unitOfWork.UserRepository.GetUserByIdAsync(userId);
        if (user == null) return;

        var isAdmin = await unitOfWork.UserRepository.IsUserAdminAsync(user);
        lock (OnlineUsers)
        {
            if (OnlineUsers.TryGetValue(userId, out var detail))
            {
                detail.ConnectionIds.Add(connectionId);
            }
            else
            {
                OnlineUsers.Add(userId, new ConnectionDetail()
                {
                    UserName = user.UserName!,
                    ConnectionIds = [connectionId],
                    IsAdmin = isAdmin
                });
            }
        }
    }

    public Task UserDisconnected(int userId, string connectionId)
    {
        lock (OnlineUsers)
        {
            if (!OnlineUsers.TryGetValue(userId, out var user)) return Task.CompletedTask;

            user.ConnectionIds.Remove(connectionId);

            if (OnlineUsers[userId].ConnectionIds.Count == 0)
            {
                OnlineUsers.Remove(userId);
            }
        }
        return Task.CompletedTask;
    }

    public static Task<string[]> GetOnlineUsers()
    {
        string[] onlineUsers;
        lock (OnlineUsers)
        {
            onlineUsers = OnlineUsers
                .Select(k => k.Value.UserName)
                .Order()
                .ToArray();
        }

        return Task.FromResult(onlineUsers);
    }

    public Task<int[]> GetOnlineAdminIds()
    {
        int[] onlineUsers;
        lock (OnlineUsers)
        {
            onlineUsers = OnlineUsers.Where(pair => pair.Value.IsAdmin)
                .Select(k => k.Key)
                .Order()
                .ToArray();
        }


        return Task.FromResult(onlineUsers);
    }

    public Task<List<string>> GetConnectionsForUser(int userId)
    {
        List<string>? connectionIds;
        lock (OnlineUsers)
        {
            connectionIds = OnlineUsers.GetValueOrDefault(userId)?.ConnectionIds;
        }

        return Task.FromResult(connectionIds ?? []);
    }
}
