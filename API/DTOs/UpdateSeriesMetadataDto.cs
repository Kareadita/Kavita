namespace API.DTOs;

public sealed record UpdateSeriesMetadataDto
{
    public SeriesMetadataDto SeriesMetadata { get; set; } = null!;
}
