using System;
using System.Text.RegularExpressions;
using API.DTOs.ReadingLists;
using API.Entities;
using API.Entities.Enums;

namespace API.Helpers;

public static class ReadingListHelper
{
    private static readonly Regex JustNumbers = new Regex(@"^\d+$", RegexOptions.Compiled | RegexOptions.IgnoreCase,
        Services.Tasks.Scanner.Parser.Parser.RegexTimeout);
    public static string FormatTitle(ReadingListItemDto item)
    {
        var title = string.Empty;
        if (item.ChapterNumber == Services.Tasks.Scanner.Parser.Parser.DefaultChapter) {
            title = $"Volume {item.VolumeNumber}";
        }

        if (item.SeriesFormat == MangaFormat.Epub) {
            var specialTitle = Services.Tasks.Scanner.Parser.Parser.CleanSpecialTitle(item.ChapterNumber);
            if (specialTitle == Services.Tasks.Scanner.Parser.Parser.DefaultChapter)
            {
                if (!string.IsNullOrEmpty(item.ChapterTitleName))
                {
                    title = item.ChapterTitleName;
                }
                else
                {
                    title = $"Volume {Services.Tasks.Scanner.Parser.Parser.CleanSpecialTitle(item.VolumeNumber)}";
                }
            } else {
                title = $"Volume {specialTitle}";
            }
        }

        var chapterNum = item.ChapterNumber;
        if (!string.IsNullOrEmpty(chapterNum) && !JustNumbers.Match(item.ChapterNumber).Success) {
            chapterNum = Services.Tasks.Scanner.Parser.Parser.CleanSpecialTitle(item.ChapterNumber);
        }

        if (title == string.Empty) {
            title = FormatChapterName(item.LibraryType, true, true) + chapterNum;
        }
        return title;
    }

    /// <summary>
    /// Formats a Chapter name based on the library it's in
    /// </summary>
    /// <param name="libraryType"></param>
    /// <param name="includeHash">For comics only, includes a # which is used for numbering on cards</param>
    /// <param name="includeSpace">Add a space at the end of the string. if includeHash and includeSpace are true, only hash will be at the end.</param>
    /// <returns></returns>
    private static string FormatChapterName(LibraryType libraryType, bool includeHash = false,
        bool includeSpace = false)
    {
        switch (libraryType)
        {
            case LibraryType.Manga:
                return "Chapter" + (includeSpace ? " " : string.Empty);
            case LibraryType.Comic:
                if (includeHash) {
                    return "Issue #";
                }
                return "Issue" + (includeSpace ? " " : string.Empty);
            case LibraryType.Book:
                return "Book" + (includeSpace ? " " : string.Empty);
            default:
                throw new ArgumentOutOfRangeException(nameof(libraryType), libraryType, null);
        }
    }

}
