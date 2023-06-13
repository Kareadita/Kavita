namespace API.DTOs.SeriesDetail;

/// <summary>
/// Represents a User Review for a given Series
/// </summary>
/// <remarks>The user does not need to be a Kavita user</remarks>
public class UserReviewDto
{
    /// <summary>
    /// A tagline for the review
    /// </summary>
    public string? Tagline { get; set; }

    /// <summary>
    /// The main review
    /// </summary>
    public string Body { get; set; }

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

    /// <summary>
    /// How many upvotes this review has gotten
    /// </summary>
    /// <remarks>More upvotes get loaded first</remarks>
    public int Score { get; set; } = 0;
}
