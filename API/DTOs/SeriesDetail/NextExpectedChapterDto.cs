using System;

namespace API.DTOs.SeriesDetail;

public class NextExpectedChapterDto
{
    public float ChapterNumber { get; set; }
    public float VolumeNumber { get; set; }
    /// <summary>
    /// Null if not applicable
    /// </summary>
    public DateTime? ExpectedDate { get; set; }
    /// <summary>
    /// The localized title to render on the card
    /// </summary>
    public string Title { get; set; }
}
