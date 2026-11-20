using System;

namespace Kavita.Models.DTOs;

public sealed class ChapterRestoreInfoDto
{
    public int ChapterId { get; init; }
    public int SeriesId { get; init; }
    public string SeriesTitle { get; init; } = string.Empty;
    public string Title { get; init; } = string.Empty;
    public DateTime RemovedAtUtc { get; init; }
}
