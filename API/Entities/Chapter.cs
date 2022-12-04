using System;
using System.Collections.Generic;
using API.Entities.Enums;
using API.Entities.Interfaces;
using API.Parser;
using API.Services;

namespace API.Entities;

public class Chapter : IEntityDate, IHasReadTimeEstimate
{
    public int Id { get; set; }
    /// <summary>
    /// Range of numbers. Chapter 2-4 -> "2-4". Chapter 2 -> "2".
    /// </summary>
    public string? Range { get; set; }
    /// <summary>
    /// Smallest number of the Range. Can be a partial like Chapter 4.5
    /// </summary>
    public required string Number { get; set; }
    /// <summary>
    /// The files that represent this Chapter
    /// </summary>
    public ICollection<MangaFile> Files { get; set; } = null!;
    public DateTime Created { get; set; }
    public DateTime LastModified { get; set; }
    /// <summary>
    /// Relative path to the (managed) image file representing the cover image
    /// </summary>
    /// <remarks>The file is managed internally to Kavita's APPDIR</remarks>
    public string? CoverImage { get; set; }
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
    /// Total number of issues or volumes in the series
    /// </summary>
    /// <remarks>Users may use Volume count or issue count. Kavita performs some light logic to help Count match up with TotalCount</remarks>
    public int TotalCount { get; set; } = 0;
    /// <summary>
    /// Number of the Total Count (progress the Series is complete)
    /// </summary>
    public int Count { get; set; } = 0;

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
    public int AvgHoursToRead { get; set; }


    /// <summary>
    /// All people attached at a Chapter level. Usually Comics will have different people per issue.
    /// </summary>
    public ICollection<Person> People { get; set; } = new List<Person>();
    /// <summary>
    /// Genres for the Chapter
    /// </summary>
    public ICollection<Genre> Genres { get; set; } = new List<Genre>();
    public ICollection<Tag> Tags { get; set; } = new List<Tag>();




    // Relationships
    public Volume Volume { get; set; } = null!;
    public int VolumeId { get; set; }

    public void UpdateFrom(ParserInfo info)
    {
        Files ??= new List<MangaFile>();
        IsSpecial = info.IsSpecialInfo();
        if (IsSpecial)
        {
            Number = "0";
        }
        Title = (IsSpecial && info.Format == MangaFormat.Epub)
            ? info.Title
            : Range;

    }
}
