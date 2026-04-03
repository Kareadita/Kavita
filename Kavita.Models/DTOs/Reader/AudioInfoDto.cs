using Kavita.Models.Entities.Enums;

namespace Kavita.Models.DTOs.Reader;

/// <summary>
/// Metadata returned to the audiobook reader for a given chapter.
/// DurationSeconds reuses the Pages field concept: total seconds of audio content.
/// </summary>
public sealed record AudioInfoDto : IChapterInfoDto
{
    public string BookTitle { get; set; } = default!;
    public int SeriesId { get; set; }
    public int VolumeId { get; set; }
    public MangaFormat SeriesFormat { get; set; }
    public string SeriesName { get; set; } = default!;
    public string ChapterNumber { get; set; } = default!;
    public string VolumeNumber { get; set; } = default!;
    public int LibraryId { get; set; }
    /// <summary>Total duration in seconds (stored as Pages in the database).</summary>
    public int Pages { get; set; }
    public bool IsSpecial { get; set; }
    public string ChapterTitle { get; set; } = default!;
    /// <summary>Author(s) of the book being narrated.</summary>
    public string Author { get; set; } = string.Empty;
    /// <summary>Narrator(s) of the audiobook.</summary>
    public string Narrator { get; set; } = string.Empty;
}
