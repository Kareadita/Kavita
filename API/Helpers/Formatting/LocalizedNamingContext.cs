using System.Threading.Tasks;
using API.DTOs;
using API.DTOs.ReadingLists;
using API.Entities.Enums;
using API.Services;

namespace API.Helpers.Formatting;
#nullable enable

/// <summary>
/// Pre-fetched localized labels for entity naming.
/// Create once per request context and reuse.
/// </summary>
public sealed class LocalizedNamingContext
{
    public LibraryType LibraryType { get; }
    public string VolumeLabel { get; }
    public string ChapterLabel { get; }
    public string IssueLabel { get; }
    public string BookLabel { get; }

    private readonly IEntityNamingService _namingService;

    private LocalizedNamingContext(
        IEntityNamingService namingService,
        LibraryType libraryType,
        string volumeLabel,
        string chapterLabel,
        string issueLabel,
        string bookLabel)
    {
        _namingService = namingService;
        LibraryType = libraryType;
        VolumeLabel = volumeLabel;
        ChapterLabel = chapterLabel;
        IssueLabel = issueLabel;
        BookLabel = bookLabel;
    }

    public static async Task<LocalizedNamingContext> CreateAsync(
        IEntityNamingService namingService,
        ILocalizationService localizationService,
        int userId,
        LibraryType libraryType)
    {
        var volumeTask = localizationService.Translate(userId, "volume-num", string.Empty);
        var chapterTask = localizationService.Translate(userId, "chapter-num", string.Empty);
        var issueTask = localizationService.Translate(userId, "issue-num", string.Empty, string.Empty);
        var bookTask = localizationService.Translate(userId, "book-num", string.Empty);

        await Task.WhenAll(volumeTask, chapterTask, issueTask, bookTask);

        return new LocalizedNamingContext(
            namingService,
            libraryType,
            (await volumeTask).Trim(),
            (await chapterTask).Trim(),
            (await issueTask).Trim(),
            (await bookTask).Trim());
    }

    public string FormatChapterTitle(ChapterDto chapter)
    {
        return _namingService.FormatChapterTitle(LibraryType, chapter, ChapterLabel, IssueLabel, BookLabel);
    }

    public string? FormatVolumeName(VolumeDto volume)
    {
        return _namingService.FormatVolumeName(LibraryType, volume, VolumeLabel);
    }

    public string BuildFullTitle(SeriesDto series, VolumeDto? volume, ChapterDto chapter)
    {
        return _namingService.BuildFullTitle(LibraryType, series, volume, chapter,
            VolumeLabel, ChapterLabel, IssueLabel, BookLabel);
    }

    /// <summary>
    /// Formats a reading list item title using the pre-fetched localized labels.
    /// </summary>
    public string FormatReadingListItemTitle(ReadingListItemDto item)
    {
        return _namingService.FormatReadingListItemTitle(
            item,
            VolumeLabel,
            ChapterLabel,
            IssueLabel,
            BookLabel);
    }
}
