using System.Collections.Generic;
using Kavita.Models.DTOs.Collection;

namespace Kavita.Models.DTOs.CollectionTags;

public sealed record UpdateSeriesForTagDto
{
    public AppUserCollectionDto Tag { get; init; } = default!;
    public IEnumerable<int> SeriesIdsToRemove { get; init; } = default!;
}
