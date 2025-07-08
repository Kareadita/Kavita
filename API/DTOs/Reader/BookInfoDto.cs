using System.Collections.Generic;
using API.Entities.Enums;

namespace API.DTOs.Reader;

public sealed record BookInfoDto : IChapterInfoDto
{
    public string BookTitle { get; set; } = default! ;
    public int SeriesId { get; set; }
    public int VolumeId { get; set; }
    public MangaFormat SeriesFormat { get; set; }
    public string SeriesName { get; set; } = default! ;
    public string ChapterNumber { get; set; } = default! ;
    public string VolumeNumber { get; set; } = default! ;
    public int LibraryId { get; set; }
    public int Pages { get; set; }
    public bool IsSpecial { get; set; }
    public string ChapterTitle { get; set; } = default! ;
    /// <summary>
    /// For Epub reader, this will contain Page number -> word count. All other times will be null.
    /// </summary>
    /// <remarks>This is optionally returned by includeWordCounts</remarks>
    public IDictionary<int, int>? PageWordCounts { get; set; }
}
