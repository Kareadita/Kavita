using System;
using System.Collections.Generic;
using API.Entities.Interfaces;

namespace API.Entities;

/// <summary>
/// This is a collection of <see cref="ReadingListItem"/> which represent individual chapters and an order.
/// </summary>
public class ReadingList : IEntityDate
{
    public int Id { get; init; }
    public string Title { get; set; }
    /// <summary>
    /// A normalized string used to check if the reading list already exists in the DB
    /// </summary>
    public string NormalizedTitle { get; set; }
    public string Summary { get; set; }
    /// <summary>
    /// Reading lists that are promoted are only done by admins
    /// </summary>
    public bool Promoted { get; set; }
    /// <summary>
    /// Absolute path to the (managed) image file
    /// </summary>
    /// <remarks>The file is managed internally to Kavita's APPDIR</remarks>
    public string CoverImage { get; set; }
    public bool CoverImageLocked { get; set; }

    public ICollection<ReadingListItem> Items { get; set; }
    public DateTime Created { get; set; }
    public DateTime LastModified { get; set; }

    // Relationships
    public int AppUserId { get; set; }
    public AppUser AppUser { get; set; }

}
