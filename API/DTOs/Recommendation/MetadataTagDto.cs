namespace API.DTOs.Recommendation;

public sealed record MetadataTagDto
{
    public string Name { get; set; }
    public string Description { get; private set; }
    public int? Rank { get; private set; }
    public bool IsGeneralSpoiler { get; private set; }
    public bool IsMediaSpoiler { get; private set; }
    public bool IsAdult { get; private set; }
}
