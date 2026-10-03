using System;
using System.Collections.Generic;

namespace Kavita.Models.DTOs.SignalR;

public sealed record ActivitySnapshotDto
{
    public Guid BootId { get; init; }
    public DateTime StartedUtc { get; init; }
    /// <summary>
    /// Latest progress message per job. A single job can have several rows (FileScanProgress, ScanProgress, etc) sharing one CorrelationId
    /// </summary>
    public IList<SignalRMessageDto> Running { get; init; } = [];
    /// <summary>
    /// Delayed scans, soonest first, at most 20 of <see cref="ScheduledTotal"/>
    /// </summary>
    public IList<ScheduledScanDto> Scheduled { get; init; } = [];
    public int ScheduledTotal { get; init; }
    public IList<UpcomingTaskDto> Upcoming { get; init; } = [];
    /// <summary>
    /// Jobs that finished in the last 24 hours, newest first. In memory, so empty after a restart
    /// </summary>
    public IList<RecentJobDto> RecentJobs { get; init; } = [];
    /// <summary>
    /// Info, Error and rate limit messages from the last 24 hours, newest first
    /// </summary>
    public IList<SignalRMessageDto> RecentEntries { get; init; } = [];
}
