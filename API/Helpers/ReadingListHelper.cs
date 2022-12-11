using System;
using System.Text.RegularExpressions;
using API.DTOs.ReadingLists;
using API.Entities;
using API.Entities.Enums;
using API.Services;

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
            title = ReaderService.FormatChapterName(item.LibraryType, true, true) + chapterNum;
        }
        return title;
    }

}
