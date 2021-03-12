using API.Entities.Enums;

namespace API.DTOs
{
    public class MangaFileDto
    {
        public string FilePath { get; init; }
        public int NumberOfPages { get; init; }
        public MangaFormat Format { get; init; }
        
    }
}