namespace API.DTOs
{
    public class BookmarkDto
    {
        public int VolumeId { get; set; }
        public int ChapterId { get; set; }
        public int PageNum { get; set; }
        public int SeriesId { get; set; }
        /// <summary>
        /// For Book reader, this can be an optional string of the id of a part marker, to help resume reading position
        /// on pages that combine multiple "chapters".
        /// </summary>
        public string BookScrollId { get; set; }
    }
}