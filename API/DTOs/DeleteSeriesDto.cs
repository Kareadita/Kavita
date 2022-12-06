using System.Collections.Generic;

namespace API.DTOs;

public class DeleteSeriesDto
{
    public IList<int> SeriesIds { get; set; } = default!;
}
