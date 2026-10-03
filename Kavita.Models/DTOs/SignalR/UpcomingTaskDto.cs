using System;

namespace Kavita.Models.DTOs.SignalR;

public sealed record UpcomingTaskDto
{
    public required string TaskId { get; init; }
    public required string Cron { get; init; }
    public DateTime NextRunUtc { get; init; }
}
