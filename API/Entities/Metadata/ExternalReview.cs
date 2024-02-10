using System.Collections.Generic;
using API.Services.Plus;

namespace API.Entities.Metadata;

/// <summary>
/// Represents an Externally supplied Review for a given Series
/// </summary>
public class ExternalReview
{
    public int Id { get; set; }
    public string Tagline { get; set; }
    public required string Body { get; set; }
    /// <summary>
    /// Pure text version of the body
    /// </summary>
    public required string BodyJustText { get; set; }
    /// <summary>
    /// Raw from the provider. Usually Markdown
    /// </summary>
    public string RawBody { get; set; }
    public required ScrobbleProvider Provider { get; set; }
    public string SiteUrl { get; set; }
    /// <summary>
    /// Reviewer's username
    /// </summary>
    public string Username { get; set; }
    /// <summary>
    /// An Optional Rating coming from the Review
    /// </summary>
    public int Rating { get; set; } = 0;
    /// <summary>
    /// The media's overall Score
    /// </summary>
    public int Score { get; set; }
    public int TotalVotes { get; set; }


    public int SeriesId { get; set; }

    // Relationships
    public ICollection<ExternalSeriesMetadata> ExternalSeriesMetadatas { get; set; } = null!;
}
