using System.Collections.Generic;
using Kavita.Models.DTOs.SignalR;

namespace Kavita.API.Services.SignalR;

/// <summary>
/// Remembers the latest progress message of every job that has not ended, so a client that was not connected can catch up
/// </summary>
public interface IActivityTracker
{
    void Record(string method, SignalRMessageDto message);

    /// <summary>
    /// Latest message per job, oldest first. Rows whose Hangfire job is no longer in <paramref name="processingJobIds"/> are dropped,
    /// since a job that throws never sends ended
    /// </summary>
    IList<SignalRMessageDto> GetRunning(IReadOnlySet<string> processingJobIds);

    /// <summary>
    /// Jobs no longer in <paramref name="processingJobIds"/>, newest first
    /// </summary>
    IList<RecentJobDto> GetRecentJobs(IReadOnlySet<string> processingJobIds);

    IList<SignalRMessageDto> GetRecentEntries();
}
