using System;
using System.Collections.Generic;
using API.DTOs.CollectionTags;
using API.DTOs.Metadata;
using API.Entities.Enums;

namespace API.DTOs;

public class SeriesMetadataDto
{
    public int Id { get; set; }
    public string Summary { get; set; } = string.Empty;
    /// <summary>
    /// Collections the Series belongs to
    /// </summary>
    public ICollection<CollectionTagDto> CollectionTags { get; set; }
    /// <summary>
    /// Genres for the Series
    /// </summary>
    public ICollection<GenreTagDto> Genres { get; set; }
    /// <summary>
    /// Collection of all Tags from underlying chapters for a Series
    /// </summary>
    public ICollection<TagDto> Tags { get; set; }
    public ICollection<PersonDto> Writers { get; set; } = new List<PersonDto>();
    public ICollection<PersonDto> CoverArtists { get; set; } = new List<PersonDto>();
    public ICollection<PersonDto> Publishers { get; set; } = new List<PersonDto>();
    public ICollection<PersonDto> Characters { get; set; } = new List<PersonDto>();
    public ICollection<PersonDto> Pencillers { get; set; } = new List<PersonDto>();
    public ICollection<PersonDto> Inkers { get; set; } = new List<PersonDto>();
    public ICollection<PersonDto> Colorists { get; set; } = new List<PersonDto>();
    public ICollection<PersonDto> Letterers { get; set; } = new List<PersonDto>();
    public ICollection<PersonDto> Editors { get; set; } = new List<PersonDto>();
    public ICollection<PersonDto> Translators { get; set; } = new List<PersonDto>();
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
    public bool WritersLocked { get; set; }
    public bool CharactersLocked { get; set; }
    public bool ColoristsLocked { get; set; }
    public bool EditorsLocked { get; set; }
    public bool InkersLocked { get; set; }
    public bool LetterersLocked { get; set; }
    public bool PencillersLocked { get; set; }
    public bool PublishersLocked { get; set; }
    public bool TranslatorsLocked { get; set; }
    public bool CoverArtistsLocked { get; set; }


    public int SeriesId { get; set; }
}
