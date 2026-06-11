using Kavita.Models.Entities.Enums;
using Kavita.Models.Entities.Enums.KavitaPlus;
using Kavita.Models.Entities.Enums.UserPreferences;

namespace Kavita.Models.DTOs.KavitaPlus;
#nullable enable

/// <summary>
/// Scrobble-specific context surfaced on a Kavita+ audit entry. Projected from <see cref="AuditLogScrobbleParamsDto"/>.
/// </summary>
public sealed record KavitaPlusScrobbleDetailsDto
{
    public ScrobbleEventType? ScrobbleEventType { get; init; }
    public int? ChapterNumber { get; init; }
    public float? VolumeNumber { get; init; }
    public float? PercentRead { get; init; }
    public float? Rating { get; init; }
    public string? ReviewBody { get; init; }
    public ScrobbleReadStatus? ReadStatus { get; init; }
    public ScrobbleProvider Provider { get; init; } = ScrobbleProvider.AniList;
    public LibraryType LibraryType { get; init; } = LibraryType.Manga;
}
