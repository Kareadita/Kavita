using API.Entities.Enums;

namespace API.DTOs.Statistics;

public class MangaFormatCountDto : ICount<MangaFormat>
{
    public MangaFormat Value { get; set; }
    public int Count { get; set; }
}
