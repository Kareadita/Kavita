using System.Collections.Generic;

namespace Kavita.Models.DTOs.ReadingLists.CBL.Import;

/// <summary>
/// Represents the summary from the Import of a given CBL
/// </summary>
public sealed record CblImportSummaryDto
{
    public string CblName { get; set; }
    /// <summary>
    /// Used only for Kavita's UI, the filename of the cbl
    /// </summary>
    public string FileName { get; set; }
    public ICollection<CblBookResult> Results { get; set; }
    public CblImportResult Success { get; set; }
    public ICollection<CblBookResult> SuccessfulInserts { get; set; }
    /// <summary>
    /// Are we updating a pre-existing list or not
    /// </summary>
    public bool IsUpdate { get; set; }

}
