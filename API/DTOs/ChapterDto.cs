using System.Collections.Generic;

namespace API.DTOs
{
    public class ChapterDto
    {
        public int Id { get; init; }
        /// <summary>
        /// Range of chapters. Chapter 2-4 -> "2-4". Chapter 2 -> "2".
        /// </summary>
        public string Range { get; init; }
        /// <summary>
        /// Smallest number of the Range. 
        /// </summary>
        public string Number { get; init; }
        /// <summary>
        /// Total number of pages in all MangaFiles
        /// </summary>
        public int Pages { get; init; }
        /// <summary>
        /// The files that represent this Chapter
        /// </summary>
        public ICollection<MangaFileDto> Files { get; init; }
        /// <summary>
        /// Calculated at API time. Number of pages read for this Chapter for logged in user.
        /// </summary>
        public int PagesRead { get; set; }
        public int VolumeId { get; init; }
    }
}