using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using API.Comparators;
using API.Entities;
using API.Services.Tasks.Scanner.Parser;

namespace API.Extensions;
#nullable enable

public static class SeriesExtensions
{
    /// <summary>
    /// Calculates the Cover Image for the Series
    /// </summary>
    /// <param name="series"></param>
    /// <returns></returns>
    /// <remarks>This is under the assumption that the Volume already has a Cover Image calculated and set</remarks>
    public static string? GetCoverImage(this Series series)
    {
        var volumes = (series.Volumes ?? [])
            .OrderBy(v => v.MinNumber, ChapterSortComparerDefaultLast.Default)
            .ToList();
        var firstVolume = volumes.GetCoverImage(series.Format);
        if (firstVolume == null) return null;

        var chapters = firstVolume.Chapters
            .OrderBy(c => c.SortOrder, ChapterSortComparerDefaultLast.Default)
            .ToList();

        if (chapters.Count > 1 && chapters.Exists(c => c.IsSpecial))
        {
            return chapters.Find(c => !c.IsSpecial)?.CoverImage ?? chapters[0].CoverImage;
        }

        // just volumes
        if (volumes.TrueForAll(v => $"{v.MinNumber}" != Parser.LooseLeafVolume))
        {
            return firstVolume.CoverImage;
        }
        // If we have loose leaf chapters

        // if loose leaf chapters AND volumes, just return first volume
        if (volumes.Count >= 1 && volumes[0].MinNumber.IsNot(Parser.LooseLeafVolumeNumber))
        {
            var looseLeafChapters = volumes.Where(v => v.MinNumber.Is(Parser.LooseLeafVolumeNumber))
                .SelectMany(c => c.Chapters.Where(c2 => !c2.IsSpecial))
                .OrderBy(c => c.MinNumber, ChapterSortComparerDefaultFirst.Default)
                .ToList();

            if (looseLeafChapters.Count > 0 && volumes[0].MinNumber > looseLeafChapters[0].MinNumber)
            {
                return looseLeafChapters[0].CoverImage;
            }
            return firstVolume.CoverImage;
        }

        var chpts = volumes
            .First(v => v.MinNumber.Is(Parser.LooseLeafVolumeNumber))
            .Chapters
            //.Where(v => v.MinNumber.Is(Parser.LooseLeafVolumeNumber))
            //.SelectMany(v => v.Chapters)

            .Where(c => !c.IsSpecial)
            .OrderBy(c => c.MinNumber, ChapterSortComparerDefaultLast.Default)
            .ToList();

        var exactlyChapter1 = chpts.FirstOrDefault(c => c.MinNumber.Is(1f));
        if (exactlyChapter1 != null)
        {
            return exactlyChapter1.CoverImage;
        }

        return chpts.FirstOrDefault()?.CoverImage ?? firstVolume.CoverImage;
    }
}
