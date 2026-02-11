using System.Collections.Generic;

namespace Kavita.Models.DTOs;

public sealed record DeleteSeriesDto
{
    public IList<int> SeriesIds { get; set; } = default!;
}
