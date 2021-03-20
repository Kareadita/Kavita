namespace API.DTOs
{
    public class InProgressChapterDto
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
        public int SeriesId { get; init; }
        public int LibraryId { get; init; }
        public string SeriesName { get; init; }
        public int VolumeId { get; init; }
        
    }
}