using System;
using Kavita.Models.Entities.Enums;
using Kavita.Models.Entities.Interfaces;
using System.ComponentModel.DataAnnotations;

namespace Kavita.Models.DTOs.Collection;
#nullable enable

public sealed record AppUserCollectionDto : IHasCoverImageDto
{
    public int Id { get; init; }
    public string Title { get; init; } = default!;
    public string? Summary { get; init; } = default!;
    public bool Promoted { get; init; }
    [EnumDataType(typeof(AgeRating))]
    public AgeRating AgeRating { get; init; }

    /// <summary>
    /// Number of Series in the Collection
    /// </summary>
    public int ItemCount { get; init; }

    /// <summary>
    /// Owner of the Collection
    /// </summary>
    public string? Owner { get; init; }
    /// <summary>
    /// Last time Kavita Synced the Collection with an upstream source (for non Kavita sourced collections)
    /// </summary>
    public DateTime LastSyncUtc { get; init; }
    /// <summary>
    /// Who created/manages the list. Non-Kavita lists are not editable by the user, except to promote
    /// </summary>
    [EnumDataType(typeof(ScrobbleProvider))]
    public ScrobbleProvider Source { get; init; } = ScrobbleProvider.Kavita;
    /// <summary>
    /// For Non-Kavita sourced collections, the url to sync from
    /// </summary>
    public string? SourceUrl { get; init; }
    /// <summary>
    /// Total number of items as of the last sync. Not applicable for Kavita managed collections.
    /// </summary>
    public int TotalSourceCount { get; init; }
    /// <summary>
    /// A <br/> separated string of all missing series
    /// </summary>
    public string? MissingSeriesFromSource { get; init; }

    public bool HasCoverImage { get; set; }
    public bool CoverImageLocked { get; init; }
}
