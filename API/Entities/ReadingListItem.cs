using Microsoft.EntityFrameworkCore;

namespace API.Entities
{
    //[Index(nameof(SeriesId), nameof(VolumeId), nameof(ChapterId), IsUnique = true)]
    public class ReadingListItem
    {
        public int Id { get; init; }
        public int SeriesId { get; set; }
        public int VolumeId { get; set; }
        public int ChapterId { get; set; }
        /// <summary>
        /// Order of the chapter within a Reading List
        /// </summary>
        public int Order { get; set; }

        // Relationship
        public ReadingList ReadingList { get; set; }
        public int ReadingListId { get; set; }

        // Idea, keep these for easy join statements
        public Series Series { get; set; }
        public Volume Volume { get; set; }
        public Chapter Chapter { get; set; }

    }
}
