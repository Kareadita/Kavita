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
            var specialTreatment = (chapter.IsSpecial || (chapter.Number == "0" && !int.TryParse(chapter.Range, out _)));
            
            return specialTreatment ? infos.Any(v => v.Filename == chapter.Range) 
                                    : infos.Any(v => v.Chapters == chapter.Range);
        }
    }
}