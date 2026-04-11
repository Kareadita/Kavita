using System.Collections.Generic;

namespace Kavita.Models.DTOs.ReadingLists;

public sealed record PromoteReadingListsDto
{
    public IList<int> ReadingListIds { get; init; }
    public bool Promoted { get; init; }
}
