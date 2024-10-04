using System;
using System.Collections.Generic;
using API.DTOs.Metadata;
using API.Entities.Enums;
using API.Entities.Interfaces;

namespace API.DTOs;

/// <summary>
/// A Chapter is the lowest grouping of a reading medium. A Chapter contains a set of MangaFiles which represents the underlying
/// file (abstracted from type).
/// </summary>
public class ChapterDto : IHasReadTimeEstimate, IHasCoverImage
{
    public int Id { get; init; }
    /// <summary>
    /// Range of chapters. Chapter 2-4 -> "2-4". Chapter 2 -> "2". If special, will be special name.
    /// </summary>
    /// <remarks>This can be something like 19.HU or Alpha as some comics are like this</remarks>
    public string Range { get; init; } = default!;
    /// <summary>
    /// Smallest number of the Range.
    /// </summary>
    [Obsolete("Use MinNumber and MaxNumber instead")]
    public string Number { get; init; } = default!;
    /// <summary>
    /// This may be 0 under the circumstance that the Issue is "Alpha" or other non-standard numbers.
    /// </summary>
    public float MinNumber { get; init; }
    public float MaxNumber { get; init; }
    /// <summary>
    /// The sorting order of the Chapter. Inherits from MinNumber, but can be overridden.
    /// </summary>
    public float SortOrder { get; set; }
    /// <summary>
    /// Total number of pages in all MangaFiles
    /// </summary>
    public int Pages { get; init; }
    /// <summary>
    /// If this Chapter contains files that could only be identified as Series or has Special Identifier from filename
    /// </summary>
    public bool IsSpecial { get; init; }
    /// <summary>
    /// Used for books/specials to display custom title. For non-specials/books, will be set to <see cref="Range"/>
    /// </summary>
    public string Title { get; set; } = default!;
    /// <summary>
    /// The files that represent this Chapter
    /// </summary>
    public ICollection<MangaFileDto> Files { get; init; } = default!;
    /// <summary>
    /// Calculated at API time. Number of pages read for this Chapter for logged in user.
    /// </summary>
    public int PagesRead { get; set; }
    /// <summary>
    /// The last time a chapter was read by current authenticated user
    /// </summary>
    public DateTime LastReadingProgressUtc { get; set; }
    /// <summary>
    /// The last time a chapter was read by current authenticated user
    /// </summary>
    public DateTime LastReadingProgress { get; set; }
    /// <summary>
    /// If the Cover Image is locked for this entity
    /// </summary>
    public bool CoverImageLocked { get; set; }
    /// <summary>
    /// Volume Id this Chapter belongs to
    /// </summary>
    public int VolumeId { get; init; }
    /// <summary>
    /// When chapter was created
    /// </summary>
    public DateTime CreatedUtc { get; set; }
    public DateTime LastModifiedUtc { get; set; }
    /// <summary>
    /// When chapter was created in local server time
    /// </summary>
    /// <remarks>This is required for Tachiyomi Extension</remarks>
    public DateTime Created { get; set; }
    /// <summary>
    /// When the chapter was released.
    /// </summary>
    /// <remarks>Metadata field</remarks>
    public DateTime ReleaseDate { get; init; }
    /// <summary>
    /// Title of the Chapter/Issue
    /// </summary>
    /// <remarks>Metadata field</remarks>
    public string TitleName { get; set; } = default!;
    /// <summary>
    /// Summary of the Chapter
    /// </summary>
    /// <remarks>This is not set normally, only for Series Detail</remarks>
    public string Summary { get; init; } = default!;
    /// <summary>
    /// Age Rating for the issue/chapter
    /// </summary>
    public AgeRating AgeRating { get; init; }
    /// <summary>
    /// Total words in a Chapter (books only)
    /// </summary>
    public long WordCount { get; set; } = 0L;
    /// <summary>
    /// Formatted Volume title ie) Volume 2.
    /// </summary>
    /// <remarks>Only available when fetched from Series Detail API</remarks>
    public string VolumeTitle { get; set; } = string.Empty;
    /// <inheritdoc cref="IHasReadTimeEstimate.MinHoursToRead"/>
    public int MinHoursToRead { get; set; }
    /// <inheritdoc cref="IHasReadTimeEstimate.MaxHoursToRead"/>
    public int MaxHoursToRead { get; set; }
    /// <inheritdoc cref="IHasReadTimeEstimate.AvgHoursToRead"/>
    public float AvgHoursToRead { get; set; }
    /// <summary>
    /// Comma-separated link of urls to external services that have some relation to the Chapter
    /// </summary>
    public string WebLinks { get; set; }
    /// <summary>
    /// ISBN-13 (usually) of the Chapter
    /// </summary>
    /// <remarks>This is guaranteed to be Valid</remarks>
    public string ISBN { get; set; }

    #region Metadata

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

    public ICollection<GenreTagDto> Genres { get; set; } = new List<GenreTagDto>();

    /// <summary>
    /// Collection of all Tags from underlying chapters for a Series
    /// </summary>
    public ICollection<TagDto> Tags { get; set; } = new List<TagDto>();
    public PublicationStatus PublicationStatus { get; set; }
    /// <summary>
    /// Language for the Chapter/Issue
    /// </summary>
    public string? Language { get; set; }
    /// <summary>
    /// Number in the TotalCount of issues
    /// </summary>
    public int Count { get; set; }
    /// <summary>
    /// Total number of issues for the series
    /// </summary>
    public int TotalCount { get; set; }

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

    #endregion

    public string CoverImage { get; set; }
    public string PrimaryColor { get; set; }
    public string SecondaryColor { get; set; }

    public void ResetColorScape()
    {
        PrimaryColor = string.Empty;
        SecondaryColor = string.Empty;
    }
}
