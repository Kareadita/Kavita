namespace API.DTOs.Filtering;

public sealed record LanguageDto
{
    public required string IsoCode { get; set; }
    public required string Title { get; set; }
}
