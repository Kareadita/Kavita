using System.ComponentModel;

namespace API.Entities.Enums;

public enum RatingAuthority
{
    /// <summary>
    /// Rating was from a User (internet or local)
    /// </summary>
    [Description("User")]
    User = 0,
    /// <summary>
    /// Rating was from Professional Critics
    /// </summary>
    [Description("Critic")]
    Critic = 1,
}
