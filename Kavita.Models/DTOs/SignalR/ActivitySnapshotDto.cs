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
}
