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

        // If first volume here is specials, move to the next as specials should almost always be last.
        if (firstVolume.MinNumber.Is(Parser.SpecialVolumeNumber) && volumes.Count > 1)
        {
            firstVolume = volumes[1];
        }

        // If the first volume is 0, then use Volume 1
        if (firstVolume.MinNumber.Is(0f) && volumes.Count > 1)
        {
            firstVolume = volumes[1];
        }

        var chapters = firstVolume.Chapters
            .OrderBy(c => c.SortOrder)
            .ToList();

        if (chapters.Count > 1 && chapters.Exists(c => c.IsSpecial))
        {
            return chapters.Find(c => !c.IsSpecial)?.CoverImage ?? chapters[0].CoverImage;
        }

        // just volumes
        if (volumes.TrueForAll(v => v.MinNumber.IsNot(Parser.LooseLeafVolumeNumber)))
        {
            return firstVolume.CoverImage;
        }
        // If we have loose leaf chapters

        // if loose leaf chapters AND volumes, just return first volume
        if (volumes.Count >= 1 && volumes[0].MinNumber.IsNot(Parser.LooseLeafVolumeNumber))
        {
            var looseLeafChapters = volumes.Where(v => v.MinNumber.Is(Parser.LooseLeafVolumeNumber))
                .SelectMany(c => c.Chapters.Where(c2 => !c2.IsSpecial))
                .OrderBy(c => c.SortOrder)
                .ToList();

            if (looseLeafChapters.Count > 0 && volumes[0].MinNumber > looseLeafChapters[0].MinNumber)
            {
                var first = looseLeafChapters.Find(c => c.SortOrder.Is(1f));
                if (first != null) return first.CoverImage;
                return looseLeafChapters[0].CoverImage;
            }
            return firstVolume.CoverImage;
        }

        var chpts = volumes
            .First(v => v.MinNumber.Is(Parser.LooseLeafVolumeNumber))
            .Chapters
            .Where(c => !c.IsSpecial)
            .OrderBy(c => c.MinNumber, ChapterSortComparerDefaultLast.Default)
            .ToList();

        var exactlyChapter1 = chpts.Find(c => c.MinNumber.Is(1f));
        if (exactlyChapter1 != null)
        {
            return exactlyChapter1.CoverImage;
        }

        return chpts.FirstOrDefault()?.CoverImage ?? firstVolume.CoverImage;
    }
}
