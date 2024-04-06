using System.Collections.Generic;

namespace API.DTOs.Collection;

public class PromoteCollectionsDto
{
    public IList<int> CollectionIds { get; init; }
    public bool Promoted { get; init; }
}
