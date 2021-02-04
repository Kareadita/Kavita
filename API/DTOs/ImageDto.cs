namespace API.DTOs
{
    public class ImageDto
    {
        public int Page { get; set; }
        public string Filename { get; init; }
        public string FullPath { get; init; }
        public int Width { get; init; }
        public int Height { get; init; }
        public string Format { get; init; }
        public byte[] Content { get; init; }
        public int Chapter { get; set; }
        public string MangaFileName { get; set; }
    }
}