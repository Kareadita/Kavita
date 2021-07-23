using System.Collections.Generic;
using System.Linq;
using API.Entities;
using API.Parser;
using API.Services;
using API.Services.Tasks.Scanner;

namespace API.Extensions
{
    public static class SeriesExtensions
    {
        /// <summary>
        /// Checks against all the name variables of the Series if it matches anything in the list.
        /// </summary>
        /// <param name="series"></param>
        /// <param name="list"></param>
        /// <returns></returns>
        public static bool NameInList(this Series series, IEnumerable<string> list)
        {
            return list.Any(name => Parser.Parser.Normalize(name) == series.NormalizedName || Parser.Parser.Normalize(name) == Parser.Parser.Normalize(series.Name)
                || name == series.Name || name == series.LocalizedName || name == series.OriginalName  || Parser.Parser.Normalize(name) == Parser.Parser.Normalize(series.OriginalName));
        }

        /// <summary>
        /// Checks against all the name variables of the Series if it matches anything in the list. Includes a check against the Format of the Series
        /// </summary>
        /// <param name="series"></param>
        /// <param name="list"></param>
        /// <returns></returns>
        public static bool NameInList(this Series series, IEnumerable<ParsedSeries> list)
        {
            return list.Any(name => Parser.Parser.Normalize(name.Name) == series.NormalizedName || Parser.Parser.Normalize(name.Name) == Parser.Parser.Normalize(series.Name)
                || name.Name == series.Name || name.Name == series.LocalizedName || name.Name == series.OriginalName  || Parser.Parser.Normalize(name.Name) == Parser.Parser.Normalize(series.OriginalName) && series.Format == name.Format);
        }

        /// <summary>
        /// Checks against all the name variables of the Series if it matches the <see cref="ParserInfo"/>
        /// </summary>
        /// <param name="series"></param>
        /// <param name="info"></param>
        /// <returns></returns>
        public static bool NameInParserInfo(this Series series, ParserInfo info)
        {
            if (info == null) return false;
            return Parser.Parser.Normalize(info.Series) == series.NormalizedName || Parser.Parser.Normalize(info.Series) == Parser.Parser.Normalize(series.Name)
                || info.Series == series.Name || info.Series == series.LocalizedName || info.Series == series.OriginalName  || Parser.Parser.Normalize(info.Series) == Parser.Parser.Normalize(series.OriginalName);
        }
    }
}
