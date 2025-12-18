using System.Collections.Generic;

namespace API.DTOs.Statistics;

#nullable enable

public sealed record MostActiveUserDto
{
    public int UserId { get; set; }
    public string Username { get; set; }
    public string? CoverImage { get; set; }

    public int TimePeriodHours { get; set; }
    public int TotalHours { get; set; }
    public int TotalComics { get; set; }
    public int TotalBooks { get; set; }
    /// <summary>
    /// Top 5 most read series for the time period
    /// </summary>
    public IList<SeriesDto> TopSeries { get; set; }
}
