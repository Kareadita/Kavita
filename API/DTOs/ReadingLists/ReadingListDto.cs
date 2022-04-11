namespace API.DTOs.ReadingLists
{
    public class ReadingListDto
    {
        public int Id { get; init; }
        public string Title { get; set; }
        public string Summary { get; set; }
        /// <summary>
        /// Reading lists that are promoted are only done by admins
        /// </summary>
        public bool Promoted { get; set; }
        public bool CoverImageLocked { get; set; }
    }
}
