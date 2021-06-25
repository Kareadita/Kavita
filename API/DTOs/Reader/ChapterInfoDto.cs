namespace API.DTOs.Reader
{
    public class ChapterInfoDto
    {
        
        public string ChapterNumber { get; set; }
        public string VolumeNumber { get; set; }
        public int VolumeId { get; set; }
        public string SeriesName { get; set; }
        public string ChapterTitle { get; set; } = "";
        public int Pages { get; set; }
        public string FileName { get; set; }
        public bool IsSpecial { get; set; }
        
    }
}