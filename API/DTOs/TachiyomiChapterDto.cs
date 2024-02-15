namespace API.DTOs;

/// <summary>
/// This is explicitly for Tachiyomi. Number field was removed in v0.8.0, but Tachiyomi needs it for the hacks.
/// </summary>
public class TachiyomiChapterDto : ChapterDto
{
    /// <summary>
    /// Smallest number of the Range.
    /// </summary>
    public string Number { get; init; } = default!;
}
