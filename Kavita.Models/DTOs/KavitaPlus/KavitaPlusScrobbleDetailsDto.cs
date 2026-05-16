using Kavita.Models.DTOs.Scrobbling;
using Kavita.Models.Entities.Enums;

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
    public float? Rating { get; init; }
    public ScrobbleProvider Provider { get; init; } = ScrobbleProvider.AniList;
    public LibraryType LibraryType { get; init; } = LibraryType.Manga;
}
