using System.Collections.Generic;

namespace API.DTOs.Reader;

public class MarkMultipleSeriesAsReadDto
{
    public IReadOnlyList<int> SeriesIds { get; init; } = default!;
}
