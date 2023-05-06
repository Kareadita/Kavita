using System.Collections.Generic;
using System.Linq;
using API.Comparators;
using API.Entities;

namespace API.Extensions;

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
        var volumes = series.Volumes ?? new List<Volume>();
        var firstVolume = volumes.GetCoverImage(series.Format);
        if (firstVolume == null) return null;
        string? coverImage = null;

        var chapters = firstVolume.Chapters.OrderBy(c => double.Parse(c.Number), ChapterSortComparerZeroFirst.Default).ToList();
        if (chapters.Count > 1 && chapters.Any(c => c.IsSpecial))
        {
            coverImage = chapters.FirstOrDefault(c => !c.IsSpecial)?.CoverImage ?? chapters.First().CoverImage;
            firstVolume = null;
        }

        return firstVolume?.CoverImage ?? coverImage;
    }
}
