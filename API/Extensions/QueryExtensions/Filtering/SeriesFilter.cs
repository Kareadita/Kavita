using System;
using System.Linq;
using System.Linq.Expressions;

namespace API.Extensions.QueryExtensions.Filtering;

public enum FilterComparison
{
    Equal = 0,
    GreaterThan =1,
    GreaterThanEqual = 2,
    LessThan = 3,
    LessThanEqual = 4,

}

public static class SeriesFilter
{
    /// <summary>
    ///.WhereIf(hasReleaseYearMinFilter, s => s.Metadata.ReleaseYear >= filter.ReleaseYearRange!.Min)
    /// </summary>
    public static IQueryable<T> ReleaseYearFilter<T>(this IQueryable<T> queryable, bool condition,
        Expression<Func<T, int>> propertySelector, FilterComparison comparison, int? value)
    {
        if (!condition) return queryable;
        if (value == null) throw new ArgumentNullException(nameof(value));

        if (propertySelector == null)
        {
            throw new ArgumentNullException(nameof(propertySelector));
        }

        var propertyExpression = propertySelector.Body as MemberExpression;
        if (propertyExpression == null)
        {
            throw new ArgumentException("Invalid property selector", nameof(propertySelector));
        }

        var parameter = Expression.Parameter(typeof(T), "x");
        var property = Expression.Property(parameter, propertyExpression.Member.Name);

        Expression comparisonExpression = comparison switch
        {
            FilterComparison.Equal => Expression.Equal(property, Expression.Constant(value)),
            FilterComparison.GreaterThan => Expression.GreaterThan(property, Expression.Constant(value)),
            FilterComparison.GreaterThanEqual => Expression.GreaterThanOrEqual(property, Expression.Constant(value)),
            FilterComparison.LessThan => Expression.LessThan(property, Expression.Constant(value)),
            FilterComparison.LessThanEqual => Expression.LessThanOrEqual(property, Expression.Constant(value)),
            _ => throw new ArgumentOutOfRangeException(nameof(comparison))
        };

        var lambda = Expression.Lambda<Func<T, bool>>(comparisonExpression, parameter);
        return queryable.Where(lambda);
    }
}
