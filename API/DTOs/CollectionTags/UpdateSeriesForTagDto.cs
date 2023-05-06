using System.Collections.Generic;

namespace API.DTOs.CollectionTags;

public class UpdateSeriesForTagDto
{
    public CollectionTagDto Tag { get; init; } = default!;
    public IEnumerable<int> SeriesIdsToRemove { get; init; } = default!;
}
