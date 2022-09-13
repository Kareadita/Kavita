
namespace API.Entities;

public class AppUserRating
{
    public int Id { get; set; }
    /// <summary>
    /// A number between 0-5 that represents how good a series is.
    /// </summary>
    public int Rating { get; set; }
    /// <summary>
    /// A short summary the user can write when giving their review.
    /// </summary>
    public string Review { get; set; }
    public int SeriesId { get; set; }


    // Relationships
    public int AppUserId { get; set; }
    public AppUser AppUser { get; set; }
}
