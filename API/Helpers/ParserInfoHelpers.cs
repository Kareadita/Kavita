using System.Collections.Generic;
using API.Entities;
using API.Entities.Enums;
using API.Extensions;
using API.Services.Tasks.Scanner;
using API.Services.Tasks.Scanner.Parser;

namespace API.Helpers;
#nullable enable

public static class ParserInfoHelpers
{
    /// <summary>
    /// Checks each parser info to see if there is a name match and if so, checks if the format matches the Series object.
    /// This accounts for if the Series has an Unknown type and if so, considers it matching.
    /// </summary>
    /// <param name="series"></param>
    /// <param name="parsedSeries"></param>
    /// <returns></returns>
    public static bool SeriesHasMatchingParserInfoFormat(Series series,
        Dictionary<ParsedSeries, IList<ParserInfo>> parsedSeries)
    {
        var format = MangaFormat.Unknown;
        foreach (var pSeries in parsedSeries.Keys)
        {
            var name = pSeries.Name;
            var normalizedName = name.ToNormalized();

            if (normalizedName == series.NormalizedName ||
                normalizedName == series.Name.ToNormalized() ||
                name == series.Name || name == series.LocalizedName ||
                name == series.OriginalName ||
                normalizedName == series.OriginalName?.ToNormalized())
            {
                format = pSeries.Format;
                if (format == series.Format)
                {
                    return true;
                }
            }
        }

        if (series.Format == MangaFormat.Unknown)
        {
            return true;
        }

        return format == series.Format;
    }
}
