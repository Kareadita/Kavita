using System;
using System.Collections.Generic;
using Kavita.Models.DTOs.SignalR.Bodies;

namespace Kavita.Models.DTOs.SignalR;

public sealed record RecentJobDto
{
    public required string CorrelationId { get; init; }
    public DateTime StartedUtc { get; init; }
    public DateTime EndedUtc { get; init; }
    /// <summary>
    /// Last frame per progress name before it ended
    /// </summary>
    public IList<SignalRMessageDto> Steps { get; init; } = [];
    /// <summary>
    /// False when the job stopped without every step sending ended (it threw)
    /// </summary>
    public bool Completed { get; init; }
    /// <summary>
    /// One per library scan that finished in this job
    /// </summary>
    public IList<LibraryScanEndedEventBodyDto> ScanSummaries { get; init; } = [];
}
