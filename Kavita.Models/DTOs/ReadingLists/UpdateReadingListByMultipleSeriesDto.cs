using System.Collections.Generic;

namespace Kavita.Models.DTOs.ReadingLists;

public sealed record UpdateReadingListByMultipleSeriesDto
{
    public int ReadingListId { get; init; }
    public IReadOnlyList<int> SeriesIds { get; init; } = default!;
}
