using System.Collections.Generic;
using System.Linq;
using API.Entities;

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
    }
}
