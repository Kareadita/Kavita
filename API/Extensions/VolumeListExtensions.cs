using System;
using System.Collections.Generic;
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


        if (volumes.Any(x => x.MinNumber != 0f)) // TODO: Refactor this so we can avoid a magic number
        {
            return volumes.OrderBy(x => x.MinNumber).FirstOrDefault(x => x.MinNumber != 0);
        }

        // We only have 1 volume of chapters, we need to be cautious if there are specials, as we don't want to order them first
        return volumes.MinBy(x => x.MinNumber);
    }
}
