using API.Entities;

namespace API.DTOs.Metadata;

public sealed record BrowseTagDto : TagDto
{
    /// <summary>
    /// Number of Series this Entity is on
    /// </summary>
    public int SeriesCount { get; set; }
    /// <summary>
    /// Number of Issues this Entity is on
    /// </summary>
    public int IssueCount { get; set; }
}
