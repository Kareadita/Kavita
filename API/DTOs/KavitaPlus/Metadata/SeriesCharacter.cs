namespace API.DTOs.KavitaPlus.Metadata;

public class SeriesCharacter
{
    public string Name { get; set; }
    public required string Description { get; set; }
    public required string Url { get; set; }
    public string? ImageUrl { get; set; }
}
