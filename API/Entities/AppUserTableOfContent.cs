using System;
using API.Entities.Interfaces;

namespace API.Entities;

/// <summary>
/// A personal table of contents for a given user linked with a given book
/// </summary>
public class AppUserTableOfContent : IEntityDate
{
    public int Id { get; set; }

    /// <summary>
    /// The page to bookmark
    /// </summary>
    public required int PageNumber { get; set; }
    /// <summary>
    /// The title of the bookmark. Defaults to Page {PageNumber} if not set
    /// </summary>
    public required string Title { get; set; }

    public required int SeriesId { get; set; }
    public virtual Series Series { get; set; }

    public required int ChapterId { get; set; }
    public virtual Chapter Chapter { get; set; }

    public int VolumeId { get; set; }
    public int LibraryId { get; set; }
    /// <summary>
    /// For Book Reader, represents the nearest passed anchor on the screen that can be used to resume scroll point. If empty, the ToC point is the beginning of the page
    /// </summary>
    public string? BookScrollId { get; set; }

    public DateTime Created { get; set; }
    public DateTime CreatedUtc { get; set; }
    public DateTime LastModified { get; set; }
    public DateTime LastModifiedUtc { get; set; }

    // Relationships
    /// <summary>
    /// Navigational Property for EF. Links to a unique AppUser
    /// </summary>
    public AppUser AppUser { get; set; } = null!;
    /// <summary>
    /// User this table of content belongs to
    /// </summary>
    public int AppUserId { get; set; }
}
