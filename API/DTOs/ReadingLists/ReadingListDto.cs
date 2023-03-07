namespace API.DTOs.ReadingLists;

public class ReadingListDto
{
    public int Id { get; init; }
    public string Title { get; set; } = default!;
    public string Summary { get; set; } = default!;
    /// <summary>
    /// Reading lists that are promoted are only done by admins
    /// </summary>
    public bool Promoted { get; set; }
    public bool CoverImageLocked { get; set; }
    /// <summary>
    /// This is used to tell the UI if it should request a Cover Image or not. If null or empty, it has not been set.
    /// </summary>
    public string CoverImage { get; set; } = string.Empty;

}
