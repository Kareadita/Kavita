using System.Linq;
using API.Entities;
using API.Services.Tasks.Scanner.Parser;

namespace API.Extensions.QueryExtensions;

public static class ChapterQueryExtensions
{
    public static IOrderedQueryable<Chapter> ApplyDefaultChapterOrdering(this IQueryable<Chapter> query)
    {
        return query
            .OrderBy(c =>
                // Priority 1: Regular volumes (not loose leaf, not special)
                c.Volume.Number == Parser.LooseLeafVolumeNumber ||
                c.Volume.Number == Parser.SpecialVolumeNumber ? 1 : 0)
            .ThenBy(c =>
                // Priority 2: Loose leaf over specials
                c.Volume.Number == Parser.SpecialVolumeNumber ? 1 : 0)
            // Priority 3: Non-special chapters
            .ThenBy(c => c.IsSpecial ? 1 : 0)
            .ThenBy(c => c.Volume.Number)
            .ThenBy(c => c.SortOrder);
    }
}
