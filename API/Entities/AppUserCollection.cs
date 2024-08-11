using System;
using System.Collections.Generic;
using API.Entities.Enums;
using API.Entities.Interfaces;
using API.Services.Plus;


namespace API.Entities;

/// <summary>
/// Represents a Collection of Series for a given User
/// </summary>
public class AppUserCollection : IEntityDate, IHasCoverImage
{
    public int Id { get; set; }
    public required string Title { get; set; }
    /// <summary>
    /// A normalized string used to check if the collection already exists in the DB
    /// </summary>
    public required string NormalizedTitle { get; set; }
    public string? Summary { get; set; }
    /// <summary>
    /// Reading lists that are promoted are only done by admins
    /// </summary>
    public bool Promoted { get; set; }
    public string? CoverImage { get; set; }
    public string PrimaryColor { get; set; }
    public string SecondaryColor { get; set; }
    public bool CoverImageLocked { get; set; }
    /// <summary>
    /// The highest age rating from all Series within the collection
    /// </summary>
    public required AgeRating AgeRating { get; set; } = AgeRating.Unknown;
    public ICollection<Series> Items { get; set; } = null!;
    public DateTime Created { get; set; }
    public DateTime LastModified { get; set; }
    public DateTime CreatedUtc { get; set; }
    public DateTime LastModifiedUtc { get; set; }

    // Sync stuff for Kavita+
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

    // Relationship
    public AppUser AppUser { get; set; } = null!;
    public int AppUserId { get; set; }
}
