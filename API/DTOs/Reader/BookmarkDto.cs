namespace API.DTOs.Reader
{
    public class BookmarkDto
    {
        public int Id { get; set; }
        public int Page { get; set; }
        public int VolumeId { get; set; }
        public int SeriesId { get; set; }
        public int ChapterId { get; set; }
    }
}
