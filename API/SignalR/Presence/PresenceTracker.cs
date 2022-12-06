using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using API.Data;

namespace API.SignalR.Presence;

public interface IPresenceTracker
{
    Task UserConnected(string? username, string connectionId);
    Task UserDisconnected(string? username, string connectionId);
    Task<string[]> GetOnlineAdmins();
    Task<List<string>> GetConnectionsForUser(string username);

}

internal class ConnectionDetail
{
    public List<string> ConnectionIds { get; set; } = new List<string>();
    public bool IsAdmin { get; set; }
}

// TODO: This can respond to UserRoleUpdate events to handle online users
/// <summary>
/// This is a singleton service for tracking what users have a SignalR connection and their difference connectionIds
/// </summary>
public class PresenceTracker : IPresenceTracker
{
    private readonly IUnitOfWork _unitOfWork;
    private static readonly Dictionary<string, ConnectionDetail> OnlineUsers = new Dictionary<string, ConnectionDetail>();

    public PresenceTracker(IUnitOfWork unitOfWork)
    {
        _unitOfWork = unitOfWork;
    }

    public async Task UserConnected(string? username, string connectionId)
    {
        if (username == null) return;
        var user = await _unitOfWork.UserRepository.GetUserByUsernameAsync(username);
        if (user == null) return;
        var isAdmin = await _unitOfWork.UserRepository.IsUserAdminAsync(user);
        lock (OnlineUsers)
        {
            if (OnlineUsers.ContainsKey(username))
            {
                OnlineUsers[username].ConnectionIds.Add(connectionId);
            }
            else
            {
                OnlineUsers.Add(username, new ConnectionDetail()
                {
                    ConnectionIds = new List<string>() {connectionId},
                    IsAdmin = isAdmin
                });
            }
        }

        // Update the last active for the user
        user.LastActive = DateTime.Now;
        await _unitOfWork.CommitAsync();
    }

    public Task UserDisconnected(string? username, string connectionId)
    {
        if (username == null) return Task.CompletedTask;
        lock (OnlineUsers)
        {
            if (!OnlineUsers.ContainsKey(username)) return Task.CompletedTask;

            OnlineUsers[username].ConnectionIds.Remove(connectionId);

            if (OnlineUsers[username].ConnectionIds.Count == 0)
            {
                OnlineUsers.Remove(username);
            }
        }
        return Task.CompletedTask;
    }

    public static Task<string[]> GetOnlineUsers()
    {
        string[] onlineUsers;
        lock (OnlineUsers)
        {
            onlineUsers = OnlineUsers.OrderBy(k => k.Key).Select(k => k.Key).ToArray();
        }

        return Task.FromResult(onlineUsers);
    }

    public Task<string[]> GetOnlineAdmins()
    {
        string[] onlineUsers;
        lock (OnlineUsers)
        {
            onlineUsers = OnlineUsers.Where(pair => pair.Value.IsAdmin).OrderBy(k => k.Key).Select(k => k.Key).ToArray();
        }


        return Task.FromResult(onlineUsers);
    }

    public Task<List<string>> GetConnectionsForUser(string username)
    {
        List<string>? connectionIds;
        lock (OnlineUsers)
        {
            connectionIds = OnlineUsers.GetValueOrDefault(username)?.ConnectionIds;
        }

        return Task.FromResult(connectionIds ?? new List<string>());
    }
}
