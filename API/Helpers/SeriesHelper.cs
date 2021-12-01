using API.Entities;
using API.Entities.Enums;
using API.Services.Tasks.Scanner;

namespace API.Helpers;

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
        return (series.NormalizedName.Equals(parsedInfoKey.NormalizedName) || Parser.Parser.Normalize(series.OriginalName).Equals(parsedInfoKey.NormalizedName))
               && (series.Format == parsedInfoKey.Format || series.Format == MangaFormat.Unknown);
    }
}
