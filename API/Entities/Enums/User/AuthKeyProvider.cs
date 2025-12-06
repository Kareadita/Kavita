using System.ComponentModel;

namespace API.Entities.Enums.User;

public enum AuthKeyProvider
{
    /// <summary>
    /// Provided by the User
    /// </summary>
    [Description("User")]
    User = 0,
    /// <summary>
    /// Provided by System
    /// </summary>
    [Description("System")]
    System = 1,
}
