using System.Collections.Generic;

namespace API.DTOs.Collection;

public class DeleteCollectionsDto
{
    public IList<int> CollectionIds { get; set; }
}
