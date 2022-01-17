using System.ComponentModel;

namespace API.Entities.Enums;

/// <summary>
/// Represents Age Rating for content.
/// </summary>
/// <remarks>Based on ComicInfo.xml v2.1 https://github.com/anansi-project/comicinfo/blob/main/drafts/v2.1/ComicInfo.xsd</remarks>
public enum AgeRating
{
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
    [Description("Kids to Adults")]
    KidsToAdults = 6,
    [Description("Teen")]
    Teen = 7,
    [Description("MA 15+")]
    Mature15Plus = 8,
    [Description("Mature 17+")]
    Mature17Plus = 9,
    [Description("M")]
    Mature = 10,
    [Description("Adults Only 18+")]
    AdultsOnly = 11,
    [Description("X18+")]
    X18Plus = 12


}
