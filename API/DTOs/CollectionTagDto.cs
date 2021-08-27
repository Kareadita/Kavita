namespace API.DTOs
{
    public class CollectionTagDto
    {
        public int Id { get; set; }
        public string Title { get; set; }
        public string Summary { get; set; }
        public bool Promoted { get; set; }
        public bool CoverImageLocked { get; set; }
    }
}
