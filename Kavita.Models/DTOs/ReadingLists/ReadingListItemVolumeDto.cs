namespace Kavita.Models.DTOs.ReadingLists;
#nullable enable

public sealed record ReadingListItemVolumeDto
{
    public int Id { get; init; }
    public string Name { get; init; } = string.Empty;
    public float MinNumber { get; init; }
    public float MaxNumber { get; init; }
    public int SeriesId { get; init; }
}
