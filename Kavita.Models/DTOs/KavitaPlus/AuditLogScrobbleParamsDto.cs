using Kavita.Models.DTOs.Scrobbling;
using Kavita.Models.Entities.Enums;
using Kavita.Models.Entities.Enums.KavitaPlus;
using Kavita.Models.Entities.Enums.UserPreferences;

namespace Kavita.Models.DTOs.KavitaPlus;
#nullable enable

/// <summary>
/// Internal typed payload written into the Payload column for scrobble audit entries.
/// Not returned directly by the API, projected to <see cref="KavitaPlusScrobbleDetailsDto"/> on read.
/// </summary>
public sealed record AuditLogScrobbleParamsDto
{
    public ScrobbleProvider Provider { get; init; }
    public ScrobbleEventType? ScrobbleEventType { get; init; }
    public int? ChapterNumber { get; init; }
    public float? VolumeNumber { get; init; }
    public float? PercentRead { get; init; }
    public int? TotalReadCountForSeries { get; init; }
    public float? Rating { get; init; }
    public string? ReviewBody { get; init; }
    public ScrobbleReadStatus ReadStatus { get; init; }
    public LibraryType LibraryType { get; init; } = LibraryType.Manga;
    /// <summary>
    /// Set when the event was produced by a read-status transition rule, identifying which rule fired.
    /// </summary>
    public TransitionRuleKind? TransitionRuleKind { get; init; }
}
