using System.Collections.Generic;

namespace API.Entities.Metadata;

/// <summary>
/// External Metadata from Kavita+ for a Chapter
/// </summary>
/// <remarks>
/// As apposed to <see cref="ExternalSeriesMetadata"/>,
/// we do not have a ValidUntilUtc, as this is only matched together with the series.
/// </remarks>
public class ExternalChapterMetadata
{
    public int Id { get; set; }

    public int ChapterId { get; set; }

    public ICollection<ExternalChapterReview> ExternalReviews { get; set; } = null!;

}
