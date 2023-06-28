namespace API.DTOs.Recommendation;

/// <summary>
/// Has extra information from Series Metadata
/// </summary>
public class ExtendedSeriesDto : SeriesDto
{
    public string Summary { get; set; }
}
