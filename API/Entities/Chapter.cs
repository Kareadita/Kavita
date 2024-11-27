using System;
using System.Collections.Generic;
using System.Globalization;
using API.Entities.Enums;
using API.Entities.Interfaces;
using API.Extensions;
using API.Services.Tasks.Scanner.Parser;

namespace API.Entities;

public class Chapter : IEntityDate, IHasReadTimeEstimate, IHasCoverImage
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
    public required string Number { get; set; }
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
    /// <summary>
    /// The files that represent this Chapter
    /// </summary>
    public ICollection<MangaFile> Files { get; set; } = null!;
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

    public ICollection<AppUserProgress> UserProgress { get; set; }


    // Relationships
    public Volume Volume { get; set; } = null!;
    public int VolumeId { get; set; }

    public void UpdateFrom(ParserInfo info)
    {
        Files ??= new List<MangaFile>();
        IsSpecial = info.IsSpecialInfo();
        if (IsSpecial)
        {
            Number = Parser.DefaultChapter;
            MinNumber = Parser.DefaultChapterNumber;
            MaxNumber = Parser.DefaultChapterNumber;
        }
        // NOTE: This doesn't work well for all because Pdf usually should use into.Title or even filename
        Title = (IsSpecial && info.Format == MangaFormat.Epub)
            ? info.Title
            : Parser.RemoveExtensionIfSupported(Range);

        var specialTreatment = info.IsSpecialInfo();
        Range = specialTreatment ? info.Filename : info.Chapters;
    }

    /// <summary>
    /// Returns the Chapter Number. If the chapter is a range, returns that, formatted.
    /// </summary>
    /// <returns></returns>
    public string GetNumberTitle()
    {
        // BUG: TODO: On non-english locales, for floats, the range will be 20,5 but the NumberTitle will return 20.5
        // Have I fixed this with TryParse CultureInvariant
        try
        {
            if (MinNumber.Is(MaxNumber))
            {
                if (MinNumber.Is(Parser.DefaultChapterNumber) && IsSpecial)
                {
                    return Parser.RemoveExtensionIfSupported(Title);
                }

                if (MinNumber.Is(0f) && !float.TryParse(Range, CultureInfo.InvariantCulture, out _))
                {
                    return $"{Range.ToString(CultureInfo.InvariantCulture)}";
                }

                return $"{MinNumber.ToString(CultureInfo.InvariantCulture)}";

            }

            return $"{MinNumber.ToString(CultureInfo.InvariantCulture)}-{MaxNumber.ToString(CultureInfo.InvariantCulture)}";
        }
        catch (Exception)
        {
            return MinNumber.ToString(CultureInfo.InvariantCulture);
        }
    }

    /// <summary>
    /// Is the Chapter representing a single Volume (volume 1.cbz). If so, Min/Max will be Default and will not be special
    /// </summary>
    /// <returns></returns>
    public bool IsSingleVolumeChapter()
    {
        return MinNumber.Is(Parser.DefaultChapterNumber) && !IsSpecial;
    }

    public void ResetColorScape()
    {
        PrimaryColor = string.Empty;
        SecondaryColor = string.Empty;
    }
}
