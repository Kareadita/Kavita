namespace API.Entities;

public class AppUserWantToRead
{
    public int Id { get; set; }

    public required int SeriesId { get; set; }
    public virtual Series Series { get; set; }


    // Relationships
    /// <summary>
    /// Navigational Property for EF. Links to a unique AppUser
    /// </summary>
    public AppUser AppUser { get; set; } = null!;
    /// <summary>
    /// User this table of content belongs to
    /// </summary>
    public int AppUserId { get; set; }
}
