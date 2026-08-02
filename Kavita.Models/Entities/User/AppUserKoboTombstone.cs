using System;

namespace Kavita.Models.Entities.User;

/// <summary>
/// Hard-deleted chapter removal pending Kobo <c>IsRemoved</c> delivery.
/// Retained until the device has synced the removal (no Chapter FK).
/// </summary>
public class AppUserKoboTombstone
{
    public int Id { get; set; }
    public int AppUserId { get; set; }
    public AppUser AppUser { get; set; } = null!;

    /// <summary>Original chapter id (chapter row may already be gone).</summary>
    public int ChapterId { get; set; }

    public Guid EntitlementId { get; set; }
    public string Title { get; set; } = string.Empty;
    public DateTime CreatedUtc { get; set; }
}
