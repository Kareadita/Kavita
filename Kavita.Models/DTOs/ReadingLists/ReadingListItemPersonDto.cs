namespace Kavita.Models.DTOs.ReadingLists;
#nullable enable

public sealed record ReadingListItemPersonDto
{
    public int Id { get; init; }
    public required string Name { get; init; }
}
