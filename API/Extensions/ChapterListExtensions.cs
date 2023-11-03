using System.Collections.Generic;
using System.Linq;
using API.Entities;
using API.Helpers;
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
    /// <param name="chapters"></param>
    /// <param name="info"></param>
    /// <returns></returns>
    public static Chapter? GetChapterByRange(this IEnumerable<Chapter> chapters, ParserInfo info)
    {
        var specialTreatment = info.IsSpecialInfo();
        return specialTreatment
            ? chapters.FirstOrDefault(c => c.Range == info.Filename || (c.Files.Select(f => f.FilePath).Contains(info.FullFilePath)))
            : chapters.FirstOrDefault(c => c.Range == info.Chapters);
    }

    /// <summary>
    /// Returns the minimum Release Year from all Chapters that meets the year requirement (>= 1000)
    /// </summary>
    /// <param name="chapters"></param>
    /// <returns></returns>
    public static int MinimumReleaseYear(this IList<Chapter> chapters)
    {
        return chapters.Select(v => v.ReleaseDate.Year).Where(y => NumberHelper.IsValidYear(y)).DefaultIfEmpty().Min();
    }
}
