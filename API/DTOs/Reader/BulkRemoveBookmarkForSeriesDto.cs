using System.Collections.Generic;

namespace API.DTOs.Reader;

public sealed record BulkRemoveBookmarkForSeriesDto
{
    public ICollection<int> SeriesIds { get; init; } = default!;
}
