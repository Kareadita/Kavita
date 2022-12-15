using System;
using API.Entities.Enums;

namespace API.DTOs.Statistics;

public class PagesReadOnADayCount<T> : ICount<T>
{
    /// <summary>
    /// The day of the readings
    /// </summary>
    public T Value { get; set; }
    /// <summary>
    /// Number of pages read
    /// </summary>
    public int Count { get; set; }
    /// <summary>
    /// Format of those files
    /// </summary>
    public MangaFormat Format { get; set; }

}
