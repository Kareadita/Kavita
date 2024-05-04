using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;

namespace API.DTOs.Collection;

public class DeleteCollectionsDto
{
    [Required]
    public IList<int> CollectionIds { get; set; }
}
