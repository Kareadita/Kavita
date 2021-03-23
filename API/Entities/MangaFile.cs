
using API.Entities.Enums;

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
        /// Number of pages for the given file
        /// </summary>
        public int Pages { get; set; }
        public MangaFormat Format { get; set; }

        // Relationship Mapping
        public Chapter Chapter { get; set; }
        public int ChapterId { get; set; }
        
    }
}