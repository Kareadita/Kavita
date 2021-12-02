using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace API.SignalR.Presence
{
    public interface IPresenceTracker
    {
        Task UserConnected(string username, string connectionId);
        Task UserDisconnected(string username, string connectionId);
        Task<string[]> GetOnlineAdmins();
        Task<List<string>> GetConnectionsForUser(string username);

    }

    /// <summary>
    /// This is a singleton service for tracking what users have a SignalR connection and their difference connectionIds
    /// </summary>
    public class PresenceTracker : IPresenceTracker
    {
        private readonly IUnitOfWork _unitOfWork;
        private static readonly Dictionary<string, List<string>> OnlineUsers = new Dictionary<string, List<string>>();

        public PresenceTracker(IUnitOfWork unitOfWork)
        {
            _unitOfWork = unitOfWork;
        }

        public async Task UserConnected(string username, string connectionId)
        {
            lock (OnlineUsers)
            {
                if (OnlineUsers.ContainsKey(username))
                {
                    OnlineUsers[username].Add(connectionId);
                }
                else
                {
                    OnlineUsers.Add(username, new List<string>() { connectionId });
                }
            }

            // Update the last active for the user
            var user = await _unitOfWork.UserRepository.GetUserByUsernameAsync(username);
            user.LastActive = DateTime.Now;
            await _unitOfWork.CommitAsync();
        }

        public Task UserDisconnected(string username, string connectionId)
        {
            lock (OnlineUsers)
            {
                if (!OnlineUsers.ContainsKey(username)) return Task.CompletedTask;

                OnlineUsers[username].Remove(connectionId);

                if (OnlineUsers[username].Count == 0)
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

        public async Task<string[]> GetOnlineAdmins()
        {
            string[] onlineUsers;
            lock (OnlineUsers)
            {
                onlineUsers = OnlineUsers.OrderBy(k => k.Key).Select(k => k.Key).ToArray();
            }

            var admins = await _unitOfWork.UserRepository.GetAdminUsersAsync();
            var result = admins.Select(a => a.UserName).Intersect(onlineUsers).ToArray();

            return result;
        }

        public Task<List<string>> GetConnectionsForUser(string username)
        {
            List<string> connectionIds;
            lock (OnlineUsers)
            {
                connectionIds = OnlineUsers.GetValueOrDefault(username);
            }

            return Task.FromResult(connectionIds);
        }
    }
}
