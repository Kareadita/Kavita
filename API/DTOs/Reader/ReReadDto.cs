using API.Entities.Enums;

namespace API.DTOs.Reader;

public sealed record ReReadDto
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
    /// Days elapsed since <see cref="ChapterOnReRead"/> was last read
    /// </summary>
    public int DaysSinceLastRead { get; init; }
    /// <summary>
    /// The chapter to open if continue is selected
    /// </summary>
    public ReReadChapterDto ChapterOnContinue { get; init; }
    /// <summary>
    /// The chapter to open if reread is selected, this may be equal to <see cref="ChapterOnContinue"/>
    /// </summary>
    public ReReadChapterDto ChapterOnReRead { get; init; }

    public static ReReadDto Dont()
    {
        return new ReReadDto
        {
            ShouldPrompt = false
        };
    }
}

public sealed record ReReadChapterDto(int LibraryId, int SeriesId, int ChapterId, string Label, MangaFormat? Format);
