using System.ComponentModel;

namespace Kavita.Models.DTOs.SignalR;

/// <summary>
/// How much attention a <see cref="SignalRMessageDto"/> needs from the user. Ordered, so higher values can be compared with &gt;=
/// </summary>
public enum MessageEventPriority
{
    /// <summary>
    /// UI refresh signals. Never shown in the events widget
    /// </summary>
    [Description("Silent")]
    Silent = 0,
    /// <summary>
    /// A background job with progress
    /// </summary>
    [Description("Activity")]
    Activity = 1,
    /// <summary>
    /// Something worth knowing, no action needed
    /// </summary>
    [Description("Info")]
    Info = 2,
    /// <summary>
    /// The user can resolve this with one action (e.g. reconnect an expired scrobble key)
    /// </summary>
    [Description("Action")]
    Action = 3,
    [Description("Error")]
    Error = 4,
}
