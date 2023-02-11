using System.Collections.Generic;
using System.ComponentModel;
using API.DTOs.ReadingLists.CBL;

namespace API.DTOs.ReadingLists;

public enum CblImportResult {
    /// <summary>
    /// There was an issue which prevented processing
    /// </summary>
    [Description("Fail")]
    Fail = 0,
    /// <summary>
    /// Some items were added, but not all
    /// </summary>
    [Description("Partial")]
    Partial = 1,
    /// <summary>
    /// Everything was imported correctly
    /// </summary>
    [Description("Success")]
    Success = 2
}

public enum CblImportReason
{
    /// <summary>
    /// The Chapter is not present in Kavita
    /// </summary>
    [Description("Chapter missing")]
    ChapterMissing = 0,
    /// <summary>
    /// The Volume is not present in Kavita or no Volume field present in CBL and there is no chapter matching
    /// </summary>
    [Description("Volume missing")]
    VolumeMissing = 1,
    /// <summary>
    /// The Series is not present in Kavita or the user does not have access to the Series due to some account restrictions
    /// </summary>
    [Description("Series missing")]
    SeriesMissing = 2,
    /// <summary>
    /// The CBL Name conflicts with another Reading List in the system
    /// </summary>
    [Description("Name Conflict")]
    NameConflict = 3,
    /// <summary>
    /// Every Series in the Reading list is missing from within Kavita or user has access restrictions to
    /// </summary>
    [Description("All Series Missing")]
    AllSeriesMissing = 4,
    /// <summary>
    /// There are no Book entries in the CBL
    /// </summary>
    [Description("Empty File")]
    EmptyFile = 5,
}

public class CblBookResult
{
    public string Series { get; set; }
    public string Volume { get; set; }
    public string Number { get; set; }
    public CblImportReason Reason { get; set; }

    public CblBookResult(CblBook book)
    {
        Series = book.Series;
        Volume = book.Volume;
        Number = book.Number;
    }

    public CblBookResult()
    {

    }
}

/// <summary>
/// Represents the summary from the Import of a given CBL
/// </summary>
public class CblImportSummaryDto
{
    public string CblName { get; set; }
    public ICollection<CblBookResult> Results { get; set; }
    public CblImportResult Success { get; set; }
    public ICollection<CblBookResult> SuccessfulInserts { get; set; }
    /// <summary>
    /// A list of Series that are within the CBL but map to multiple libraries within Kavita
    /// </summary>
    public IList<SeriesDto> Conflicts { get; set; }
    public IList<CblConflictQuestion> Conflicts2 { get; set; }

}
