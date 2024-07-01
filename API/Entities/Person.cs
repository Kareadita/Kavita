using System.Collections.Generic;
using API.Entities.Enums;
using API.Entities.Metadata;

namespace API.Entities;

public class Person
{
    public int Id { get; set; }
    public required string Name { get; set; }
    public required string NormalizedName { get; set; }
    public required PersonRole Role { get; set; }

    public string? CoverImage { get; set; }
    public string Description { get; set; }
    /// <summary>
    /// https://anilist.co/staff/{AniListId}/
    /// </summary>
    /// <remarks>Kavita+ Only</remarks>
    public int AniListId { get; set; }
    /// <summary>
    /// https://myanimelist.net/people/{MalId}/
    /// </summary>
    /// <remarks>Kavita+ Only</remarks>
    public long MalId { get; set; }
    /// <summary>
    /// https://hardcover.app/authors/{HardcoverId}
    /// </summary>
    /// <remarks>Kavita+ Only</remarks>
    public string HardcoverId { get; set; }


    // Relationships
    public ICollection<SeriesMetadata> SeriesMetadatas { get; set; } = null!;
    public ICollection<Chapter> ChapterMetadatas { get; set; } = null!;
}
