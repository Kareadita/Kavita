using System.Collections.Generic;

namespace Kavita.Models.DTOs.Reader;

public sealed record MarkMultipleSeriesAsReadDto
{
    public IReadOnlyList<int> SeriesIds { get; init; } = default!;
}
