using System.Collections.Generic;
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
            foreach (var name in list)
            {
                if (Parser.Parser.Normalize(name) == series.NormalizedName || name == series.Name || name == series.LocalizedName || name == series.OriginalName)
                {
                    return true;
                }
            }

            return false;
        }
    }
}