using System.Collections.Generic;
using API.DTOs.Collection;

namespace API.DTOs.CollectionTags;

public sealed record UpdateSeriesForTagDto
{
    public AppUserCollectionDto Tag { get; init; } = default!;
    public IEnumerable<int> SeriesIdsToRemove { get; init; } = default!;
}
