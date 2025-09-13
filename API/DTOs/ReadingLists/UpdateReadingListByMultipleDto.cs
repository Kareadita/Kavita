using System.Collections.Generic;

namespace API.DTOs.ReadingLists;

public sealed record UpdateReadingListByMultipleDto
{
    public int SeriesId { get; init; }
    public int ReadingListId { get; init; }
    public IReadOnlyList<int> VolumeIds { get; init; } = default!;
    public IReadOnlyList<int> ChapterIds { get; init; } = default!;
}
