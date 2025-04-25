using API.Entities.Enums;

namespace API.Entities;

#nullable enable

public class AppUserChapterRating
{
    public int Id { get; set; }
    public float Rating { get; set; }
    public bool HasBeenRated { get; set; }
    public string? Review { get; set; }
    public RatingProvider Provider {get; set; }

    public int SeriesId { get; set; }
    public Series Series { get; set; } = null!;

    public int ChapterId { get; set; }
    public Chapter Chapter { get; set; } = null!;

    public int VolumeId { get; set; }
    public Volume Volume { get; set; } = null!;

    public int AppUserId { get; set; }
    public AppUser AppUser { get; set; } = null!;
}
