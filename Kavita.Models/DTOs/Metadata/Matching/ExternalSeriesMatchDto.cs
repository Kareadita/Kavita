using Kavita.Models.DTOs.KavitaPlus.Metadata;

namespace Kavita.Models.DTOs.Metadata.Matching;

public sealed record ExternalSeriesMatchDto
{
    public ExternalSeriesDetailDto Series { get; set; }
    public float MatchRating { get; set; }
}
