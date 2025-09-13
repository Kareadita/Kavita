using System.Collections.Generic;

namespace API.DTOs.Reader;

public sealed record MarkMultipleSeriesAsReadDto
{
    public IReadOnlyList<int> SeriesIds { get; init; } = default!;
}
