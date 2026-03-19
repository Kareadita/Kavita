namespace Kavita.Models.DTOs.ReadingLists.CBL.Internal;

/// <summary>
/// A resolved external-database reference for a series/issue pair.
/// Populated from V1 <c>Database</c> elements or V2 <c>issueList[].id[]</c> entries.
/// </summary>
public sealed record CblExternalId
{
    /// <summary>
    /// The external database provider (e.g. ComicVine, Metron).
    /// </summary>
    public CblExternalDbProvider Provider { get; set; }
    /// <summary>
    /// Provider-specific series identifier.
    /// </summary>
    public string SeriesId { get; set; } = string.Empty;
    /// <summary>
    /// Provider-specific issue identifier.
    /// </summary>
    public string IssueId { get; set; } = string.Empty;
}
