using System;
using System.Collections;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using API.Entities;
using API.Entities.Enums;
using API.Services.Tasks.Scanner.Parser;

namespace API.Extensions;
#nullable enable

public static class VolumeListExtensions
{
    /// <summary>
    /// Selects the first Volume to get the cover image from. For a book with only a special, the special will be returned.
    /// If there are both specials and non-specials, then the first non-special will be returned.
    /// </summary>
    /// <param name="volumes"></param>
    /// <param name="seriesFormat"></param>
    /// <returns></returns>
    public static Volume? GetCoverImage(this IList<Volume> volumes, MangaFormat seriesFormat)
    {
        if (volumes == null) throw new ArgumentException("Volumes cannot be null");

        if (seriesFormat == MangaFormat.Epub || seriesFormat == MangaFormat.Pdf)
        {
            return volumes.MinBy(x => x.MinNumber);
        }

        if (volumes.HasAnyNonLooseLeafVolumes())
        {
            return volumes.FirstNonLooseLeafOrDefault();
        }

        // We only have 1 volume of chapters, we need to be cautious if there are specials, as we don't want to order them first
        return volumes.MinBy(x => x.MinNumber);
    }

    /// <summary>
    /// If the collection of volumes has any non-loose leaf volumes
    /// </summary>
    /// <param name="volumes"></param>
    /// <returns></returns>
    public static bool HasAnyNonLooseLeafVolumes(this IEnumerable<Volume> volumes)
    {
        return volumes.Any(x => Math.Abs(x.MinNumber - Parser.DefaultChapterNumber) > 0.001f);
    }

    /// <summary>
    /// Returns first non-loose leaf volume
    /// </summary>
    /// <param name="volumes"></param>
    /// <returns></returns>
    public static Volume? FirstNonLooseLeafOrDefault(this IEnumerable<Volume> volumes)
    {
        return volumes.OrderBy(x => x.MinNumber).FirstOrDefault(v => Math.Abs(v.MinNumber - Parser.DefaultChapterNumber) >= 0.001f);
    }

    /// <summary>
    /// Returns the first (and only) loose leaf volume or null if none
    /// </summary>
    /// <param name="volumes"></param>
    /// <returns></returns>
    public static Volume? GetLooseLeafVolumeOrDefault(this IEnumerable<Volume> volumes)
    {
        return volumes.FirstOrDefault(v => Math.Abs(v.MinNumber - Parser.DefaultChapterNumber) < 0.001f);
    }
}
