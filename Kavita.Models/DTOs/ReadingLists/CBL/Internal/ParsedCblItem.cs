using System.Collections.Generic;

namespace Kavita.Models.DTOs.ReadingLists.CBL.Internal;

/// <summary>
/// A single issue/book entry in a unified (V1+V2) parsed reading list
/// </summary>
public sealed record ParsedCblItem
{
    /// <summary>
    /// Zero-based position of this item in the reading list
    /// </summary>
    public int Order { get; set; }
    /// <summary>
    /// Name of the comic series. Sourced from V1 <c>Book/@Series</c> or V2 <c>seriesName</c>
    /// </summary>
    public string SeriesName { get; set; } = string.Empty;
    /// <summary>
    /// Issue/chapter number. Sourced from V1 <c>Book/@Number</c> or V2 <c>issueNumber</c>
    /// </summary>
    public string Number { get; set; } = string.Empty;
    /// <summary>
    /// Volume identifier. V1: <c>Book/@Volume</c> (often the year). V2: derived from <c>seriesStartYear</c>
    /// </summary>
    public string Volume { get; set; } = string.Empty;
    /// <summary>
    /// Publication year. V1: <c>Book/@Year</c>. V2: extracted from <c>issueCoverDate</c>
    /// </summary>
    public string Year { get; set; } = string.Empty;
    /// <summary>
    /// V1-only format tag (e.g. "Main Series", "Annual"). Maps to ComicInfo Format
    /// </summary>
    public string Format { get; set; } = string.Empty;
    /// <summary>
    /// V1-only file type hint (Kavita extension, not part of the CBL standard)
    /// </summary>
    public string FileType { get; set; } = string.Empty;
    /// <summary>
    /// Full cover date string from V2 (ISO 8601 YYYY-MM-DD). Empty for V1
    /// </summary>
    public string CoverDate { get; set; } = string.Empty;
    /// <summary>
    /// Issue classification from V2. Always <see cref="CblIssueType.Unknown"/> for V1
    /// </summary>
    public CblIssueType IssueType { get; set; } = CblIssueType.Unknown;
    /// <summary>
    /// External database references for this issue.
    /// </summary>
    public List<CblExternalId> ExternalIds { get; set; } = new();
}
