namespace Kavita.Models.DTOs.ReadingLists;

public sealed record ReadingListTagDto
{
    public int Id { get; set; }
    public required string Title { get; set; }
    public required string NormalizedTitle { get; set; }
}
