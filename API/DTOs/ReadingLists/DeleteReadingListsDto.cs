using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;

namespace API.DTOs.ReadingLists;

public class DeleteReadingListsDto
{
    [Required]
    public IList<int> ReadingListIds { get; set; }
}
