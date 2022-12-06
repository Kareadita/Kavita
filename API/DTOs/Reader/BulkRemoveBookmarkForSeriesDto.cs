using System.Collections.Generic;

namespace API.DTOs.Reader;

public class BulkRemoveBookmarkForSeriesDto
{
    public ICollection<int> SeriesIds { get; init; } = default!;
}
