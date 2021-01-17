namespace API.Entities
{
    /// <summary>
    /// Represents the progress a single user has on a given Volume.
    /// </summary>
    public class AppUserProgress
    {
        public int Id { get; set; }
        public int PagesRead { get; set; }
        
        public AppUser AppUser { get; set; }
        public int AppUserId { get; set; }
        public int VolumeId { get; set; }
        public int SeriesId { get; set; } // shortcut
        //public bool VolumeCompleted { get; set; } // This will be set true if PagesRead == Sum of MangaFiles on volume
    }
}