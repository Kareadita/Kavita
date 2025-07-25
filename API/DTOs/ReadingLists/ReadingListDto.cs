using System;
using API.Entities.Enums;
using API.Entities.Interfaces;

namespace API.DTOs.ReadingLists;
#nullable enable

public sealed record ReadingListDto : IHasCoverImage
{
    public int Id { get; init; }
    public string Title { get; set; } = default!;
    public string Summary { get; set; } = default!;
    /// <summary>
    /// Reading lists that are promoted are only done by admins
    /// </summary>
    public bool Promoted { get; set; }
    public bool CoverImageLocked { get; set; }
    /// <summary>
    /// This is used to tell the UI if it should request a Cover Image or not. If null or empty, it has not been set.
    /// </summary>
    public string? CoverImage { get; set; } = string.Empty;

    public string? PrimaryColor { get; set; } = string.Empty;
    public string? SecondaryColor { get; set; } = string.Empty;

    /// <summary>
    /// Number of Items in the Reading List
    /// </summary>
    public int ItemCount { get; set; }

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
    /// <summary>
    /// The highest age rating from all Series within the reading list
    /// </summary>
    public required AgeRating AgeRating { get; set; } = AgeRating.Unknown;

    /// <summary>
    /// Username of the User that owns (in the case of a promoted list)
    /// </summary>
    public string OwnerUserName { get; set; }

    public void ResetColorScape()
    {
        PrimaryColor = string.Empty;
        SecondaryColor = string.Empty;
    }

}
