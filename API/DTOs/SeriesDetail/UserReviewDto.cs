using API.Services.Plus;

namespace API.DTOs.SeriesDetail;
#nullable enable

/// <summary>
/// Represents a User Review for a given Series
/// </summary>
/// <remarks>The user does not need to be a Kavita user</remarks>
public class UserReviewDto
{
    /// <summary>
    /// A tagline for the review
    /// </summary>
    /// <remarks>This is not possible to set as a local user</remarks>
    public string? Tagline { get; set; }
    /// <summary>
    /// The main review
    /// </summary>
    public string Body { get; set; }
    /// <summary>
    /// The main body with just text, for review preview
    /// </summary>
    public string? BodyJustText { get; set; }
    /// <summary>
    /// The series this is for
    /// </summary>
    public int SeriesId { get; set; }
    /// <summary>
    /// The library this series belongs in
    /// </summary>
    public int LibraryId { get; set; }
    /// <summary>
    /// The user who wrote this
    /// </summary>
    public string Username { get; set; }
    public int TotalVotes { get; set; }
    public float Rating { get; set; }
    public string? RawBody { get; set; }
    /// <summary>
    /// How many upvotes this review has gotten
    /// </summary>
    /// <remarks>More upvotes get loaded first</remarks>
    public int Score { get; set; } = 0;
    /// <summary>
    /// If External, the url of the review
    /// </summary>
    public string? SiteUrl { get; set; }
    /// <summary>
    /// Does this review come from an external Source
    /// </summary>
    public bool IsExternal { get; set; }
    /// <summary>
    /// If this review is External, which Provider did it come from
    /// </summary>
    public ScrobbleProvider Provider { get; set; } = ScrobbleProvider.Kavita;
}
