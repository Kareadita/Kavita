using System;
using System.Collections.Generic;

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
    public int SeriesAdded { get; init; }
    public int SeriesRemoved { get; init; }
}
