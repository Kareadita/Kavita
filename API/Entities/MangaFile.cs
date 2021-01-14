
namespace API.Entities
{
    public class MangaFile
    {
        public int Id { get; set; }
        public string FilePath { get; set; }
        /// <summary>
        /// Do not expect this to be set. If this MangaFile represents a volume file, this will be null.
        /// </summary>
        public int Chapter { get; set; } 
        /// <summary>
        /// Number of pages for the given file
        /// </summary>
        public int NumberOfPages { get; set; }
        public MangaFormat Format { get; set; }

        // Relationship Mapping
        public Volume Volume { get; set; }
        public int VolumeId { get; set; }
        
    }
}