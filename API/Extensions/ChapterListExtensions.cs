using System.Collections.Generic;
using System.IO;
using System.Linq;
using API.Entities;
using API.Helpers;
using API.Helpers.Builders;
using API.Services.Tasks.Scanner.Parser;

namespace API.Extensions;
#nullable enable

public static class ChapterListExtensions
{
    /// <summary>
    /// Returns first chapter in the list with at least one file
    /// </summary>
    /// <param name="chapters"></param>
    /// <returns></returns>
    public static Chapter? GetFirstChapterWithFiles(this IEnumerable<Chapter> chapters)
    {
        return chapters.FirstOrDefault(c => c.Files.Any());
    }

    /// <summary>
    /// Gets a single chapter (or null if doesn't exist) where Range matches the info.Chapters property. If the info
    /// is <see cref="ParserInfo.IsSpecial"/> then, the filename is used to search against Range or if filename exists within Files of said Chapter.
    /// </summary>
    /// <remarks>This uses GetNumberTitle() to calculate the Range to compare against the info.Chapters</remarks>
    /// <param name="chapters"></param>
    /// <param name="info"></param>
    /// <returns></returns>
    public static Chapter? GetChapterByRange(this IEnumerable<Chapter> chapters, ParserInfo info)
    {
        var normalizedPath = Parser.NormalizePath(info.FullFilePath);
        var specialTreatment = info.IsSpecialInfo();
        // NOTE: This can fail to find the chapter when Range is "1.0" as the chapter will store it as "1" hence why we need to emulate a Chapter
        var fakeChapter = new ChapterBuilder(info.Chapters, info.Chapters).Build();
        fakeChapter.UpdateFrom(info);
        return specialTreatment
             ? chapters.FirstOrDefault(c => c.Range == Parser.RemoveExtensionIfSupported(info.Filename) || c.Files.Select(f => Parser.NormalizePath(f.FilePath)).Contains(normalizedPath))
             : chapters.FirstOrDefault(c => c.Range == fakeChapter.GetNumberTitle());
    }

    /// <summary>
    /// Returns the minimum Release Year from all Chapters that meets the year requirement (>= 1000)
    /// </summary>
    /// <param name="chapters"></param>
    /// <returns></returns>
    public static int MinimumReleaseYear(this IList<Chapter> chapters)
    {
        return chapters.Select(v => v.ReleaseDate.Year).Where(NumberHelper.IsValidYear).DefaultIfEmpty().Min();
    }
}
