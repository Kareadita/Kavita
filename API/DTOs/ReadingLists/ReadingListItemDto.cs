using API.Entities.Enums;

namespace API.DTOs.ReadingLists
{
    public class ReadingListItemDto
    {
        public int Order { get; init; }
        public int ChapterId { get; init; }
        public int SeriesId { get; init; }
        public string SeriesName { get; set; }
        public MangaFormat SeriesFormat { get; set; }
        public int PagesRead { get; set; }
        public int PagesTotal { get; set; }
        public string ChapterNumber { get; set; }
        public string VolumeNumber { get; set; }
        public string Title { get; set; }
    }
}
