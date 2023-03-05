namespace API.Entities;

public class ReadingListItem
{
    public int Id { get; init; }
    public int SeriesId { get; set; }
    public int VolumeId { get; set; }
    public int ChapterId { get; set; }
    /// <summary>
    /// Order of the chapter within a Reading List
    /// </summary>
    public int Order { get; set; }

    // Relationship
    public ReadingList ReadingList { get; set; } = null!;
    public int ReadingListId { get; set; }

    // Keep these for easy join statements
    public Series Series { get; set; } = null!;
    public Volume Volume { get; set; } = null!;
    public Chapter Chapter { get; set; } = null!;
}
