using System.Collections.Generic;

namespace Kavita.Models.DTOs.Reader;

public sealed record BulkRemoveBookmarkForSeriesDto
{
    public ICollection<int> SeriesIds { get; init; } = default!;
}
