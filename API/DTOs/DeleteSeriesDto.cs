using System.Collections.Generic;

namespace API.DTOs;

public sealed record DeleteSeriesDto
{
    public IList<int> SeriesIds { get; set; } = default!;
}
