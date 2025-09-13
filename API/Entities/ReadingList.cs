using System;
using System.Collections.Generic;
using API.Entities.Enums;
using API.Entities.Interfaces;

namespace API.Entities;

#nullable enable

/// <summary>
/// This is a collection of <see cref="ReadingListItem"/> which represent individual chapters and an order.
/// </summary>
public class ReadingList : IEntityDate, IHasCoverImage
{
    public int Id { get; init; }
    public required string Title { get; set; }
    /// <summary>
    /// A normalized string used to check if the reading list already exists in the DB
    /// </summary>
    public required string NormalizedTitle { get; set; }
    public string? Summary { get; set; }
    /// <summary>
    /// Reading lists that are promoted are only done by admins
    /// </summary>
    public bool Promoted { get; set; }
    public string? CoverImage { get; set; }
    public string? PrimaryColor { get; set; }
    public string? SecondaryColor { get; set; }
    public bool CoverImageLocked { get; set; }

    /// <summary>
    /// The highest age rating from all Series within the reading list
    /// </summary>
    /// <remarks>Introduced in v0.6</remarks>
    public required AgeRating AgeRating { get; set; } = AgeRating.Unknown;

    public ICollection<ReadingListItem> Items { get; set; } = null!;
    public DateTime Created { get; set; }
    public DateTime LastModified { get; set; }
    public DateTime CreatedUtc { get; set; }
    public DateTime LastModifiedUtc { get; set; }
    /// <summary>
    /// Minimum Year the Reading List starts
    /// </summary>
    public int StartingYear { get; set; }
    /// <summary>
    /// Minimum Month the Reading List starts
    /// </summary>
    public int StartingMonth { get; set; }
    /// <summary>
    /// Maximum Year the Reading List starts
    /// </summary>
    public int EndingYear { get; set; }
    /// <summary>
    /// Maximum Month the Reading List starts
    /// </summary>
    public int EndingMonth { get; set; }

    // Relationships
    public int AppUserId { get; set; }
    public AppUser AppUser { get; set; } = null!;

    public void ResetColorScape()
    {
        PrimaryColor = string.Empty;
        SecondaryColor = string.Empty;
    }
}
