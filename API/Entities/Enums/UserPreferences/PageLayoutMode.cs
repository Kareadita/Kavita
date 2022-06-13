using System.ComponentModel;

namespace API.Entities.Enums.UserPreferences;

public enum PageLayoutMode
{
    [Description("Cards")]
    Cards = 0,
    [Description("List")]
    List = 1
}
