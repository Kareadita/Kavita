using System.ComponentModel;

namespace API.Entities.Enums;

public enum CoverDisplayOption
{
    [Description("Default")]
    Default = 0,
    [Description("Random")]
    Random,
    [Description("Latest Volume")]
    LatestVolume
}
