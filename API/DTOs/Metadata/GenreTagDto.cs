namespace API.DTOs.Metadata;

public sealed record GenreTagDto
{
    public int Id { get; set; }
    public required string Title { get; set; }
}
