namespace API.DTOs.Reader;

public sealed record MarkVolumeReadDto
{
    public int SeriesId { get; init; }
    public int VolumeId { get; init; }
}
