using System;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using API.DTOs;
using API.DTOs.ReadingLists;
using API.Entities.Enums;
using API.Services.Tasks.Scanner.Parser;

namespace API.Services;
#nullable enable

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
    /// Formats a reading list item title from raw values.
    /// </summary>
    string FormatReadingListItemTitle( LibraryType libraryType, MangaFormat format, string? chapterNumber, string? volumeNumber, string? chapterTitleName, bool isSpecial, string? volumeLabel = null, string? chapterLabel = null, string? issueLabel = null, string? bookLabel = null);
}

public partial class EntityNamingService : IEntityNamingService
{
    private const string DefaultVolumeLabel = "Volume";
    private const string DefaultChapterLabel = "Chapter";
    private const string DefaultIssueLabel = "Issue";
    private const string DefaultBookLabel = "Book";
    private const string DefaultHashMark = "#";

    [GeneratedRegex(@"^\d+(\.\d+)?$", RegexOptions.Compiled)]
    private static partial Regex JustNumbersRegex();

    public string FormatChapterTitle(LibraryType libraryType, ChapterDto chapter,
        string? chapterLabel = null, string? issueLabel = null, string? bookLabel = null)
    {
        return FormatChapterTitle(libraryType, chapter.IsSpecial, chapter.Range, chapter.Title,
            chapterLabel, issueLabel, bookLabel);
    }

    public string FormatChapterTitle(LibraryType libraryType, bool isSpecial, string range, string? title,
        string? chapterLabel = null, string? issueLabel = null, string? bookLabel = null, bool withHash = true)
    {
        if (isSpecial)
        {
            return Parser.CleanSpecialTitle(title!);
        }

        chapterLabel ??= DefaultChapterLabel;
        issueLabel ??= DefaultIssueLabel;
        bookLabel ??= DefaultBookLabel;
        var hashMark = withHash ? DefaultHashMark : string.Empty;

        var baseTitle = libraryType switch
        {
            LibraryType.Book => $"{bookLabel} {title}".Trim(),
            LibraryType.LightNovel => $"{bookLabel} {range}".Trim(),
            LibraryType.Comic or LibraryType.ComicVine => $"{issueLabel} {hashMark}{range}".Trim(),
            LibraryType.Manga or LibraryType.Image => $"{chapterLabel} {range}".Trim(),
            _ => $"{chapterLabel} {range}".Trim()
        };

        // Append title only if it adds new information
        if (ShouldAppendTitle(title, range, baseTitle, libraryType))
        {
            baseTitle += $" - {title}";
        }

        return baseTitle;
    }

    public string? FormatVolumeName(LibraryType libraryType, VolumeDto volume, string? volumeLabel = null)
    {
        if (volume.IsSpecial())
        {
            return null;
        }

        volumeLabel ??= DefaultVolumeLabel;

        if (libraryType is LibraryType.Book or LibraryType.LightNovel)
        {
            return FormatBookVolumeName(volume);
        }

        return FormatStandardVolumeName(volume.Name, volumeLabel);
    }

    public string BuildFullTitle(LibraryType libraryType, SeriesDto series, VolumeDto? volume, ChapterDto chapter,
        string? volumeLabel = null, string? chapterLabel = null, string? issueLabel = null, string? bookLabel = null)
    {
        var seriesName = series.Name!;
        volumeLabel ??= DefaultVolumeLabel;

        // No volume context
        if (volume == null)
        {
            var chapterTitle = FormatChapterTitle(libraryType, chapter, chapterLabel, issueLabel, bookLabel);
            return $"{seriesName} - {chapterTitle}";
        }

        var title = BuildChapterTitle(libraryType, volume, chapter, volumeLabel, chapterLabel, issueLabel, bookLabel);

        return string.IsNullOrEmpty(title)
            ? seriesName
            : $"{seriesName} - {title}";
    }

    public string BuildChapterTitle(LibraryType libraryType, VolumeDto volume, ChapterDto chapter, string? volumeLabel = null,
        string? chapterLabel = null, string? issueLabel = null, string? bookLabel = null)
    {
        volumeLabel ??= DefaultVolumeLabel;

        // Special volume - just use chapter title
        if (volume.IsSpecial())
        {
            return FormatChapterTitle(libraryType, chapter, chapterLabel, issueLabel, bookLabel);
        }

        // Loose-leaf volume
        if (volume.IsLooseLeaf())
        {
            return volume.Chapters.Count == 1
                ? string.Empty
                : FormatChapterTitle(libraryType, chapter, chapterLabel, issueLabel, bookLabel);
        }

        // Single chapter in volume - use volume name only
        if (volume.Chapters.Count == 1)
        {
            return FormatVolumeName(libraryType, volume, volumeLabel) ?? string.Empty;
        }

        // Multiple chapters in volume - include both volume and chapter
        var volName = FormatVolumeName(libraryType, volume, volumeLabel)
                      ?? FormatStandardVolumeName(volume.Name, volumeLabel);
        var chapTitle = FormatChapterTitle(libraryType, chapter, chapterLabel, issueLabel, bookLabel);

        if (string.IsNullOrEmpty(volName))
        {
            return chapTitle;
        }

        return $"{volName} - {chapTitle}";
    }

    public string FormatReadingListItemTitle(ReadingListItemDto item,
        string? volumeLabel = null, string? chapterLabel = null, string? issueLabel = null, string? bookLabel = null)
    {
        return FormatReadingListItemTitle(
            item.LibraryType,
            item.SeriesFormat,
            item.ChapterNumber,
            item.VolumeNumber,
            item.ChapterTitleName,
            item.IsSpecial,
            volumeLabel,
            chapterLabel,
            issueLabel,
            bookLabel);
    }

    public string FormatReadingListItemTitle(
        LibraryType libraryType,
        MangaFormat format,
        string? chapterNumber,
        string? volumeNumber,
        string? chapterTitleName,
        bool isSpecial,
        string? volumeLabel = null,
        string? chapterLabel = null,
        string? issueLabel = null,
        string? bookLabel = null)
    {
        volumeLabel ??= DefaultVolumeLabel;
        chapterLabel ??= DefaultChapterLabel;
        issueLabel ??= DefaultIssueLabel;
        bookLabel ??= DefaultBookLabel;

        // Handle epub format with special logic
        if (format == MangaFormat.Epub)
        {
            return FormatEpubReadingListTitle(chapterNumber, volumeNumber, chapterTitleName, volumeLabel);
        }

        // Try volume-only title first (when chapter is default but volume is real)
        if (Parser.IsDefaultChapter(chapterNumber) && !Parser.IsLooseLeafVolume(volumeNumber))
        {
            return $"{volumeLabel} {volumeNumber}";
        }

        // Clean chapter number for display
        var displayChapterNumber = GetDisplayChapterNumber(chapterNumber);

        // Default chapter with title name
        if (Parser.IsDefaultChapter(chapterNumber) && !string.IsNullOrEmpty(chapterTitleName))
        {
            return chapterTitleName;
        }

        // Special chapter
        if (isSpecial)
        {
            return !string.IsNullOrEmpty(chapterTitleName)
                ? chapterTitleName
                : displayChapterNumber ?? string.Empty;
        }

        // Standard chapter formatting based on library type
        var chapterPrefix = GetChapterPrefix(libraryType, chapterLabel, issueLabel, bookLabel);
        return $"{chapterPrefix}{displayChapterNumber}";
    }

    #region Reading List Helpers

    /// <summary>
    /// Handles the special epub formatting logic for reading list items.
    /// </summary>
    private static string FormatEpubReadingListTitle(
        string? chapterNumber,
        string? volumeNumber,
        string? chapterTitleName,
        string volumeLabel)
    {
        var cleanedChapterNumber = Parser.CleanSpecialTitle(chapterNumber);

        // Default/empty chapter number
        if (Parser.IsDefaultChapter(cleanedChapterNumber))
        {
            // Prefer title name if available
            if (!string.IsNullOrEmpty(chapterTitleName))
            {
                return chapterTitleName;
            }

            // Fall back to volume
            var cleanedVolume = Parser.CleanSpecialTitle(volumeNumber);
            return $"{volumeLabel} {cleanedVolume}";
        }

        // Special volume marker - just use cleaned chapter
        if (volumeNumber == Parser.SpecialVolume)
        {
            return cleanedChapterNumber;
        }

        // Regular epub with chapter number
        return $"{volumeLabel} {cleanedChapterNumber}";
    }

    /// <summary>
    /// Gets the display-ready chapter number, cleaning special characters if needed.
    /// </summary>
    private static string? GetDisplayChapterNumber(string? chapterNumber)
    {
        if (string.IsNullOrEmpty(chapterNumber))
        {
            return null;
        }

        // If it's just numbers (including decimals like "1.5"), return as-is
        if (JustNumbersRegex().IsMatch(chapterNumber))
        {
            return chapterNumber;
        }

        // Otherwise clean special title formatting
        return Parser.CleanSpecialTitle(chapterNumber);
    }

    /// <summary>
    /// Gets the chapter prefix string based on library type.
    /// Maps to ReaderService.FormatChapterName logic.
    /// </summary>
    private static string GetChapterPrefix(
        LibraryType libraryType,
        string chapterLabel,
        string issueLabel,
        string bookLabel)
    {
        return libraryType switch
        {
            LibraryType.Comic or LibraryType.ComicVine => $"{issueLabel} #",
            LibraryType.Book or LibraryType.LightNovel => $"{bookLabel} ",
            _ => $"{chapterLabel} "
        };
    }

    #endregion

    #region Volume Helpers

    /// <summary>
    /// Formats volume name for book/light novel libraries.
    /// </summary>
    private static string? FormatBookVolumeName(VolumeDto volume)
    {
        var firstChapter = volume.Chapters.Count > 0 ? volume.Chapters.First() : null;

        if (firstChapter == null)
        {
            return volume.Name;
        }

        // Specials handled by caller
        if (firstChapter.IsSpecial)
        {
            return null;
        }

        // Has explicit title name
        if (!string.IsNullOrEmpty(firstChapter.TitleName))
        {
            return volume.IsLooseLeaf() ? volume.Name : firstChapter.TitleName;
        }

        // Loose-leaf without title
        if (Parser.IsLooseLeafVolume(firstChapter.Range))
        {
            // Volume is real (not loose-leaf) - it has a meaningful name, use it
            if (!volume.IsLooseLeaf())
            {
                return volume.Name;
            }
        }

        // Extract title from filename
        var fileTitle = Path.GetFileNameWithoutExtension(firstChapter.Range);
        if (string.IsNullOrEmpty(fileTitle))
        {
            return volume.Name;
        }

        return $"{volume.Name} - {fileTitle}";
    }

    /// <summary>
    /// Formats volume name for standard (non-book) libraries.
    /// Handles cases where volume.Name may already contain the label.
    /// </summary>
    private static string FormatStandardVolumeName(string volumeName, string volumeLabel)
    {
        if (Parser.IsLooseLeafVolume(volumeName))
        {
            return string.Empty;
        }

        // Already has the label - return as-is
        if (HasVolumePrefix(volumeName, volumeLabel))
        {
            return volumeName;
        }

        return $"{volumeLabel} {volumeName}".Trim();
    }

    /// <summary>
    /// Checks if the volume name already starts with a volume-like prefix.
    /// Handles localized labels and common variations.
    /// </summary>
    private static bool HasVolumePrefix(string volumeName, string volumeLabel)
    {
        if (string.IsNullOrEmpty(volumeName))
        {
            return false;
        }

        // Check for the provided label
        if (volumeName.StartsWith(volumeLabel, StringComparison.OrdinalIgnoreCase))
        {
            return true;
        }

        // Check for common variations that might exist in data
        var commonPrefixes = new[] { "Volume", "Vol.", "Vol ", "V." };
        foreach (var prefix in commonPrefixes)
        {
            if (volumeName.StartsWith(prefix, StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }
        }

        return false;
    }

    #endregion

    #region Chapter Helpers

    /// <summary>
    /// Determines if the title should be appended to the base chapter title.
    /// Prevents duplication like "Chapter 1448 - Chapter 1448".
    /// </summary>
    private static bool ShouldAppendTitle(string? title, string range, string baseTitle, LibraryType libraryType)
    {
        // No title to append
        if (string.IsNullOrWhiteSpace(title))
        {
            return false;
        }

        // Books use title as the primary identifier
        if (libraryType == LibraryType.Book)
        {
            return false;
        }

        // Title is just the range number
        if (string.Equals(title, range, StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        // Title is already contained in the base title (e.g., "Chapter 1448" contains "Chapter 1448")
        if (baseTitle.Contains(title, StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        // Title contains the base title (e.g., title "Chapter 1448" when baseTitle is "Chapter 1448")
        if (title.Contains(baseTitle, StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        // Check if title is just a variation of "Chapter/Issue X" pattern
        if (IsRedundantChapterTitle(title, range))
        {
            return false;
        }

        return true;
    }

    /// <summary>
    /// Checks if the title is a redundant chapter/issue label pattern.
    /// E.g., "Chapter 1448", "Ch. 1448", "Issue #5", etc.
    /// </summary>
    private static bool IsRedundantChapterTitle(string title, string range)
    {
        var redundantPrefixes = new[]
        {
            "Chapter ", "Ch. ", "Ch ",
            "Issue ", "Issue #",
            "Episode ", "Ep. ", "Ep ",
            "Part ", "Pt. ", "Pt ",
            "#"
        };

        foreach (var prefix in redundantPrefixes)
        {
            // Title is "Chapter 1448" and range is "1448"
            if (title.StartsWith(prefix, StringComparison.OrdinalIgnoreCase))
            {
                var remainder = title[prefix.Length..].Trim();
                if (string.Equals(remainder, range, StringComparison.OrdinalIgnoreCase))
                {
                    return true;
                }
            }
        }

        return false;
    }

    #endregion
}
