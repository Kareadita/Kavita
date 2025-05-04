using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;

namespace API.DTOs.ReadingLists;

public sealed record DeleteReadingListsDto
{
    [Required]
    public IList<int> ReadingListIds { get; set; }
}
