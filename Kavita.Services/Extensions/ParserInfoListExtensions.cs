using System.Collections.Generic;
using System.Linq;
using Kavita.Models.Entities;
using Kavita.Models.Parser;
using Kavita.Services.Scanner;

namespace Kavita.Services.Extensions;

public static class ParserInfoListExtensions
{
    /// <summary>
    /// Selects distinct volume numbers by the "Volumes" key on the ParserInfo
    /// </summary>
    /// <param name="infos"></param>
    /// <returns></returns>
    public static IList<string> DistinctVolumes(this IList<ParserInfo> infos)
    {
        return infos
            .Select(p => p.Volumes)
            .Distinct()
            .GroupBy(v => (Min: Parser.MinNumberFromRange(v), Max: Parser.MaxNumberFromRange(v)))
            // shortest tends to be the "cleanest" form (1 over 01)
            .Select(g => g.OrderBy(v => v.Length).First())
            .ToList();
    }

    /// <summary>
    /// Checks if a list of ParserInfos has a given chapter or not. Lookup occurs on Range property. If a chapter is
    /// special, then the <see cref="ParserInfo.Filename"/> is matched, else the <see cref="ParserInfo.Chapters"/> field is checked.
    /// </summary>
    /// <param name="infos"></param>
    /// <param name="chapter"></param>
    /// <returns></returns>
    public static bool HasInfo(this IList<ParserInfo> infos, Chapter chapter)
    {
        var chapterFiles = chapter.Files.Select(x => Scanner.Parser.NormalizePath(x.FilePath)).ToList();
        var infoFiles = infos.Select(x => Scanner.Parser.NormalizePath(x.FullFilePath)).ToList();
        return infoFiles.Intersect(chapterFiles).Any();
    }

}
