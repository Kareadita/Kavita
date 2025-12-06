
using System;
using API.Entities.Interfaces;

namespace API.Entities;
#nullable enable

public class AppUserRating : IEntityDate
{
    public int Id { get; set; }

    /// <summary>
    /// A number between 0-5.0 that represents how good a series is.
    /// </summary>
    public float Rating { get; set; }

    /// <summary>
    /// If the rating has been explicitly set. Otherwise, the 0.0 rating should be ignored as it's not rated
    /// </summary>
    public bool HasBeenRated { get; set; }

    /// <summary>
    /// A short summary the user can write when giving their review.
    /// </summary>
    public string? Review { get; set; }

    public DateTime Created { get; set; }
    public DateTime CreatedUtc { get; set; }
    public DateTime LastModified { get; set; }
    public DateTime LastModifiedUtc { get; set; }

    public int SeriesId { get; set; }
    public Series Series { get; set; } = null!;

    // Relationships
    public int AppUserId { get; set; }
    public AppUser AppUser { get; set; } = null!;
}
