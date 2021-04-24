using System.Collections.Generic;
using System.Linq;
using API.Entities;
using API.Parser;

namespace API.Extensions
{
    public static class ParserInfoListExtensions
    {
        public static IList<string> DistinctVolumes(this ParserInfo[] infos)
        {
            return infos.Select(p => p.Volumes).Distinct().ToList();
        }

        public static bool HasInfo(this ParserInfo[] infos, Chapter chapter)
        {
            var range = chapter.Range;
            if (chapter.Range.Contains("-"))
            {
                range = Parser.Parser.MinimumNumberFromRange(chapter.Range) + string.Empty;
            }
            var specialTreatment = (chapter.IsSpecial || (chapter.Number == "0" && !float.TryParse(range, out _)));
            
            return specialTreatment ? infos.Any(v => v.Filename == chapter.Range) 
                                    : infos.Any(v => v.Chapters == chapter.Range);
        }
    }
}