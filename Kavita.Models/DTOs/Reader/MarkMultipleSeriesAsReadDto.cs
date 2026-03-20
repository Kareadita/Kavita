using System.Collections.Generic;

namespace Kavita.Models.DTOs.Reader;

public sealed record MarkMultipleSeriesAsReadDto
{
    public IReadOnlyList<int> SeriesIds { get; init; } = default!;
    /// <summary>
    /// If true, generates a new reading session for the user. Based on the estimated time from the current progress
    /// till the end
    /// </summary>
    public bool GenerateReadingSession { get; init; }
}
