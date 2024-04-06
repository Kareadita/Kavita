using System.Collections.Generic;
using API.DTOs.Metadata;
using API.Entities.Enums;

namespace API.DTOs;

public class SeriesMetadataDto
{
    public int Id { get; set; }
    public string Summary { get; set; } = string.Empty;

    /// <summary>
    /// Genres for the Series
    /// </summary>
    public ICollection<GenreTagDto> Genres { get; set; } = new List<GenreTagDto>();

    /// <summary>
    /// Collection of all Tags from underlying chapters for a Series
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
    /// Earliest Year from all chapters
    /// </summary>
    public int ReleaseYear { get; set; }
    /// <summary>
    /// Language of the content (BCP-47 code)
    /// </summary>
    public string Language { get; set; } = string.Empty;
    /// <summary>
    /// Max number of issues/volumes in the series (Max of Volume/Issue field in ComicInfo)
    /// </summary>
    public int MaxCount { get; set; } = 0;
    /// <summary>
    /// Total number of issues/volumes for the series
    /// </summary>
    public int TotalCount { get; set; }
    /// <summary>
    /// Publication status of the Series
    /// </summary>
    public PublicationStatus PublicationStatus { get; set; }
    /// <summary>
    /// A comma-separated list of Urls
    /// </summary>
    public string WebLinks { get; set; }

    public bool LanguageLocked { get; set; }
    public bool SummaryLocked { get; set; }
    /// <summary>
    /// Locked by user so metadata updates from scan loop will not override AgeRating
    /// </summary>
    public bool AgeRatingLocked { get; set; }
    /// <summary>
    /// Locked by user so metadata updates from scan loop will not override PublicationStatus
    /// </summary>
    public bool PublicationStatusLocked { get; set; }
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
    public bool ReleaseYearLocked { get; set; }


    public int SeriesId { get; set; }
}
