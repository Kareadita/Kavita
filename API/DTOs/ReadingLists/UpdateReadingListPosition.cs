namespace API.DTOs.ReadingLists
{
    public class UpdateReadingListPosition
    {
        public int ReadingListId { get; set; }
        public int ReadingListItemId { get; set; }
        public int FromPosition { get; set; }
        public int ToPosition { get; set; }
    }
}
