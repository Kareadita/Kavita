using System.ComponentModel;

namespace API.Entities.Enums;

/// <summary>
/// Represents Age Rating for content.
/// </summary>
/// <remarks>Based on ComicInfo.xml v2.1 https://github.com/anansi-project/comicinfo/blob/main/drafts/v2.1/ComicInfo.xsd</remarks>
public enum AgeRating
{
    /// <summary>
    /// This is for Age Restriction for Restricted Profiles
    /// </summary>
    [Description("Not Applicable")]
    NotApplicable = -1,
    [Description("Unknown")]
    Unknown = 0,
    [Description("Rating Pending")]
    RatingPending = 1,
    [Description("Early Childhood")]
    EarlyChildhood = 2,
    [Description("Everyone")]
    Everyone = 3,
    [Description("G")]
    G = 4,
    [Description("Everyone 10+")]
    Everyone10Plus = 5,
    [Description("PG")]
    // ReSharper disable once InconsistentNaming
    PG = 6,
    [Description("Kids to Adults")]
    KidsToAdults = 7,
    [Description("Teen")]
    Teen = 8,
    [Description("MA15+")]
    Mature15Plus = 9,
    [Description("Mature 17+")]
    Mature17Plus = 10,
    [Description("M")]
    Mature = 11,
    [Description("R18+")]
    R18Plus = 12,
    [Description("Adults Only 18+")]
    AdultsOnly = 13,
    [Description("X18+")]
    X18Plus = 14


}
