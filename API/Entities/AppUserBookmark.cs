using System;
using System.Text.Json.Serialization;
using API.Entities.Interfaces;

namespace API.Entities;

/// <summary>
/// Represents a saved page in a Chapter entity for a given user.
/// </summary>
public class AppUserBookmark : IEntityDate
{
    public int Id { get; set; }
    public int Page { get; set; }
    public int SeriesId { get; set; }
    public int VolumeId { get; set; }
    public int ChapterId { get; set; }

    /// <summary>
    /// Filename in the Bookmark Directory
    /// </summary>
    public string FileName { get; set; } = string.Empty;


    // Relationships
    [JsonIgnore]
    public AppUser AppUser { get; set; } = null!;
    public int AppUserId { get; set; }
    public DateTime Created { get; set; }
    public DateTime LastModified { get; set; }
    public DateTime CreatedUtc { get; set; }
    public DateTime LastModifiedUtc { get; set; }
}
