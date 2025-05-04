namespace API.DTOs.ReadingLists;

public sealed record CreateReadingListDto
{
    public string Title { get; init; } = default!;
}
