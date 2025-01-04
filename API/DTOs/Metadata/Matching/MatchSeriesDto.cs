namespace API.DTOs.Metadata.Matching;

/// <summary>
/// Used for matching a series with Kavita+ for metadata and scrobbling
/// </summary>
public class MatchSeriesDto
{
    /// <summary>
    /// When set, Kavita will stop attempting to match this series and will not perform any scrobbling
    /// </summary>
    public bool DontMatch { get; set; }
    /// <summary>
    /// Series Id to pull internal metadata from to improve matching
    /// </summary>
    public int SeriesId { get; set; }
    /// <summary>
    /// Free form text to query for. Can be a url and ids will be parsed from it
    /// </summary>
    public string Query { get; set; }
}
