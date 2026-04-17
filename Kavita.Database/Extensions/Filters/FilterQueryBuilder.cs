using System;
using System.Collections.Generic;
using System.Linq;
using Kavita.Models.DTOs.Filtering.v2;

namespace Kavita.Database.Extensions.Filters;

/// <summary>
/// Shared filter query building logic. Handles the Intersect/Union/Aggregate pattern and limit
/// </summary>
public static class FilterQueryBuilder
{
    /// <summary>
    /// Builds a filtered query by applying each statement via the provided dispatch function,
    /// combining results with Intersect (And) or Union (Or).
    /// </summary>
    /// <typeparam name="TEntity">The EF entity type (Series, Person, AppUserAnnotation, etc.)</typeparam>
    /// <typeparam name="TStatement">The statement DTO type</typeparam>
    /// <param name="filter">The filter DTO containing statements and combination mode</param>
    /// <param name="query">The base IQueryable</param>
    /// <param name="buildGroup">
    /// Entity-specific dispatch: given a statement and the base query, returns a filtered query.
    /// This is where the field switch and value converter live.
    /// </param>
    /// <param name="preProcess">
    /// Optional hook called before statement processing. Used for one-off cases
    /// like injecting additional statements based on existing ones.
    /// </param>
    public static IQueryable<TEntity> Apply<TEntity, TStatement>(
        IFilterDto<TStatement> filter,
        IQueryable<TEntity> query,
        Func<TStatement, IQueryable<TEntity>, IQueryable<TEntity>> buildGroup,
        Action<IFilterDto<TStatement>>? preProcess = null)
    {
        if (filter.Statements.Count == 0) return query;

        preProcess?.Invoke(filter);

        var queries = filter.Statements
            .Select(statement => buildGroup(statement, query))
            .ToList();

        return filter.Combination == FilterCombination.And
            ? queries.Aggregate((q1, q2) => q1.Intersect(q2))
            : queries.Aggregate((q1, q2) => q1.Union(q2));
    }

    /// <summary>
    /// Applies a row limit to the query. Use after sorting to preserve sort order.
    /// </summary>
    public static IQueryable<T> ApplyLimit<T>(this IQueryable<T> query, int limit)
    {
        return limit <= 0 ? query : query.Take(limit);
    }
}
