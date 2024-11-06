using System;
using API.Entities.Enums;
using API.Entities.Interfaces;
using API.Services.Plus;

namespace API.DTOs.Collection;
#nullable enable

public class AppUserCollectionDto : IHasCoverImage
{
    public int Id { get; init; }
    public string Title { get; set; } = default!;
    public string Summary { get; set; } = default!;
    public bool Promoted { get; set; }
    public AgeRating AgeRating { get; set; }

    /// <summary>
    /// This is used to tell the UI if it should request a Cover Image or not. If null or empty, it has not been set.
    /// </summary>
    public string? CoverImage { get; set; } = string.Empty;

    public string PrimaryColor { get; set; } = string.Empty;
    public string SecondaryColor { get; set; } = string.Empty;
    public bool CoverImageLocked { get; set; }

    /// <summary>
    /// Number of Series in the Collection
    /// </summary>
    public int ItemCount { get; set; }

    /// <summary>
    /// Owner of the Collection
    /// </summary>
    public string? Owner { get; set; }
    /// <summary>
    /// Last time Kavita Synced the Collection with an upstream source (for non Kavita sourced collections)
    /// </summary>
    public DateTime LastSyncUtc { get; set; }
    /// <summary>
    /// Who created/manages the list. Non-Kavita lists are not editable by the user, except to promote
    /// </summary>
    public ScrobbleProvider Source { get; set; } = ScrobbleProvider.Kavita;
    /// <summary>
    /// For Non-Kavita sourced collections, the url to sync from
    /// </summary>
    public string? SourceUrl { get; set; }
    /// <summary>
    /// Total number of items as of the last sync. Not applicable for Kavita managed collections.
    /// </summary>
    public int TotalSourceCount { get; set; }
    /// <summary>
    /// A <br/> separated string of all missing series
    /// </summary>
    public string? MissingSeriesFromSource { get; set; }

    public void ResetColorScape()
    {
        PrimaryColor = string.Empty;
        SecondaryColor = string.Empty;
    }
}
