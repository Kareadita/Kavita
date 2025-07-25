
using System;
using API.Entities.Interfaces;

namespace API.Entities;

/// <summary>
/// Represents the progress a single user has on a given Chapter.
/// </summary>
public class AppUserProgress : IEntityDate
{
    /// <summary>
    /// Id of Entity
    /// </summary>
    public int Id { get; set; }
    /// <summary>
    /// Pages Read for given Chapter
    /// </summary>
    public int PagesRead { get; set; }
    /// <summary>
    /// Volume belonging to Chapter
    /// </summary>
    public int VolumeId { get; set; }
    /// <summary>
    /// Series belonging to Chapter
    /// </summary>
    public int SeriesId { get; set; }
    /// <summary>
    /// Library belonging to Chapter
    /// </summary>
    public int LibraryId { get; set; }
    /// <summary>
    /// Chapter
    /// </summary>
    public int ChapterId { get; set; }
    /// <summary>
    /// For Book Reader, represents the nearest passed anchor on the screen that can be used to resume scroll point
    /// on next load
    /// </summary>
    public string? BookScrollId { get; set; }
    /// <summary>
    /// When this was first created
    /// </summary>
    public DateTime Created { get; set; }
    /// <summary>
    /// Last date this was updated
    /// </summary>
    public DateTime LastModified { get; set; }

    public DateTime CreatedUtc { get; set; }
    public DateTime LastModifiedUtc { get; set; }

    // Relationships
    /// <summary>
    /// Navigational Property for EF. Links to a unique AppUser
    /// </summary>
    public AppUser AppUser { get; set; } = null!;
    /// <summary>
    /// User this progress belongs to
    /// </summary>
    public int AppUserId { get; set; }

    public void MarkModified()
    {
        LastModified = DateTime.Now;
        LastModifiedUtc = DateTime.UtcNow;
    }
}
