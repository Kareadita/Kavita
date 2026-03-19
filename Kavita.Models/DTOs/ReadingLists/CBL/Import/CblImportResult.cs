using System.ComponentModel;

namespace Kavita.Models.DTOs.ReadingLists.CBL.Import;

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
