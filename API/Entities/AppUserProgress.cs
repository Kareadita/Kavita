using System.ComponentModel.DataAnnotations.Schema;

namespace API.Entities
{
    /// <summary>
    /// Represents the progress a single user has on a given Volume.
    /// </summary>
    public class AppUserProgress
    {
        public int Id { get; set; }
        public int PagesRead { get; set; }
        public int VolumeId { get; set; }
        public int SeriesId { get; set; }
        
        // Relationships
        public AppUser AppUser { get; set; }
        public int AppUserId { get; set; }
    }
}