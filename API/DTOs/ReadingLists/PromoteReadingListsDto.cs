using System.Collections.Generic;

namespace API.DTOs.ReadingLists;

public sealed record PromoteReadingListsDto
{
    public IList<int> ReadingListIds { get; init; }
    public bool Promoted { get; init; }
}
