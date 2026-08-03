namespace Kavita.Models.DTOs.Kobo;

/// <summary>
/// Restore request for Removed-from-Kobo. Null or empty <see cref="ChapterIds"/> restores all
/// still-eligible device-deleted archives; otherwise only the listed chapter ids.
/// </summary>
public sealed record RestoreKoboRemovedBooksDto
{
    public int[]? ChapterIds { get; init; }
}
