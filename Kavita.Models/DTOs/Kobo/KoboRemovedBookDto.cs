using System;

namespace Kavita.Models.DTOs.Kobo;

/// <summary>
/// A device-deleted chapter that is still eligible for Kobo sync and can be restored.
/// </summary>
public sealed record KoboRemovedBookDto
{
    public int ChapterId { get; init; }
    public int SeriesId { get; init; }
    public int LibraryId { get; init; }
    public string SeriesName { get; init; } = string.Empty;
    public string Title { get; init; } = string.Empty;
    public DateTime RemovedUtc { get; init; }
}
