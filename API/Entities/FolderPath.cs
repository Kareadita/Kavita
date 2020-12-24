namespace API.Entities
{
    public class FolderPath
    {
        public int Id { get; set; }
        public string Path { get; set; }
        public Library Library { get; set; }
        public int LibraryId { get; set; }
    }
}