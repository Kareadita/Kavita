using Kavita.Models.DTOs.Scrobbling;
using Kavita.Models.Entities.Enums;

namespace Kavita.Models.DTOs.KavitaPlus;
#nullable enable

/// <summary>
/// Internal typed payload written into the Payload column for scrobble audit entries.
/// Not returned directly by the API — projected to <see cref="KavitaPlusScrobbleDetailsDto"/> on read.
/// </summary>
public sealed record AuditLogScrobbleParamsDto
{
    public ScrobbleEventType? ScrobbleEventType { get; init; }
    public int? ChapterNumber { get; init; }
    public float? VolumeNumber { get; init; }
    public float? Rating { get; init; }
    public LibraryType LibraryType { get; init; } = LibraryType.Manga;
}
