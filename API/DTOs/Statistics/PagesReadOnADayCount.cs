using API.Entities.Enums;

namespace API.DTOs.Statistics;

public class PagesReadOnADayCount<T> : ICount<T>
{
    /// <summary>
    /// The day of the readings
    /// </summary>
    public T Value { get; set; } = default!;
    /// <summary>
    /// Number of pages read
    /// </summary>
    public long Count { get; set; }
    /// <summary>
    /// Format of those files
    /// </summary>
    public MangaFormat Format { get; set; }
}
