
namespace API.Entities
{
    public class MangaFile
    {
        public int Id { get; set; }
        /// <summary>
        /// Absolute path to the archive file
        /// </summary>
        public string FilePath { get; set; }
        /// <summary>
        /// Used to track if multiple MangaFiles (archives) represent a single Volume. If only one volume file, this will be 0.
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