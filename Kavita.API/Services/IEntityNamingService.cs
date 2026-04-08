using System.Threading.Tasks;
using Kavita.Models.DTOs;
using Kavita.Models.DTOs.ReadingLists;
using Kavita.Models.Entities.Enums;

namespace Kavita.API.Services;

/// <summary>
/// Provides consistent, testable naming for series, volumes, and chapters across the application.
/// All methods are pure functions with no side effects.
/// </summary>
public interface IEntityNamingService
{
    /// <summary>
    /// Formats a chapter title based on library type and chapter metadata.
    /// </summary>
    string FormatChapterTitle(LibraryType libraryType, ChapterDto chapter, string? chapterLabel = null, string? issueLabel = null, string? bookLabel = null);

    /// <summary>
    /// Formats a chapter title from raw values.
    /// </summary>
    string FormatChapterTitle(LibraryType libraryType, bool isSpecial, string range, string? title, string? chapterLabel = null, string? issueLabel = null, string? bookLabel = null, bool withHash = true);

    /// <summary>
    /// Formats a volume name based on library type and volume metadata.
    /// </summary>
    string? FormatVolumeName(LibraryType libraryType, VolumeDto volume, string? volumeLabel = null);
    /// <summary>
    /// Builds a full display title for a chapter within a series/volume context.
    /// Used for OPDS feeds, reading lists, etc.
    /// </summary>
    string BuildFullTitle(LibraryType libraryType, SeriesDto series, VolumeDto? volume, ChapterDto chapter, string? volumeLabel = null, string? chapterLabel = null, string? issueLabel = null, string? bookLabel = null);
    /// <summary>
    /// Builds a display title for a chapter within its volume context.
    /// Used when series context is not needed (e.g., reading history within a series grouping).
    /// </summary>
    string BuildChapterTitle(LibraryType libraryType, VolumeDto volume, ChapterDto chapter, string? volumeLabel = null, string? chapterLabel = null, string? issueLabel = null, string? bookLabel = null);
    /// <summary>
    /// Formats a reading list item title based on the item's metadata.
    /// Handles the unique naming conventions for reading list display.
    /// </summary>
    string FormatReadingListItemTitle(ReadingListItemDto item, string? volumeLabel = null, string? chapterLabel = null, string? issueLabel = null, string? bookLabel = null);

    /// <summary>
    /// Formats a reading list item title from nested chapter/volume DTOs.
    /// </summary>
    string FormatReadingListItemTitle(ReadingListItemChapterDto chapter, ReadingListItemVolumeDto volume, LibraryType libraryType, MangaFormat format, string? volumeLabel = null, string? chapterLabel = null, string? issueLabel = null, string? bookLabel = null);

    /// <summary>
    /// Formats a reading list item title from raw values.
    /// </summary>
    string FormatReadingListItemTitle( LibraryType libraryType, MangaFormat format, string? chapterNumber, string? volumeNumber, string? chapterTitleName, bool isSpecial, string? volumeLabel = null, string? chapterLabel = null, string? issueLabel = null, string? bookLabel = null);
}

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
        var volumeTask = localizationService.Translate(userId, "volume-num");
        var chapterTask = localizationService.Translate(userId, "chapter-num");
        var issueTask = localizationService.Translate(userId, "issue-num");
        var bookTask = localizationService.Translate(userId, "book-num");


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

    public string BuildChapterTitle(VolumeDto volume, ChapterDto chapter)
    {
        return _namingService.BuildChapterTitle(LibraryType, volume, chapter,
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
