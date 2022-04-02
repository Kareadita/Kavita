using System.ComponentModel;

namespace API.Entities.Enums.Theme;

public enum ThemeProvider
{
    /// <summary>
    /// Theme is provided by System
    /// </summary>
    [Description("System")]
    System = 1,
    /// <summary>
    /// Theme is provided by the User (ie it's custom)
    /// </summary>
    [Description("User")]
    User = 2
}
