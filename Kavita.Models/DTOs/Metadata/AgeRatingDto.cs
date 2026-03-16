using Kavita.Models.Entities.Enums;

namespace Kavita.Models.DTOs.Metadata;

public sealed record AgeRatingDto
{
    public AgeRating Value { get; set; }
    public required string Title { get; set; }
}
