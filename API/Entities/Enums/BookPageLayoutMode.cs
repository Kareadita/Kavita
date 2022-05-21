using System.ComponentModel;

namespace API.Entities.Enums;

public enum BookPageLayoutMode
{
    [Description("Default")]
    Default = 0,
    [Description("1 Column")]
    Column1 = 1,
    [Description("2 Column")]
    Column2 = 2
}
