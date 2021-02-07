using API.Entities.Enums;

namespace API.DTOs
{
    public class MangaFileDto
    {
        public string FilePath { get; set; }
        public int NumberOfPages { get; set; }
        public MangaFormat Format { get; set; }
        
    }
}