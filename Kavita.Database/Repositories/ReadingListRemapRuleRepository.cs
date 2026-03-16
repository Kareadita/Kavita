using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Kavita.API.Repositories;
using Kavita.Models.Entities;
using Kavita.Models.Entities.ReadingLists;
using Microsoft.EntityFrameworkCore;

namespace Kavita.Database.Repositories;

public class ReadingListRemapRuleRepository(DataContext context) : IReadingListRemapRuleRepository
{
    public async Task<IList<ReadingListRemapRule>> GetRulesForNamesAsync(IList<string> normalizedNames, int userId, CancellationToken ct = default)
    {
        return await context.ReadingListRemapRule
            .Where(r => normalizedNames.Contains(r.NormalizedCblSeriesName)
                        && (r.AppUserId == userId || r.AppUserId == null))
            .OrderByDescending(r => r.AppUserId.HasValue) // user-specific first
            .ToListAsync(ct);
    }

    public void Add(ReadingListRemapRule rule)
    {
        context.ReadingListRemapRule.Add(rule);
    }

    public void Remove(ReadingListRemapRule rule)
    {
        context.ReadingListRemapRule.Remove(rule);
    }
}
