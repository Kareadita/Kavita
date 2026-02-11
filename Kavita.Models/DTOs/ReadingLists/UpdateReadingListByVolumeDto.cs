namespace Kavita.Models.DTOs.ReadingLists;

public sealed record UpdateReadingListByVolumeDto
{
    public int VolumeId { get; init; }
    public int SeriesId { get; init; }
    public int ReadingListId { get; init; }
}
