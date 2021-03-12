using API.Entities.Enums;

namespace API.DTOs
{
    public class MangaFileDto
    {
        public string FilePath { get; set; }
        public int NumberOfPages { get; set; } // TODO: Refactor to Pages
        public MangaFormat Format { get; set; }
        
    }
}