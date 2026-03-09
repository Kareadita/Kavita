using System.Collections.Generic;

namespace Kavita.Models.DTOs.Collection;

public class PromoteCollectionsDto
{
    public IList<int> CollectionIds { get; init; }
    public bool Promoted { get; init; }
}
