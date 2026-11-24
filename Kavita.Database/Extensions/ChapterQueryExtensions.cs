using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Kavita.Common.Constants;
using Kavita.Models.Constants;
using Kavita.Models.Entities;
using Microsoft.EntityFrameworkCore;

namespace Kavita.Database.Extensions;

public static class ChapterQueryExtensions
{
    public static IOrderedQueryable<Chapter> ApplyDefaultChapterOrdering(this IQueryable<Chapter> query)
    {
        return query
            .Include(c => c.Volume)
            .OrderBy(c =>
                // Priority 1: Regular volumes (not loose-leaf, not special)
                c.Volume.MinNumber == ParserConstants.LooseLeafVolumeNumber ||
                c.Volume.MinNumber == ParserConstants.SpecialVolumeNumber ? 1 : 0)
            .ThenBy(c =>
                // Priority 2: Loose leaf over specials
                c.Volume.MinNumber == ParserConstants.SpecialVolumeNumber ? 1 : 0)
            // Priority 3: Non-special chapters
            .ThenBy(c => c.IsSpecial ? 1 : 0)
            .ThenBy(c => c.Volume.MinNumber)
            .ThenBy(c => c.SortOrder);
    }

    /// <summary>
    /// Returns the positions in a series for the given chapters. Same ordering as <see cref="ApplyDefaultChapterOrdering"/>
    /// </summary>
    /// <remarks>Positions are 1 based!</remarks>
    public static async Task<Dictionary<int, int>> GetSeriesPositionsAsync(this IQueryable<Chapter> query,
        ICollection<int> chapterIds, CancellationToken ct = default)
    {
        const int looseLeaf = ParserConstants.LooseLeafVolumeNumber;
        const int special = ParserConstants.SpecialVolumeNumber;

        var rows = await query
            .Where(c => chapterIds.Contains(c.Id))
            .Select(c => new
            {
                c.Id,
                Position = query.Count(c2 =>
                    c2.Volume.SeriesId == c.Volume.SeriesId &&
                    (
                        // Priority 1: Regular volumes (not loose-leaf, not special)
                        ((c2.Volume.MinNumber == looseLeaf || c2.Volume.MinNumber == special) ? 1 : 0)
                        < ((c.Volume.MinNumber == looseLeaf || c.Volume.MinNumber == special) ? 1 : 0)
                        ||
                        (((c2.Volume.MinNumber == looseLeaf || c2.Volume.MinNumber == special) ? 1 : 0)
                            == ((c.Volume.MinNumber == looseLeaf || c.Volume.MinNumber == special) ? 1 : 0)
                            // Priority 2: Loose leaf over specials
                            && ((c2.Volume.MinNumber == special ? 1 : 0) < (c.Volume.MinNumber == special ? 1 : 0)
                                ||
                                ((c2.Volume.MinNumber == special ? 1 : 0) == (c.Volume.MinNumber == special ? 1 : 0)
                                    // Priority 3: Non-special chapters
                                    && ((c2.IsSpecial ? 1 : 0) < (c.IsSpecial ? 1 : 0)
                                        ||
                                        ((c2.IsSpecial ? 1 : 0) == (c.IsSpecial ? 1 : 0)
                                            && (c2.Volume.MinNumber < c.Volume.MinNumber
                                                || (c2.Volume.MinNumber == c.Volume.MinNumber
                                                    && (c2.SortOrder < c.SortOrder
                                                        // deterministic tiebreaker so ranks are total
                                                        || (c2.SortOrder == c.SortOrder && c2.Id < c.Id)))))))))
                    )) + 1
            })
            .ToListAsync(ct);

        return rows.ToDictionary(r => r.Id, r => r.Position);
    }
}
