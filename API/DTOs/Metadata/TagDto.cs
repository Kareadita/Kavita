namespace API.DTOs.Metadata;

public record TagDto
{
    public int Id { get; set; }
    public required string Title { get; set; }
}
