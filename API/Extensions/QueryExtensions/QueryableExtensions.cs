using System;
using System.Collections.Generic;
using System.Linq;
using System.Linq.Expressions;
using System.Threading.Tasks;
using API.Data.Misc;
using API.Data.Repositories;
using API.DTOs.Filtering;
using API.Entities;
using API.Entities.Enums;
using API.Entities.Scrobble;
using Microsoft.EntityFrameworkCore;

namespace API.Extensions.QueryExtensions;

public static class QueryableExtensions
{
    public static Task<AgeRestriction> GetUserAgeRestriction(this DbSet<AppUser> queryable, int userId)
    {
        if (userId < 1)
        {
            return Task.FromResult(new AgeRestriction()
            {
                AgeRating = AgeRating.NotApplicable,
                IncludeUnknowns = true
            });
        }
        return queryable
            .AsNoTracking()
            .Where(u => u.Id == userId)
            .Select(u =>
                new AgeRestriction(){
                    AgeRating = u.AgeRestriction,
                    IncludeUnknowns = u.AgeRestrictionIncludeUnknowns
                })
            .SingleAsync();
    }


    /// <summary>
    /// Applies restriction based on if the Library has restrictions (like include in search)
    /// </summary>
    /// <param name="query"></param>
    /// <param name="context"></param>
    /// <returns></returns>
    public static IQueryable<Library> IsRestricted(this IQueryable<Library> query, QueryContext context)
    {
        if (context.HasFlag(QueryContext.None)) return query;

        if (context.HasFlag(QueryContext.Dashboard))
        {
            query = query.Where(l => l.IncludeInDashboard);
        }

        if (context.HasFlag(QueryContext.Recommended))
        {
            query = query.Where(l => l.IncludeInRecommended);
        }

        if (context.HasFlag(QueryContext.Search))
        {
            query = query.Where(l => l.IncludeInSearch);
        }

        return query;
    }

    /// <summary>
    /// Returns all libraries for a given user
    /// </summary>
    /// <param name="library"></param>
    /// <param name="userId"></param>
    /// <param name="queryContext"></param>
    /// <returns></returns>
    public static IQueryable<int> GetUserLibraries(this IQueryable<Library> library, int userId, QueryContext queryContext = QueryContext.None)
    {
        return library
            .Include(l => l.AppUsers)
            .Where(lib => lib.AppUsers.Any(user => user.Id == userId))
            .IsRestricted(queryContext)
            .AsNoTracking()
            .AsSplitQuery()
            .Select(lib => lib.Id);
    }

    /// <summary>
    /// Returns all libraries for a given user and library type
    /// </summary>
    /// <param name="library"></param>
    /// <param name="userId"></param>
    /// <param name="queryContext"></param>
    /// <returns></returns>
    public static IQueryable<int> GetUserLibrariesByType(this IQueryable<Library> library, int userId, LibraryType type, QueryContext queryContext = QueryContext.None)
    {
        return library
            .Include(l => l.AppUsers)
            .Where(lib => lib.AppUsers.Any(user => user.Id == userId))
            .Where(lib => lib.Type == type)
            .IsRestricted(queryContext)
            .AsNoTracking()
            .AsSplitQuery()
            .Select(lib => lib.Id);
    }

    public static IEnumerable<DateTime> Range(this DateTime startDate, int numberOfDays) =>
        Enumerable.Range(0, numberOfDays).Select(e => startDate.AddDays(e));

    public static IQueryable<T> WhereIf<T>(this IQueryable<T> queryable, bool condition,
        Expression<Func<T, bool>> predicate)
    {
        return condition ? queryable.Where(predicate) : queryable;
    }

    public static IQueryable<T> WhereLike<T>(this IQueryable<T> queryable, bool condition, Expression<Func<T, string>> propertySelector, string searchQuery)
        where T : class
    {
        if (!condition || string.IsNullOrEmpty(searchQuery)) return queryable;

        var method = typeof(DbFunctionsExtensions).GetMethod(nameof(DbFunctionsExtensions.Like), new[] { typeof(DbFunctions), typeof(string), typeof(string) });
        var dbFunctions = typeof(EF).GetMethod(nameof(EF.Functions))?.Invoke(null, null);
        var searchExpression = Expression.Constant($"%{searchQuery}%");
        var likeExpression = Expression.Call(method, Expression.Constant(dbFunctions), propertySelector.Body, searchExpression);
        var lambda = Expression.Lambda<Func<T, bool>>(likeExpression, propertySelector.Parameters[0]);

        return queryable.Where(lambda);
    }

    /// <summary>
    /// Performs a WhereLike that ORs multiple fields
    /// </summary>
    /// <param name="queryable"></param>
    /// <param name="propertySelectors"></param>
    /// <param name="searchQuery"></param>
    /// <typeparam name="T"></typeparam>
    /// <returns></returns>
    /// <exception cref="ArgumentNullException"></exception>
    public static IQueryable<T> WhereLike<T>(this IQueryable<T> queryable, bool condition, List<Expression<Func<T, string>>> propertySelectors, string searchQuery)
        where T : class
    {
        if (!condition || string.IsNullOrEmpty(searchQuery)) return queryable;

        var method = typeof(DbFunctionsExtensions).GetMethod(nameof(DbFunctionsExtensions.Like), new[] { typeof(DbFunctions), typeof(string), typeof(string) });
        var dbFunctions = typeof(EF).GetMethod(nameof(EF.Functions))?.Invoke(null, null);
        var searchExpression = Expression.Constant($"%{searchQuery}%");

        Expression orExpression = null;
        foreach (var propertySelector in propertySelectors)
        {
            var likeExpression = Expression.Call(method, Expression.Constant(dbFunctions), propertySelector.Body, searchExpression);
            var lambda = Expression.Lambda<Func<T, bool>>(likeExpression, propertySelector.Parameters[0]);
            orExpression = orExpression == null ? lambda.Body : Expression.OrElse(orExpression, lambda.Body);
        }

        if (orExpression == null)
        {
            throw new ArgumentNullException(nameof(orExpression));
        }

        var combinedLambda = Expression.Lambda<Func<T, bool>>(orExpression, propertySelectors[0].Parameters[0]);
        return queryable.Where(combinedLambda);
    }

    public static IQueryable<ScrobbleEvent> SortBy(this IQueryable<ScrobbleEvent> query, ScrobbleEventSortField sort, bool isDesc = false)
    {
        if (isDesc)
        {
            return sort switch
            {
                ScrobbleEventSortField.None => query,
                ScrobbleEventSortField.Created => query.OrderByDescending(s => s.Created),
                ScrobbleEventSortField.LastModified => query.OrderByDescending(s => s.LastModified),
                ScrobbleEventSortField.Type => query.OrderByDescending(s => s.ScrobbleEventType),
                ScrobbleEventSortField.Series => query.OrderByDescending(s => s.Series.NormalizedName),
                ScrobbleEventSortField.IsProcessed => query.OrderByDescending(s => s.IsProcessed),
                _ => query
            };
        }

        return sort switch
        {
            ScrobbleEventSortField.None => query,
            ScrobbleEventSortField.Created => query.OrderBy(s => s.Created),
            ScrobbleEventSortField.LastModified => query.OrderBy(s => s.LastModified),
            ScrobbleEventSortField.Type => query.OrderBy(s => s.ScrobbleEventType),
            ScrobbleEventSortField.Series => query.OrderBy(s => s.Series.NormalizedName),
            ScrobbleEventSortField.IsProcessed => query.OrderBy(s => s.IsProcessed),
            _ => query
        };
    }

    /// <summary>
    /// Performs either OrderBy or OrderByDescending on the given query based on the value of SortOptions.IsAscending.
    /// </summary>
    /// <param name="query"></param>
    /// <param name="keySelector"></param>
    /// <param name="sortOptions"></param>
    /// <returns></returns>
    public static IQueryable<T> DoOrderBy<T, TKey>(this IQueryable<T> query, Expression<Func<T, TKey>> keySelector, SortOptions sortOptions)
    {
        return sortOptions.IsAscending ? query.OrderBy(keySelector) : query.OrderByDescending(keySelector);
    }
}
