using System;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using Kavita.API.Services;
using Kavita.Models.DTOs;
using Kavita.Models.DTOs.ReadingLists;
using Kavita.Models.Entities.Enums;
using Kavita.Services.Extensions;
using Kavita.Services.Scanner;

namespace Kavita.Services;

public partial class EntityNamingService : IEntityNamingService
{
    private const string DefaultVolumeLabel = "Volume {0}";
    private const string DefaultChapterLabel = "Chapter {0}";
    private const string DefaultIssueLabel = "Issue {0}{1}";
    private const string DefaultBookLabel = "Book {0}";
    private const string DefaultHashMark = "#";

    [GeneratedRegex(@"^\d+(\.\d+)?$", RegexOptions.Compiled)]
    private static partial Regex JustNumbersRegex();

    [GeneratedRegex(@"\{\d+\}")]
    private static partial Regex FormatPlaceholderRegex();

    /// <summary>
    /// Validates that a label string contains at least one format placeholder (e.g., {0}).
    /// Throws <see cref="ArgumentException"/> if the placeholder is missing.
    /// This prevents silent data loss when callers pass plain strings to format-string parameters.
    /// </summary>
    private static void ValidateFormatLabel(string label, string paramName)
    {
        if (!FormatPlaceholderRegex().IsMatch(label))
        {
            throw new ArgumentException($"Label must contain at least one format placeholder (e.g., {{0}}).", paramName);
        }
    }

    public string FormatChapterTitle(LibraryType libraryType, ChapterDto chapter,
        string? chapterLabel = null, string? issueLabel = null, string? bookLabel = null)
    {
        var title = string.IsNullOrEmpty(chapter.TitleName) ? Parser.CleanSpecialTitle(chapter.Title) : chapter.TitleName;
        return FormatChapterTitle(libraryType, chapter.IsSpecial, chapter.Range, title,
            chapterLabel, issueLabel, bookLabel);
    }

    public string FormatChapterTitle(LibraryType libraryType, bool isSpecial, string range, string? title,
        string? chapterLabel = null, string? issueLabel = null, string? bookLabel = null, bool withHash = true)
    {
        if (isSpecial)
        {
            return title!;
        }

        chapterLabel ??= DefaultChapterLabel;
        issueLabel ??= DefaultIssueLabel;
        bookLabel ??= DefaultBookLabel;
        ValidateFormatLabel(chapterLabel, nameof(chapterLabel));
        ValidateFormatLabel(issueLabel, nameof(issueLabel));
        ValidateFormatLabel(bookLabel, nameof(bookLabel));

        var hashMark = withHash ? DefaultHashMark : string.Empty;

        var baseTitle = libraryType switch
        {
            LibraryType.Book => string.Format(bookLabel, title).Trim(),
            LibraryType.LightNovel => string.Format(bookLabel, range).Trim(),
            LibraryType.Comic or LibraryType.ComicVine => string.Format(issueLabel, hashMark, range).Trim(),
            LibraryType.Manga or LibraryType.Image => string.Format(chapterLabel, range).Trim(),
            _ => string.Format(chapterLabel, range).Trim()
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
        ValidateFormatLabel(volumeLabel, nameof(volumeLabel));

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
        ValidateFormatLabel(volumeLabel, nameof(volumeLabel));

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
        ValidateFormatLabel(volumeLabel, nameof(volumeLabel));

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

    public string FormatReadingListItemTitle(ReadingListItemChapterDto chapter, ReadingListItemVolumeDto volume,
        LibraryType libraryType, MangaFormat format,
        string? volumeLabel = null, string? chapterLabel = null, string? issueLabel = null, string? bookLabel = null)
    {
        return FormatReadingListItemTitle(
            libraryType,
            format,
            chapter.Range,
            volume.Name,
            chapter.TitleName,
            chapter.IsSpecial,
            volumeLabel,
            chapterLabel,
            issueLabel,
            bookLabel);
    }

    public string FormatReadingListItemTitle( LibraryType libraryType, MangaFormat format, string? chapterNumber,
        string? volumeNumber, string? chapterTitleName, bool isSpecial, string? volumeLabel = null,
        string? chapterLabel = null, string? issueLabel = null, string? bookLabel = null)
    {
        volumeLabel ??= DefaultVolumeLabel;
        chapterLabel ??= DefaultChapterLabel;
        issueLabel ??= DefaultIssueLabel;
        bookLabel ??= DefaultBookLabel;
        ValidateFormatLabel(volumeLabel, nameof(volumeLabel));
        ValidateFormatLabel(chapterLabel, nameof(chapterLabel));
        ValidateFormatLabel(issueLabel, nameof(issueLabel));
        ValidateFormatLabel(bookLabel, nameof(bookLabel));

        // Handle epub format with special logic
        if (format == MangaFormat.Epub)
        {
            return FormatEpubReadingListTitle(chapterNumber, volumeNumber, chapterTitleName, volumeLabel);
        }

        // Try volume-only title first (when chapter is default but volume is real)
        if (Parser.IsDefaultChapter(chapterNumber) && !Parser.IsLooseLeafVolume(volumeNumber))
        {
            return string.Format(volumeLabel, volumeNumber);
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
        return libraryType switch
        {
            LibraryType.Comic or LibraryType.ComicVine =>
                string.Format(issueLabel, DefaultHashMark, displayChapterNumber),
            LibraryType.Book or LibraryType.LightNovel =>
                string.Format(bookLabel, displayChapterNumber),
            _ => string.Format(chapterLabel, displayChapterNumber)
        };
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
            return string.Format(volumeLabel, cleanedVolume);
        }

        // Special volume marker - just use cleaned chapter
        if (volumeNumber == Parser.SpecialVolume)
        {
            return cleanedChapterNumber;
        }

        // Regular epub with chapter number
        return string.Format(volumeLabel, cleanedChapterNumber);
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

        return string.Format(volumeLabel, volumeName).Trim();
    }

    /// <summary>
    /// Checks if the volume name already starts with a volume-like prefix.
    /// </summary>
    private static bool HasVolumePrefix(string volumeName, string? volumeLabel = null)
    {
        if (string.IsNullOrEmpty(volumeName))
        {
            return false;
        }

        var commonPrefixes = new[] { "Volume", "Vol.", "Vol ", "V." };
        foreach (var prefix in commonPrefixes)
        {
            if (volumeName.StartsWith(prefix, StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }
        }

        if (string.IsNullOrEmpty(volumeLabel)) return false;

        const int placeholderLength = 3; // "{0}".Length
        var placeholderIndex = volumeLabel.IndexOf("{0}", StringComparison.Ordinal);
        if (placeholderIndex < 0) return false;

        var labelPrefix = volumeLabel[..placeholderIndex].Trim();
        var labelSuffix = volumeLabel[(placeholderIndex + placeholderLength)..].Trim();

        if (!string.IsNullOrEmpty(labelPrefix) && volumeName.StartsWith(labelPrefix, StringComparison.OrdinalIgnoreCase))
        {
            return true;
        }

        if (!string.IsNullOrEmpty(labelSuffix) && volumeName.EndsWith(labelSuffix, StringComparison.OrdinalIgnoreCase))
        {
            return true;
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
