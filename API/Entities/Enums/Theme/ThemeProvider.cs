using System;
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
    [Obsolete("User themes have been deprecated and Downloaded should be used instead")]
    [Description("User")]
    User = 2,
    /// <summary>
    /// Theme was downloaded via Kavita Themes Repo
    /// </summary>
    [Description("Provided")]
    Provided = 3
}
