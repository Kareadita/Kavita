namespace Kavita.Models.DTOs;

public sealed record UpdateSeriesMetadataDto
{
    public SeriesMetadataDto SeriesMetadata { get; set; } = null!;
}
