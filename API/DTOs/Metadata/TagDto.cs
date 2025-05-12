namespace API.DTOs.Metadata;

public sealed record TagDto
{
    public int Id { get; set; }
    public required string Title { get; set; }
}
