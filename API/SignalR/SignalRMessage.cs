using System;

namespace API.SignalR;
#nullable enable

/// <summary>
/// Payload for SignalR messages to Frontend
/// </summary>
public class SignalRMessage
{
    /// <summary>
    /// Body of the event type
    /// </summary>
    public object? Body { get; set; }
    public required string Name { get; set; }
    /// <summary>
    /// User friendly Title of the Event
    /// </summary>
    /// <example>Scanning Manga</example>
    public string Title { get; set; } = string.Empty;
    /// <summary>
    /// User friendly subtitle. Should have extra info
    /// </summary>
    /// <example>C:/manga/Accel World V01.cbz</example>
    public string SubTitle { get; set; } = string.Empty;
    /// <summary>
    /// Represents what this represents. started | updated | ended | single
    /// <see cref="ProgressEventType"/>
    /// </summary>
    public string EventType { get; set; } = ProgressEventType.Updated;
    /// <summary>
    /// How should progress be represented. If Determinate, the Body MUST have a Progress float on it.
    /// </summary>
    public string Progress { get; set; } = ProgressType.None;
    /// <summary>
    /// When event took place
    /// </summary>
    public readonly DateTime EventTime = DateTime.Now;
}
