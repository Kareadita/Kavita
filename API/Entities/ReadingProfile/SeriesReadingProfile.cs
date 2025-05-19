namespace API.Entities;

public class SeriesReadingProfile
{

    public int Id { get; set; }

    public int AppUserId { get; set; }
    public AppUser AppUser { get; set; }

    public int SeriesId { get; set; }
    public Series Series { get; set; }

    public int ReadingProfileId { get; set; }
    public AppUserReadingProfile ReadingProfile { get; set; }

}
