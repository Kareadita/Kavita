﻿#nullable enable
using System.Collections.Generic;
using System.Linq;
using API.Comparators;
using API.Entities;
using API.Services.Tasks.Scanner.Parser;

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
        var volumes = (series.Volumes ?? new List<Volume>())
            .OrderBy(v => v.Number, ChapterSortComparer.Default)
            .ToList();
        var firstVolume = volumes.GetCoverImage(series.Format);
        if (firstVolume == null) return null;

        var chapters = firstVolume.Chapters
            .OrderBy(c => double.Parse(c.Number), ChapterSortComparerZeroFirst.Default)
            .ToList();

        if (chapters.Count > 1 && chapters.Any(c => c.IsSpecial))
        {
            return chapters.FirstOrDefault(c => !c.IsSpecial)?.CoverImage ?? chapters.First().CoverImage;
        }

        // just volumes
        if (volumes.All(v => $"{v.Number}" != Parser.DefaultVolume))
        {
            return firstVolume.CoverImage;
        }
        // If we have loose leaf chapters

        // if loose leaf chapters AND volumes, just return first volume
        if (volumes.Count >= 1 && $"{volumes.First().Number}" != Parser.DefaultVolume)
        {
            var looseLeafChapters = volumes.Where(v => $"{v.Number}" == Parser.DefaultVolume)
                .SelectMany(c => c.Chapters.Where(c => !c.IsSpecial))
                .OrderBy(c => double.Parse(c.Number), ChapterSortComparerZeroFirst.Default)
                .ToList();
            if ((1.0f * volumes.First().Number) > float.Parse(looseLeafChapters.First().Number))
            {
                return looseLeafChapters.First().CoverImage;
            }
            return firstVolume.CoverImage;
        }

        var firstLooseLeafChapter = volumes
            .Where(v => $"{v.Number}" == Parser.DefaultVolume)
            .SelectMany(v => v.Chapters)
            .OrderBy(c => double.Parse(c.Number), ChapterSortComparerZeroFirst.Default)
            .FirstOrDefault(c => !c.IsSpecial);

        return firstLooseLeafChapter?.CoverImage ?? firstVolume.CoverImage;
    }
}
