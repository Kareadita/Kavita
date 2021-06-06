
using System;
using API.Entities.Interfaces;

namespace API.Entities
{
    /// <summary>
    /// Represents the progress a single user has on a given Chapter.
    /// </summary>
    public class AppUserProgress : IEntityDate
    {
        public int Id { get; set; }
        public int PagesRead { get; set; }
        public int VolumeId { get; set; }
        public int SeriesId { get; set; }
        public int ChapterId { get; set; }
        /// <summary>
        /// For Book Reader, represents the nearest passed anchor on the screen that can be used to resume scroll point
        /// on next load
        /// </summary>
        public string BookScrollId { get; set; }
        
        // Relationships
        public AppUser AppUser { get; set; }
        public int AppUserId { get; set; }
        
        public DateTime Created { get; set; }
        public DateTime LastModified { get; set; }
    }
}