namespace API.DTOs;

/// <summary>
/// Used to browse writers and click in to see their series
/// </summary>
public class BrowsePersonDto : PersonDto
{
    /// <summary>
    /// Number of Series this Person is the Writer for
    /// </summary>
    public int SeriesCount { get; set; }
    /// <summary>
    /// Number or Issues this Person is the Writer for
    /// </summary>
    public int IssueCount { get; set; }
}
