using System;
using System.Collections.Generic;
using System.Linq;
using System.Linq.Expressions;
using System.Threading.Tasks;
using API.Entities.Progress;
using Microsoft.EntityFrameworkCore;

namespace API.Extensions.QueryExtensions;

public class IdCount
{
    public int Id { get; set; }
    public int Count { get; set; }
}

/// <summary>
/// Extensions primarily focusing on Profile and Server Stats pages/queries
/// </summary>
public static class StatisticsQueryExtensions
{
    public static async Task<List<IdCount>> GetTopCounts( this IQueryable<AppUserProgress> query, Expression<Func<AppUserProgress, int>> keySelector, int? take = null)
    {
        var result = query
            .GroupBy(keySelector)
            .Select(g => new IdCount {Id = g.Key, Count = g.Count()})
            .OrderByDescending(x => x.Count);

        if (take.HasValue)
        {
            return await result.Take(take.Value).ToListAsync();
        }

        return await result.ToListAsync();
    }
}
