namespace API.DTOs.ReadingLists;

public class UpdateReadingListDto
{
    public int ReadingListId { get; set; }
    public string Title { get; set; }
    public string Summary { get; set; }
    public bool Promoted { get; set; }
    public bool CoverImageLocked { get; set; }
}
