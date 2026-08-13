using System;
using Kavita.Models.Entities.Enums;
using Kavita.Models.Entities.Metadata;
using System.ComponentModel.DataAnnotations;

namespace Kavita.Models.DTOs;
#nullable enable

public sealed record RatingDto
{

    /// <summary>
    /// Normalized score 0-100
    /// </summary>
    public int AverageScore { get; set; }
    [Obsolete("Not used as of v0.9.1")]
    public int FavoriteCount { get; set; }
    [EnumDataType(typeof(ScrobbleProvider))]
    public ScrobbleProvider Provider { get; set; }
    /// <inheritdoc cref="ExternalRating.Authority"/>
    [EnumDataType(typeof(RatingAuthority))]
    public RatingAuthority Authority { get; set; } = RatingAuthority.User;
    public string? ProviderUrl { get; set; }
}