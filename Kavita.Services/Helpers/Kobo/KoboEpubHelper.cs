using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using Kavita.Models.DTOs;
using Kavita.Models.Entities.Enums;
using Kavita.Models.Kobo;
using Kavita.Services.Scanner;

namespace Kavita.Services.Helpers.Kobo;

public static class KoboEpubHelper
{
    private const string SectionPrefix = "section";
    public const int MaxSinglePageEntries = 30;

    /// <summary>
    /// Returns the 0-based Kavita page for a Kobo section path. A split half maps to its source page
    /// </summary>
    /// <remarks>Sections are 1-based indexed</remarks>
    /// <example>TryGetPage("EPUB/section13-2.html") -> true, 12</example>
    public static bool TryGetPage(string? path, out int page)
    {
        page = 0;
        if (string.IsNullOrEmpty(path)) return false;

        var name = Path.GetFileNameWithoutExtension(path).AsSpan();
        if (!name.StartsWith(SectionPrefix, StringComparison.Ordinal)) return false;

        var number = name[SectionPrefix.Length..];
        var dash = number.IndexOf('-');
        if (dash >= 0)
        {
            if (!TryParsePositive(number[(dash + 1)..], out _)) return false;
            number = number[..dash];
        }

        if (!TryParsePositive(number, out var section)) return false;

        page = section - 1;
        return true;
    }

    /// <summary>
    /// Returns the section path for a 0-based Kavita page. Part 2 and up are the halves of a split page
    /// </summary>
    /// <example>GetPath(12) -> "EPUB/section13.html", GetPath(12, 2) -> "EPUB/section13-2.html"</example>
    public static string GetPath(int page, int part = 1)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(page);
        ArgumentOutOfRangeException.ThrowIfLessThan(part, 1);

        var suffix = part == 1 ? string.Empty : $"-{part}";
        return $"EPUB/{SectionPrefix}{page + 1}{suffix}.html";
    }

    /// <summary>
    /// Splits a chapter's pages into table of contents groups, one per chapter number parsed from the chapter's file
    /// names, else one per page up to <see cref="MaxSinglePageEntries"/> pages, else runs of <paramref name="pagesPerGroup"/> pages
    /// </summary>
    /// <remarks>The Kobo lists the ToC, not the page-list, so a short chapter needs its pages in the ToC</remarks>
    /// <param name="files">The chapter's files, in any order</param>
    /// <param name="pageCount">Number of pages in the chapter's cache folder</param>
    /// <param name="libraryType">Picks the parser for the file names</param>
    /// <param name="pagesPerGroup">Size of each group when falling back to runs of pages</param>
    public static IList<EpubTocGroup> GroupPages(IEnumerable<MangaFileDto> files, int pageCount, LibraryType libraryType,
        int pagesPerGroup = 20)
    {
        if (pageCount <= 0) return [];

        // The cache extracts each file into a folder named by its id, so pages follow id order
        var orderedFiles = files.OrderBy(f => f.Id).ToList();
        if (orderedFiles.Count > 1 && orderedFiles.Sum(f => f.Pages) == pageCount)
        {
            var chapterGroups = GroupByChapter(orderedFiles, libraryType);
            if (chapterGroups.Count > 1) return chapterGroups;
        }

        return pageCount <= MaxSinglePageEntries ? GroupBySinglePage(pageCount) : GroupByRuns(pageCount, pagesPerGroup);
    }

    /// <summary>
    /// Returns empty when no file has a chapter number, or the numbers do not go up
    /// </summary>
    private static List<EpubTocGroup> GroupByChapter(List<MangaFileDto> files, LibraryType libraryType)
    {
        var numbers = GetChapterNumbers(files, libraryType);
        if (numbers.Count == 0) return [];

        var groups = new List<EpubTocGroup>();
        var groupStart = 0;
        var nextPage = 0;
        for (var i = 0; i < files.Count; i++)
        {
            nextPage += files[i].Pages;

            var isLastFileOfChapter = i == files.Count - 1 || numbers[i + 1] != numbers[i];
            if (!isLastFileOfChapter) continue;

            groups.Add(new EpubTocGroup(groupStart, nextPage - 1, EpubTocGroupKind.Chapter, numbers[i]));
            groupStart = nextPage;
        }

        var numbersGoUp = groups.Zip(groups.Skip(1))
            .All(pair => Parser.MinNumberFromRange(pair.Second.Number!) > Parser.MinNumberFromRange(pair.First.Number!));

        return numbersGoUp ? groups : [];
    }

    /// <summary>
    /// One chapter number per file. A file without one takes the number before it, or the first number when it leads.
    /// Returns empty when no file has a number
    /// </summary>
    private static List<string> GetChapterNumbers(List<MangaFileDto> files, LibraryType libraryType)
    {
        var parsed = files
            .Select(f => Parser.ParseChapter(Path.GetFileNameWithoutExtension(f.FilePath), libraryType))
            .ToList();

        var previous = parsed.FirstOrDefault(n => n != Parser.DefaultChapter);
        if (previous == null) return [];

        var numbers = new List<string>(parsed.Count);
        foreach (var number in parsed)
        {
            if (number != Parser.DefaultChapter)
            {
                previous = number;
            }

            numbers.Add(previous);
        }

        return numbers;
    }

    private static List<EpubTocGroup> GroupBySinglePage(int pageCount)
    {
        return Enumerable.Range(0, pageCount)
            .Select(page => new EpubTocGroup(page, page, EpubTocGroupKind.Page, null))
            .ToList();
    }

    private static List<EpubTocGroup> GroupByRuns(int pageCount, int pagesPerGroup)
    {
        var groups = new List<EpubTocGroup>();
        for (var firstPage = 0; firstPage < pageCount; firstPage += pagesPerGroup)
        {
            var lastPage = Math.Min(firstPage + pagesPerGroup, pageCount) - 1;
            groups.Add(new EpubTocGroup(firstPage, lastPage, EpubTocGroupKind.Pages, null));
        }

        return groups;
    }

    private static bool TryParsePositive(ReadOnlySpan<char> value, out int number)
    {
        return int.TryParse(value, NumberStyles.None, CultureInfo.InvariantCulture, out number) && number >= 1;
    }
}
