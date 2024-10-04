using System.Collections.Generic;

namespace API.DTOs.ReadingLists;

public class PromoteReadingListsDto
{
    public IList<int> ReadingListIds { get; init; }
    public bool Promoted { get; init; }
}
