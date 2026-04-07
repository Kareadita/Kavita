using System;
using System.Collections.Generic;
using Kavita.Models.Entities.Enums;
using Kavita.Models.Entities.Interfaces;
using Kavita.Models.Entities.Metadata;
using Kavita.Models.Entities.MetadataMatching;
using Kavita.Models.Entities.Person;
using Kavita.Models.Entities.Progress;
using Kavita.Models.Entities.User;

namespace Kavita.Models.Entities;

public class Chapter : IEntityDate, IHasReadTimeEstimate, IHasCoverImage, IHasKPlusMetadata, IHasMetadataIds, IHasTags<Tag>
{
    public int Id { get; set; }
    /// <summary>
    /// Range of numbers. Chapter 2-4 -> "2-4". Chapter 2 -> "2". If the chapter is a special, will return the Special Name
    /// </summary>
    public required string Range { get; set; }
    /// <summary>
    /// Smallest number of the Range. Can be a partial like Chapter 4.5
    /// </summary>
    [Obsolete("Use MinNumber and MaxNumber instead")]
    public string Number { get; set; }
    /// <summary>
    /// Minimum Chapter Number.
    /// </summary>
    public float MinNumber { get; set; }
    /// <summary>
    /// Maximum Chapter Number
    /// </summary>
    public float MaxNumber { get; set; }
    /// <summary>
    /// The sorting order of the Chapter. Inherits from MinNumber, but can be overridden.
    /// </summary>
    public float SortOrder { get; set; }
    /// <summary>
    /// Can the sort order be updated on scan or is it locked from UI
    /// </summary>
    public bool SortOrderLocked { get; set; }
    public DateTime Created { get; set; }
    public DateTime LastModified { get; set; }
    public DateTime CreatedUtc { get; set; }
    public DateTime LastModifiedUtc { get; set; }

    public string? CoverImage { get; set; }
    public string PrimaryColor { get; set; }
    public string SecondaryColor { get; set; }
    public bool CoverImageLocked { get; set; }
    /// <summary>
    /// Total number of pages in all MangaFiles
    /// </summary>
    public int Pages { get; set; }
    /// <summary>
    /// If this Chapter contains files that could only be identified as Series or has Special Identifier from filename
    /// </summary>
    public bool IsSpecial { get; set; }
    /// <summary>
    /// Used for books/specials to display custom title. For non-specials/books, will be set to <see cref="Range"/>
    /// </summary>
    public string? Title { get; set; }

    /// <summary>
    /// Age Rating for the issue/chapter
    /// </summary>
    public AgeRating AgeRating { get; set; }

    /// <summary>
    /// Chapter title
    /// </summary>
    /// <remarks>This should not be confused with Title which is used for special filenames.</remarks>
    public string TitleName { get; set; } = string.Empty;
    /// <summary>
    /// Date which chapter was released
    /// </summary>
    public DateTime ReleaseDate { get; set; }
    /// <summary>
    /// Summary for the Chapter/Issue
    /// </summary>
    public string? Summary { get; set; }
    /// <summary>
    /// Language for the Chapter/Issue
    /// </summary>
    public string? Language { get; set; }
    /// <summary>
    /// Total number of issues or volumes in the series. This is straight from ComicInfo
    /// </summary>
    public int TotalCount { get; set; } = 0;
    /// <summary>
    /// Number of the Total Count (progress the Series is complete)
    /// </summary>
    /// <remarks>This is either the highest of ComicInfo Count field and (nonparsed volume/chapter number)</remarks>
    public int Count { get; set; } = 0;
    /// <summary>
    /// SeriesGroup tag in ComicInfo
    /// </summary>
    public string SeriesGroup { get; set; } = string.Empty;
    public string StoryArc { get; set; } = string.Empty;
    public string StoryArcNumber { get; set; } = string.Empty;
    public string AlternateNumber { get; set; } = string.Empty;
    public string AlternateSeries { get; set; } = string.Empty;

    /// <summary>
    /// Not currently used in Kavita
    /// </summary>
    public int AlternateCount { get; set; } = 0;

    /// <summary>
    /// Total Word count of all chapters in this chapter.
    /// </summary>
    /// <remarks>Word Count is only available from EPUB files</remarks>
    public long WordCount { get; set; }
    /// <inheritdoc cref="IHasReadTimeEstimate"/>
    public int MinHoursToRead { get; set; }
    /// <inheritdoc cref="IHasReadTimeEstimate"/>
    public int MaxHoursToRead { get; set; }
    /// <inheritdoc cref="IHasReadTimeEstimate"/>
    public float AvgHoursToRead { get; set; }
    /// <summary>
    /// Comma-separated link of urls to external services that have some relation to the Chapter
    /// </summary>
    public string WebLinks { get; set; } = string.Empty;
    public string ISBN { get; set; } = string.Empty;

    /// <summary>
    /// Tracks which metadata has been set by K+
    /// </summary>
    public IList<MetadataSettingField> KPlusOverrides { get; set; } = [];

    /// <summary>
    /// (Kavita+) Average rating from Kavita+ metadata
    /// </summary>
    public float AverageExternalRating { get; set; } = 0f;

    #region Metadata
    public int AniListId { get; set; }
    public long MalId { get; set; }
    public int HardcoverId { get; set; }
    public long MetronId { get; set; }
    public string? ComicVineId { get; set; }
    public long MangaBakaId { get; set; }
    #endregion

    #region Locks

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

    #endregion

    /// <summary>
    /// All people attached at a Chapter level. Usually Comics will have different people per issue.
    /// </summary>
    public ICollection<ChapterPeople> People { get; set; } = new List<ChapterPeople>();
    /// <summary>
    /// Genres for the Chapter
    /// </summary>
    public ICollection<Genre> Genres { get; set; } = new List<Genre>();
    public ICollection<Tag> Tags { get; set; } = new List<Tag>();
    public ICollection<AppUserChapterRating> Ratings { get; set; } = [];

    public ICollection<AppUserProgress> UserProgress { get; set; }
    /// <summary>
    /// The files that represent this Chapter
    /// </summary>
    public ICollection<MangaFile> Files { get; set; } = null!;


    public Volume Volume { get; set; } = null!;
    public int VolumeId { get; set; }

    public ICollection<ExternalReview> ExternalReviews { get; set; } = [];
    public ICollection<ExternalRating> ExternalRatings { get; set; } = null!;
    public void ResetColorScape()
    {
        PrimaryColor = string.Empty;
        SecondaryColor = string.Empty;
    }

    public bool IsPersonRoleLocked(PersonRole role)
    {
        return role switch
        {
            PersonRole.Character => CharacterLocked,
            PersonRole.Writer => WriterLocked,
            PersonRole.Penciller => PencillerLocked,
            PersonRole.Inker => InkerLocked,
            PersonRole.Colorist => ColoristLocked,
            PersonRole.Letterer => LettererLocked,
            PersonRole.CoverArtist => CoverArtistLocked,
            PersonRole.Editor => EditorLocked,
            PersonRole.Publisher => PublisherLocked,
            PersonRole.Translator => TranslatorLocked,
            PersonRole.Imprint => ImprintLocked,
            PersonRole.Team => TeamLocked,
            PersonRole.Location => LocationLocked,
            _ => throw new ArgumentOutOfRangeException(nameof(role), role, null)
        };
    }
}
