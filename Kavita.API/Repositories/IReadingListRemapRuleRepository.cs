using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Kavita.Models.Entities;
using Kavita.Models.Entities.ReadingLists;

namespace Kavita.API.Repositories;

public interface IReadingListRemapRuleRepository
{
    /// <summary>
    /// Returns all remap rules matching the given normalized CBL series names,
    /// ordered with user-specific rules before global rules.
    /// </summary>
    Task<IList<ReadingListRemapRule>> GetRulesForNamesAsync(IList<string> normalizedNames, int userId, CancellationToken ct = default);
    void Add(ReadingListRemapRule rule);
    void Remove(ReadingListRemapRule rule);
}
