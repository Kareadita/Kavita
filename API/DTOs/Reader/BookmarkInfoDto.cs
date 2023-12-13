using System.Collections.Generic;
using API.Entities.Enums;

namespace API.DTOs.Reader;
#nullable enable

public class BookmarkInfoDto
{
    public string SeriesName { get; set; } = default!;
    public MangaFormat SeriesFormat { get; set; }
    public int SeriesId { get; set; }
    public int LibraryId { get; set; }
    public LibraryType LibraryType { get; set; }
    public int Pages { get; set; }
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
