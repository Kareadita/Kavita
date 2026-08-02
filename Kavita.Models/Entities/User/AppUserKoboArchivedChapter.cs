using System;

namespace Kavita.Models.Entities.User;

/// <summary>
/// Per-user archive flag for Kobo removal semantics (device DELETE, eligibility loss).
/// Device-deleted archives require a profile restore; eligibility archives auto-clear when eligible again.
/// </summary>
public class AppUserKoboArchivedChapter
{
    public int Id { get; set; }
    public int AppUserId { get; set; }
    public AppUser AppUser { get; set; } = null!;
    public int ChapterId { get; set; }
    public Chapter Chapter { get; set; } = null!;
    public DateTime LastModifiedUtc { get; set; }

    /// <summary>
    /// True when the user removed the book on the device (DELETE). False for admin/library eligibility archives.
    /// </summary>
    public bool IsDeviceDeleted { get; set; }
}
