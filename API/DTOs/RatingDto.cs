using API.Entities.Enums;
using API.Entities.Metadata;
using API.Services.Plus;

namespace API.DTOs;
#nullable enable

public sealed record RatingDto
{

    public int AverageScore { get; set; }
    public int FavoriteCount { get; set; }
    public ScrobbleProvider Provider { get; set; }
    /// <inheritdoc cref="ExternalRating.Authority"/>
    public RatingAuthority Authority { get; set; } = RatingAuthority.User;
    public string? ProviderUrl { get; set; }
}
