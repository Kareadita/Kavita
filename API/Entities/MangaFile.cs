
namespace API.Entities
{
    public class MangaFile
    {
        public int Id { get; set; }
        public string FilePath { get; set; }

        // Relationship Mapping
        public Volume Volume { get; set; }
        public int VolumeId { get; set; }
        
    }
}