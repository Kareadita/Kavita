using API.Entities.Enums;

namespace API.DTOs.Reader
{
    public class BookInfoDto
    {
        public string BookTitle { get; set; }
        public int SeriesId { get; set; }
        public int VolumeId { get; set; }
        public MangaFormat SeriesFormat { get; set; }
        public int LibraryId { get; set; }
    }
}
