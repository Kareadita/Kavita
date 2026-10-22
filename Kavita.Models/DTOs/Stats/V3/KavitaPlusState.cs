using System.ComponentModel;

namespace Kavita.Models.DTOs.Stats.V3;

/// <summary>
/// For KavitaStats API Only
/// </summary>
public enum KavitaPlusState
{
    [Description("None")]
    None = 0,
    [Description("Active")]
    Active = 1,
    [Description("Cancelling")]
    Cancelling = 2,
    [Description("Expired")]
    Expired = 3,
    [Description("Paused")]
    Paused = 4,
    [Description("Unreachable")]
    Unreachable = 5
}
