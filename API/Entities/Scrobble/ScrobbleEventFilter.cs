namespace API.Entities.Scrobble;

public class ScrobbleEventFilter
{
    /// <summary>
    /// Which field to sort on
    /// </summary>
    public ScrobbleEventSortField Field { get; set; } = ScrobbleEventSortField.LastModified;

    /// <summary>
    /// If the sort should be a descending sort
    /// </summary>
    public bool IsDescending { get; set; } = true;
    /// <summary>
    /// A query to search against
    /// </summary>
    public string Query { get; set; }
    /// <summary>
    /// Include reviews in the result - Note: Review Scrobbling is disabled
    /// </summary>
    public bool IncludeReviews { get; set; } = false;
}
