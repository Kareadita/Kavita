using API.DTOs.KavitaPlus.Metadata;

namespace API.DTOs.Metadata.Matching;

public sealed record ExternalSeriesMatchDto
{
    public ExternalSeriesDetailDto Series { get; set; }
    public float MatchRating { get; set; }
}
