namespace Kavita.Models.Entities.User;

/// <summary>
/// Durable per-user Kobo sync cursor: chapters already sent to the device.
/// </summary>
public class AppUserKoboSyncedChapter
{
    public int Id { get; set; }
    public int AppUserId { get; set; }
    public AppUser AppUser { get; set; } = null!;
    public int ChapterId { get; set; }
    public Chapter Chapter { get; set; } = null!;
}
