using System;
using System.Collections.Generic;
using API.DTOs.Metadata;
using API.Entities.Enums;

namespace API.DTOs;

public class UpdateChapterDto
{
    public int Id { get; init; }
    public string Summary { get; set; } = string.Empty;

    /// <summary>
    /// Genres for the Chapter
    /// </summary>
    public ICollection<GenreTagDto> Genres { get; set; } = new List<GenreTagDto>();
    /// <summary>
    /// Collection of all Tags from underlying chapters for a Chapter
    /// </summary>
    public ICollection<TagDto> Tags { get; set; } = new List<TagDto>();

    public ICollection<PersonDto> Writers { get; set; } = new List<PersonDto>();
    public ICollection<PersonDto> CoverArtists { get; set; } = new List<PersonDto>();
    public ICollection<PersonDto> Publishers { get; set; } = new List<PersonDto>();
    public ICollection<PersonDto> Characters { get; set; } = new List<PersonDto>();
    public ICollection<PersonDto> Pencillers { get; set; } = new List<PersonDto>();
    public ICollection<PersonDto> Inkers { get; set; } = new List<PersonDto>();
    public ICollection<PersonDto> Imprints { get; set; } = new List<PersonDto>();
    public ICollection<PersonDto> Colorists { get; set; } = new List<PersonDto>();
    public ICollection<PersonDto> Letterers { get; set; } = new List<PersonDto>();
    public ICollection<PersonDto> Editors { get; set; } = new List<PersonDto>();
    public ICollection<PersonDto> Translators { get; set; } = new List<PersonDto>();
    public ICollection<PersonDto> Teams { get; set; } = new List<PersonDto>();
    public ICollection<PersonDto> Locations { get; set; } = new List<PersonDto>();

    /// <summary>
    /// Highest Age Rating from all Chapters
    /// </summary>
    public AgeRating AgeRating { get; set; } = AgeRating.Unknown;
    /// <summary>
    /// Language of the content (BCP-47 code)
    /// </summary>
    public string Language { get; set; } = string.Empty;


    /// <summary>
    /// Locked by user so metadata updates from scan loop will not override AgeRating
    /// </summary>
    public bool AgeRatingLocked { get; set; }
    public bool TitleNameLocked { get; set; }
    public bool GenresLocked { get; set; }
    public bool TagsLocked { get; set; }
    public bool WriterLocked { get; set; }
    public bool CharacterLocked { get; set; }
    public bool ColoristLocked { get; set; }
    public bool EditorLocked { get; set; }
    public bool InkerLocked { get; set; }
    public bool ImprintLocked { get; set; }
    public bool LettererLocked { get; set; }
    public bool PencillerLocked { get; set; }
    public bool PublisherLocked { get; set; }
    public bool TranslatorLocked { get; set; }
    public bool TeamLocked { get; set; }
    public bool LocationLocked { get; set; }
    public bool CoverArtistLocked { get; set; }
    public bool LanguageLocked { get; set; }
    public bool SummaryLocked { get; set; }
    public bool ISBNLocked { get; set; }
    public bool ReleaseDateLocked { get; set; }

    /// <summary>
    /// The sorting order of the Chapter. Inherits from MinNumber, but can be overridden.
    /// </summary>
    public float SortOrder { get; set; }
    /// <summary>
    /// Can the sort order be updated on scan or is it locked from UI
    /// </summary>
    public bool SortOrderLocked { get; set; }

    /// <summary>
    /// Comma-separated link of urls to external services that have some relation to the Chapter
    /// </summary>
    public string WebLinks { get; set; } = string.Empty;
    public string ISBN { get; set; } = string.Empty;
    /// <summary>
    /// Date which chapter was released
    /// </summary>
    public DateTime ReleaseDate { get; set; }
    /// <summary>
    /// Chapter title
    /// </summary>
    /// <remarks>This should not be confused with Title which is used for special filenames.</remarks>
    public string TitleName { get; set; } = string.Empty;
}
