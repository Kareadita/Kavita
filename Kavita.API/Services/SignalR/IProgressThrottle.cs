using System;
using System.Threading.Tasks;
using Kavita.Models.DTOs.SignalR;

namespace Kavita.API.Services.SignalR;

/// <summary>
/// Limits how often "updated" progress messages for the same job reach clients
/// </summary>
public interface IProgressThrottle
{
    /// <summary>
    /// Invokes <paramref name="send"/> now, later with only the newest pending update, or not at all when a newer one replaces it.
    /// Started, ended and step changes (a new <see cref="SignalRMessageDto.Code"/>) are always sent immediately
    /// </summary>
    Task SendAsync(SignalRMessageDto message, Func<Task> send);
}
