using System;

namespace Kavita.Models.DTOs.SignalR;

public sealed record ScheduledScanDto
{
    public required string JobId { get; init; }
    /// <summary>
    /// Null when every library will be scanned
    /// </summary>
    public int? LibraryId { get; init; }
    public int? SeriesId { get; init; }
    public DateTime RunAtUtc { get; init; }
}
