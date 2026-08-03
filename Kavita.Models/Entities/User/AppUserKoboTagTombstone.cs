using System;

namespace Kavita.Models.Entities.User;

/// <summary>
/// Deleted Reading List / Collection pending Kobo <c>DeletedTag</c> delivery.
/// One-shot per user: removed after the device sync emits the delete.
/// </summary>
public class AppUserKoboTagTombstone
{
    public int Id { get; set; }
    public int AppUserId { get; set; }
    public AppUser AppUser { get; set; } = null!;

    /// <summary>Stable Tag UUID (<c>readinglist:{id}</c> / <c>collection:{id}</c>).</summary>
    public Guid TagId { get; set; }

    public DateTime LastModifiedUtc { get; set; }
}
