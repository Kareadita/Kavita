using System.Collections.Generic;
using System.Linq;
using API.Entities;
using API.Entities.Enums;
using API.Extensions;
using API.Services.Tasks.Scanner;

namespace API.Helpers;
#nullable enable

public static class SeriesHelper
{
    /// <summary>
    /// Given a parsedSeries checks if any of the names match against said Series and the format matches
    /// </summary>
    /// <param name="series"></param>
    /// <param name="parsedInfoKey"></param>
    /// <returns></returns>
    public static bool FindSeries(Series series, ParsedSeries parsedInfoKey)
    {
        return (series.NormalizedName.Equals(parsedInfoKey.NormalizedName)
                || (series.LocalizedName != null && series.LocalizedName.ToNormalized().Equals(parsedInfoKey.NormalizedName))
                || (series.OriginalName != null && series.OriginalName.ToNormalized().Equals(parsedInfoKey.NormalizedName))
                )
               && (series.Format == parsedInfoKey.Format || series.Format == MangaFormat.Unknown);
    }

    /// <summary>
    /// Removes all instances of missingSeries' Series from existingSeries Collection. Existing series is updated by
    /// reference and the removed element count is returned.
    /// </summary>
    /// <param name="existingSeries">Existing Series in DB</param>
    /// <param name="missingSeries">Series not found on disk or can't be parsed</param>
    /// <param name="removeCount"></param>
    /// <returns>the updated existingSeries</returns>
    public static IEnumerable<Series> RemoveMissingSeries(IList<Series> existingSeries, IEnumerable<Series> missingSeries, out int removeCount)
    {
        var existingCount = existingSeries.Count;
        var missingList = missingSeries.ToList();

        existingSeries = existingSeries.Where(
            s => !missingList.Exists(
                m => m.NormalizedName.Equals(s.NormalizedName) && m.Format == s.Format)).ToList();

        removeCount = existingCount -  existingSeries.Count;

        return existingSeries;
    }
}
