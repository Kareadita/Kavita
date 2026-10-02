using System;

namespace Kavita.Models.DTOs.SignalR;
#nullable enable

/// <summary>
/// Payload for SignalR messages to Frontend
/// </summary>
public sealed record SignalRMessageDto
{
    /// <summary>
    /// Body of the event type
    /// </summary>
    public object? Body { get; set; }
    public required string Name { get; set; }
    /// <summary>
    /// User-friendly Title of the Event
    /// </summary>
    /// <example>Scanning Manga</example>
    public string Title { get; set; } = string.Empty;
    /// <summary>
    /// User-friendly subtitle. Should have extra info
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
    public DateTime EventTimeUtc { get; set; } = DateTime.UtcNow;
    /// <summary>
    /// How much attention this needs. Null when the sender did not set one, the UI then infers it from <see cref="Name"/>
    /// </summary>
    public MessageEventPriority? Priority { get; set; }
    /// <summary>
    /// Stable key the UI translates. <see cref="Title"/> and <see cref="SubTitle"/> remain the English fallback
    /// </summary>
    public string? Code { get; set; }
    /// <summary>
    /// Ties every message of a single job run together
    /// </summary>
    public string? CorrelationId { get; set; }
}
