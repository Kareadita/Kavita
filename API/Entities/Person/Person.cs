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
    public bool CoverImageLocked { get; set; }
    public string Description { get; set; }
    /// <summary>
    /// ASIN for person
    /// </summary>
    /// <remarks>Can be used for Amazon author lookup</remarks>
    public string? Asin { get; set; }
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

    // TODO: I need to rearchcitect this whole system unfortunately.
    // The relationship itself needs to have the role and the person needs to be unique


    // Relationships
//     public ICollection<SeriesMetadata> SeriesMetadatas { get; set; } = null!;
//     public ICollection<Chapter> ChapterMetadatas { get; set; } = null!;
    public ICollection<ChapterPeople> ChapterPeople { get; set; } = new List<ChapterPeople>();
    public ICollection<SeriesMetadataPeople> SeriesMetadataPeople { get; set; } = new List<SeriesMetadataPeople>();
}
