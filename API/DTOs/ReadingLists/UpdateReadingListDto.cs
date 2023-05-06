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
    public int StartingMonth { get; set; } = 0;
    public int StartingYear { get; set; } = 0;
    public int EndingMonth { get; set; } = 0;
    public int EndingYear { get; set; } = 0;

}
