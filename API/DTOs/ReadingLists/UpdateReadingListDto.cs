using System;
using System.ComponentModel.DataAnnotations;

namespace API.DTOs.ReadingLists;

public class UpdateReadingListDto
{
    [Required]
    public int ReadingListId { get; set; }
    public string Title { get; set; } = string.Empty;
    public string Summary { get; set; } = string.Empty;
    public bool Promoted { get; set; }
    public bool CoverImageLocked { get; set; }
}
