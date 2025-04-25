using API.Services.Plus;

namespace API.Entities;

#nullable enable

public enum ChapterRatingProvider
{
    Kavita = 0,
    AniList = 1,
    Mal = 2,
    CbrUser = 3,
    CbrCritic = 4,
}

public class AppUserChapterRating
{

    public int Id { get; set; }
    public float Rating { get; set; }
    public bool HasBeenRated { get; set; }
    public string? Review { get; set; }
    public ChapterRatingProvider Provider {get; set; }

    public int SeriesId { get; set; }
    public Series Series { get; set; } = null!;

    public int ChapterId { get; set; }
    public Chapter Chapter { get; set; } = null!;

    public int VolumeId { get; set; }
    public Volume Volume { get; set; } = null!;

    public int AppUserId { get; set; }
    public AppUser AppUser { get; set; } = null!;
}
