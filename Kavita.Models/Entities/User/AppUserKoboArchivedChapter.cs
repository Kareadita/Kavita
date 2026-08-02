using System;

namespace Kavita.Models.Entities.User;

/// <summary>
/// Per-user archive flag for Kobo removal semantics (device DELETE, eligibility loss).
/// </summary>
public class AppUserKoboArchivedChapter
{
    public int Id { get; set; }
    public int AppUserId { get; set; }
    public AppUser AppUser { get; set; } = null!;
    public int ChapterId { get; set; }
    public Chapter Chapter { get; set; } = null!;
    public DateTime LastModifiedUtc { get; set; }
}
