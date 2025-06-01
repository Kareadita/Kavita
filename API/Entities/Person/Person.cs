using System.Collections.Generic;
using API.Entities.Interfaces;

namespace API.Entities.Person;

public class Person : IHasCoverImage
{
    public int Id { get; set; }
    public required string Name { get; set; }
    public required string NormalizedName { get; set; }
    public ICollection<PersonAlias> Aliases { get; set; } = [];

    public string? CoverImage { get; set; }
    public bool CoverImageLocked { get; set; }
    public string PrimaryColor { get; set; }
    public string SecondaryColor { get; set; }

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
    public int AniListId { get; set; } = 0;
    /// <summary>
    /// https://myanimelist.net/people/{MalId}/
    /// https://myanimelist.net/character/{MalId}/CharacterName
    /// </summary>
    /// <remarks>Kavita+ Only</remarks>
    public long MalId { get; set; } = 0;
    /// <summary>
    /// https://hardcover.app/authors/{HardcoverId}
    /// </summary>
    /// <remarks>Kavita+ Only</remarks>
    public string? HardcoverId { get; set; }

    /// <summary>
    /// https://metron.cloud/creator/{slug}/
    /// </summary>
    /// <remarks>Kavita+ Only</remarks>
    //public long MetronId { get; set; } = 0;

    // Relationships
    public ICollection<ChapterPeople> ChapterPeople { get; set; } = [];
    public ICollection<SeriesMetadataPeople> SeriesMetadataPeople { get; set; } = [];


    public void ResetColorScape()
    {
        PrimaryColor = string.Empty;
        SecondaryColor = string.Empty;
    }
}
