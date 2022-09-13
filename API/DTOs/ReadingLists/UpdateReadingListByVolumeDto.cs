namespace API.DTOs.ReadingLists;

public class UpdateReadingListByVolumeDto
{
    public int VolumeId { get; init; }
    public int SeriesId { get; init; }
    public int ReadingListId { get; init; }
}
