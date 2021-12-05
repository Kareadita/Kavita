using System.ComponentModel;

namespace API.Entities.Enums;

public enum AgeRating
{
    [Description("Unknown")]
    Unknown = 0,
    [Description("Adults Only 18+")]
    AdultsOnly = 1,
    [Description("Early Childhood")]
    EarlyChildhood = 2,
    [Description("Everyone")]
    Everyone = 3,
    [Description("Everyone 10+")]
    Everyone10Plus = 4,
    [Description("G")]
    G = 5,
    [Description("Kids to Adults")]
    KidsToAdults = 6,
    [Description("Mature")]
    Mature = 7,
    [Description("Mature 15+")]
    Mature15Plus = 8,
    [Description("Mature 17+")]
    Mature17Plus = 9,
    [Description("Rating Pending")]
    RatingPending = 10,
    [Description("Teen")]
    Teen = 11,
    [Description("X 18+")]
    X18Plus = 12


}
