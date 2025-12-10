using System.Collections.Generic;

namespace API.DTOs.Progress;

public class DailyReadingDataDto
{
    public int TotalMinutesRead { get; set; }
    public int TotalPagesRead { get; set; }
    public int TotalWordsRead { get; set; }
    public int LongestSessionMinutes { get; set; }
    public IList<int> SeriesIds { get; set; }
    public IList<int> ChapterIds { get; set; }
}
