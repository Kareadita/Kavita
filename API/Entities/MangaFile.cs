
namespace API.Entities
{
    public class MangaFile
    {
        public int Id { get; set; }
        public string FilePath { get; set; }
        // Should I just store information related to FilePath here? Reset it on anytime FilePath changes? 

        // Relationship Mapping
        public Volume Volume { get; set; }
        public int VolumeId { get; set; }
        
    }
}