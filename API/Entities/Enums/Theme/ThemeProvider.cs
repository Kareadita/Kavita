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
    User = 2,
    /// <summary>
    /// Theme was downloaded via Kavita Themes Repo
    /// </summary>
    [Description("Downloaded")]
    Downloaded = 3
}
