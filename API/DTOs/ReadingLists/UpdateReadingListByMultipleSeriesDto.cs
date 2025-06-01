using System.Collections.Generic;

namespace API.DTOs.ReadingLists;

public sealed record UpdateReadingListByMultipleSeriesDto
{
    public int ReadingListId { get; init; }
    public IReadOnlyList<int> SeriesIds { get; init; } = default!;
}
