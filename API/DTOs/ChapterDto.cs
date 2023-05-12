using System;
using System.Collections.Generic;
using API.Entities.Enums;
using API.Entities.Interfaces;

namespace API.DTOs;

/// <summary>
/// A Chapter is the lowest grouping of a reading medium. A Chapter contains a set of MangaFiles which represents the underlying
/// file (abstracted from type).
/// </summary>
public class ChapterDto : IHasReadTimeEstimate, IEntityDate
{
    public int Id { get; init; }
    /// <summary>
    /// Range of chapters. Chapter 2-4 -> "2-4". Chapter 2 -> "2".
    /// </summary>
    public string Range { get; init; } = default!;
    /// <summary>
    /// Smallest number of the Range.
    /// </summary>
    public string Number { get; init; } = default!;
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
    public DateTime Created { get; set; }
    public DateTime LastModified { get; set; }
    public DateTime CreatedUtc { get; set; }
    public DateTime LastModifiedUtc { get; set; }
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
    public int AvgHoursToRead { get; set; }
    /// <summary>
    /// Comma-separated link of urls to external services that have some relation to the Chapter
    /// </summary>
    public string WebLinks { get; set; }
    /// <summary>
    /// ISBN-13 (usually) of the Chapter
    /// </summary>
    /// <remarks>This is guaranteed to be Valid</remarks>
    public string ISBN { get; set; }
}
