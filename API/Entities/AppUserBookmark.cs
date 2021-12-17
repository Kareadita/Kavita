using System.Text.Json.Serialization;

namespace API.Entities
{
    /// <summary>
    /// Represents a saved page in a Chapter entity for a given user.
    /// </summary>
    public class AppUserBookmark
    {
        public int Id { get; set; }
        public int Page { get; set; }
        public int VolumeId { get; set; }
        public int SeriesId { get; set; }
        public int ChapterId { get; set; }
        /// <summary>
        /// Filename in the Bookmark Directory
        /// </summary>
        public string FileName { get; set; }


        // Relationships
        [JsonIgnore]
        public AppUser AppUser { get; set; }
        public int AppUserId { get; set; }
    }
}
