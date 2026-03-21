using System.Collections.Generic;
using Kavita.Models.DTOs.ReadingLists.CBL.Internal;
using Kavita.Models.Entities.Enums;

namespace Kavita.Models.DTOs.ReadingLists.CBL.Import;
#nullable enable

public sealed record CblBookResult
{
    /// <summary>
    /// Order in the CBL
    /// </summary>
    public int Order { get; set; }
    public string Series { get; set; }
    public string Volume { get; set; }
    public string Number { get; set; }
    /// <summary>
    /// Used on Series conflict
    /// </summary>
    public int LibraryId { get; set; }
    /// <summary>
    /// Used on Series conflict
    /// </summary>
    public int SeriesId { get; set; }
    /// <summary>
    /// The name of the reading list
    /// </summary>
    public string ReadingListName { get; set; }
    public CblImportReason Reason { get; set; }
    /// <summary>
    /// Which matching tier resolved this item (null if not processed by new matcher)
    /// </summary>
    public CblMatchTier? MatchTier { get; set; }
    /// <summary>
    /// The matched chapter's ID (0 if not matched)
    /// </summary>
    public int ChapterId { get; set; }
    /// <summary>
    /// Display title of the matched chapter (e.g., range or title)
    /// </summary>
    public string ChapterTitle { get; set; } = string.Empty;
    /// <summary>
    /// The Kavita series name this item matched to (empty if unmatched)
    /// </summary>
    public string MatchedSeriesName { get; set; } = string.Empty;
    /// <summary>
    /// The library type of the matched series (for entity-title rendering)
    /// </summary>
    public LibraryType LibraryType { get; set; }
    /// <summary>
    /// The raw chapter range/number (e.g. "5", "10.5") — separate from ChapterTitle
    /// </summary>
    public string ChapterNumber { get; set; } = string.Empty;
    /// <summary>
    /// When a SeriesCollision occurs, the candidate series the user can choose from
    /// </summary>
    public IList<CblSeriesCandidate> Candidates { get; set; }

    public CblBookResult(ParsedCblItem item)
    {
        Series = item.SeriesName;
        Volume = item.Volume;
        Number = item.Number;
        Order = item.Order;
    }

    public CblBookResult() { }
}
