using API.Entities.Enums;

namespace API.DTOs.Reader;

public sealed record RereadDto
{
    /// <summary>
    /// Should the prompt be shown
    /// </summary>
    public required bool ShouldPrompt { get; init; }
    /// <summary>
    /// If the prompt is triggered because of time, false when triggered because of fully read
    /// </summary>
    public bool TimePrompt { get; init; } = false;
    /// <summary>
    /// True if the entity is not atomic and will be fully reread on reread (I.e. rereading a series on volume)
    /// </summary>
    public bool FullReread { get; init; } = false;
    /// <summary>
    /// Days elapsed since <see cref="ChapterOnReread"/> was last read
    /// </summary>
    public int DaysSinceLastRead { get; init; }
    /// <summary>
    /// The chapter to open if continue is selected
    /// </summary>
    public RereadChapterDto ChapterOnContinue { get; init; }
    /// <summary>
    /// The chapter to open if reread is selected, this may be equal to <see cref="ChapterOnContinue"/>
    /// </summary>
    public RereadChapterDto ChapterOnReread { get; init; }

    public static RereadDto Dont()
    {
        return new RereadDto
        {
            ShouldPrompt = false
        };
    }
}

public sealed record RereadChapterDto(int LibraryId, int SeriesId, int VolumeId, int ChapterId, string Label, MangaFormat? Format);
