using System.Collections.Generic;

namespace API.DTOs.CollectionTags;

public class UpdateSeriesForTagDto
{
    public CollectionTagDto Tag { get; init; }
    public IEnumerable<int> SeriesIdsToRemove { get; init; }
}
