using System.Collections.Generic;
using API.Entities.Enums;

namespace API.DTOs.Reader;
#nullable enable

/// <summary>
/// Information about the Chapter for the Reader to render
/// </summary>
public class ChapterInfoDto : IChapterInfoDto
{
    /// <summary>
    /// The Chapter Number
    /// </summary>
    public string ChapterNumber { get; set; } = default! ;
    /// <summary>
    /// The Volume Number
    /// </summary>
    public string VolumeNumber { get; set; } = default! ;
    /// <summary>
    /// Volume entity Id
    /// </summary>
    public int VolumeId { get; set; }
    /// <summary>
    /// Series Name
    /// </summary>
    public string SeriesName { get; set; } = null!;
    /// <summary>
    /// Series Format
    /// </summary>
    public MangaFormat SeriesFormat { get; set; }
    /// <summary>
    /// Series entity Id
    /// </summary>
    public int SeriesId { get; set; }
    /// <summary>
    /// Library entity Id
    /// </summary>
    public int LibraryId { get; set; }
    /// <summary>
    /// Library type
    /// </summary>
    public LibraryType LibraryType { get; set; }
    /// <summary>
    /// Chapter's title if set via ComicInfo.xml (Title field)
    /// </summary>
    public string ChapterTitle { get; set; } = string.Empty;
    /// <summary>
    /// Total Number of pages in this Chapter
    /// </summary>
    public int Pages { get; set; }
    /// <summary>
    /// File name of the chapter
    /// </summary>
    public string? FileName { get; set; }
    /// <summary>
    /// If this is marked as a special in Kavita
    /// </summary>
    public bool IsSpecial { get; set; }
    /// <summary>
    /// The subtitle to render on the reader
    /// </summary>
    public string? Subtitle { get; set; }
    /// <summary>
    /// Series Title
    /// </summary>
    /// <remarks>Usually just series name, but can include chapter title</remarks>
    public string Title { get; set; } = default!;
    /// <summary>
    /// Total pages for the series
    /// </summary>
    public int SeriesTotalPages { get; set; }
    /// <summary>
    /// Total pages read for the series
    /// </summary>
    public int SeriesTotalPagesRead { get; set; }

    /// <summary>
    /// List of all files with their inner archive structure maintained in filename and dimensions
    /// </summary>
    /// <remarks>This is optionally returned by includeDimensions</remarks>
    public IEnumerable<FileDimensionDto>? PageDimensions { get; set; }
    /// <summary>
    /// For Double Page reader, this will contain snap points to ensure the reader always resumes on correct page
    /// </summary>
    /// <remarks>This is optionally returned by includeDimensions</remarks>
    public IDictionary<int, int>? DoublePairs { get; set; }
}
