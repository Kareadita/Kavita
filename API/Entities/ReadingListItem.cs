using Microsoft.EntityFrameworkCore;

namespace API.Entities
{
    [Index(nameof(SeriesId), nameof(VolumeId), nameof(ChapterId), nameof(LibraryId), IsUnique = true)]
    public class ReadingListItem
    {
        public int Id { get; init; }
        public int LibraryId { get; set; }
        public int SeriesId { get; set; }
        public int VolumeId { get; set; }
        public int ChapterId { get; set; }
        /// <summary>
        /// Order of the chapter within a Reading List
        /// </summary>
        public int Order { get; set; }

    }
}
