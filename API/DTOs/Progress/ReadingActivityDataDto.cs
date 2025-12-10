using System;

namespace API.DTOs.Progress;
#nullable enable

public sealed record ReadingActivityDataDto
{
    public int ChapterId { get; set; }
    public int VolumeId { get; set; }
    public int SeriesId { get; set; }
    public int LibraryId { get; set; }
    public int StartPage { get; set; }
    public int EndPage { get; set; }
    public DateTime StartTimeUtc { get; set; }
    public DateTime? EndTimeUtc { get; set; }
    public int PagesRead { get; set; }
    /// <summary>
    /// Only applicable for Book entries
    /// </summary>
    public int WordsRead { get; set; }
    public int TotalPages { get; set; }
    public int TotalWords { get; set; }

    public string LibraryName { get; set; }
    public string SeriesName { get; set; }
    public string ChapterTitle { get; set; }

    public ClientInfoDto? ClientInfo { get; set; }
}
