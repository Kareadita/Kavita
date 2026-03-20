using System;
using Kavita.Models.Parser;

namespace Kavita.Services.Helpers;

/// <summary>
/// Helps with figuring out Count and TotalCount during <c>ProcessSeries.UpdateChapters</c>
/// </summary>
public static class ParsedCountHelper
{
    /// <summary>
    /// Uses both Volume and Number to make an educated guess as to what count refers to, and the highest number.
    /// </summary>
    /// <returns></returns>
    public static int GetCalculatedCount(ParserInfo info)
    {
        // Prioritize Chapter over Volume as that is more common (and closer to real spec)
        if (info.HighestChapter > 0) return (int) Math.Floor(info.HighestChapter);
        if (info.HighestVolume > 0) return (int) Math.Floor(info.HighestVolume);
        return 0;
    }

    /// <summary>
    /// Returns the total Count. Will use ComicInfo.Count if exists, otherwise will fallback to the highest Volume/Number
    /// </summary>
    /// <param name="info"></param>
    /// <returns></returns>
    public static int? GetTotalCount(ParserInfo info)
    {
        if (info.ComicInfo?.Count is > 0) return info.ComicInfo.Count;
        if (info.HasEndMarker) return (int) Math.Max(info.HighestVolume, info.HighestChapter);
        return null;
    }
}
