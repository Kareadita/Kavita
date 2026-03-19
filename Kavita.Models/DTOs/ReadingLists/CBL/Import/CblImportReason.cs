using System.ComponentModel;

namespace Kavita.Models.DTOs.ReadingLists.CBL.Import;

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
    /// <summary>
    /// Series Collides between Libraries
    /// </summary>
    [Description("Series Collision")]
    SeriesCollision = 6,
    /// <summary>
    /// Every book chapter is missing or can't be matched
    /// </summary>
    [Description("All Chapters Missing")]
    AllChapterMissing = 7,
    /// <summary>
    /// The Chapter was imported
    /// </summary>
    [Description("Success")]
    Success = 8,
    /// <summary>
    /// The file does not match the XML spec
    /// </summary>
    [Description("Invalid File")]
    InvalidFile = 9,
}
