using System.Collections.Generic;

namespace API.DTOs.ReadingLists;

public enum CblImportResult {
    /// <summary>
    /// There was an issue which prevented processing
    /// </summary>
    Fail = 0,
    /// <summary>
    /// Some items were added, but not all
    /// </summary>
    Partial = 1,
    /// <summary>
    /// Everything was imported correctly
    /// </summary>
    Success = 2
}

public class CblBookResult
{
    public string Series { get; set; }
    public string Volume { get; set; }
    public string Number { get; set; }
    public string Reason { get; set; }
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

}
