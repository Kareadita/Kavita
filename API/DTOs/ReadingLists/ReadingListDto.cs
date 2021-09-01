namespace API.DTOs.ReadingLists
{
    public class ReadingListDto
    {
        public string Title { get; set; }
        public string Summary { get; set; }
        /// <summary>
        /// Reading lists that are promoted are only done by admins
        /// </summary>
        public bool Promoted { get; set; }
        //public ICollection<ReadingListItem> Items { get; set; }
    }
}
