using System;
using System.Collections.Generic;
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
    Contains = 5,

}

public static class SeriesFilter
{

    #nullable enable
    public static IQueryable<T> PropertyFilter<T, TPV, TV>(this IQueryable<T> queryable, bool condition,
        Expression<Func<T, TPV>> propertySelector, FilterComparison comparison, TV? value)
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

        var comparisonExpression = GetExpression<T, TV>(comparison, property, value);

        var lambda = Expression.Lambda<Func<T, bool>>(comparisonExpression, parameter);
        return queryable.Where(lambda);
    }
    private static Expression GetExpression<T, TV>(FilterComparison comparison, Expression property, TV? value)
    {
        if (value == null) throw new NullReferenceException("value cannot be null");
        return comparison switch
        {
            FilterComparison.Equal => Expression.Equal(property, Expression.Constant(value)),
            FilterComparison.GreaterThan => Expression.GreaterThan(property, Expression.Constant(value)),
            FilterComparison.GreaterThanEqual => Expression.GreaterThanOrEqual(property, Expression.Constant(value)),
            FilterComparison.LessThan => Expression.LessThan(property, Expression.Constant(value)),
            FilterComparison.LessThanEqual => Expression.LessThanOrEqual(property, Expression.Constant(value)),
            FilterComparison.Contains => (typeof(T) != typeof(List<T>)) ? Expression.Empty() :
                Expression.Call(Expression.Constant(value), typeof(List<T>).GetMethod("Contains")!, property),
            _ => throw new ArgumentOutOfRangeException(nameof(comparison), comparison, null)
        };
    }

    #nullable disable
    //
    // public static IQueryable<T> LanguageFilter<T>(this IQueryable<T> queryable, bool condition,
    //     Expression<Func<T, int>> propertySelector, FilterComparison comparison, string value)
    // {
    //     if (!condition) return queryable;
    //     if (value == null) throw new ArgumentNullException(nameof(value));
    //
    //     if (propertySelector == null)
    //     {
    //         throw new ArgumentNullException(nameof(propertySelector));
    //     }
    //
    //     var propertyExpression = propertySelector.Body as MemberExpression;
    //     if (propertyExpression == null)
    //     {
    //         throw new ArgumentException("Invalid property selector", nameof(propertySelector));
    //     }
    //
    //     var parameter = Expression.Parameter(typeof(T), "x");
    //     var property = Expression.Property(parameter, propertyExpression.Member.Name);
    //
    //     Expression comparisonExpression = GetExpression<string>(comparison, property, value!);
    //
    //     var lambda = Expression.Lambda<Func<T, bool>>(comparisonExpression, parameter);
    //     return queryable.Where(lambda);
    // }
    //
    // /// <summary>
    // ///
    // /// </summary>
    // public static IQueryable<T> ReleaseYearFilter<T>(this IQueryable<T> queryable, bool condition,
    //     Expression<Func<T, int>> propertySelector, FilterComparison comparison, int? value)
    // {
    //     if (!condition) return queryable;
    //     if (value == null) throw new ArgumentNullException(nameof(value));
    //
    //     if (propertySelector == null)
    //     {
    //         throw new ArgumentNullException(nameof(propertySelector));
    //     }
    //
    //     var propertyExpression = propertySelector.Body as MemberExpression;
    //     if (propertyExpression == null)
    //     {
    //         throw new ArgumentException("Invalid property selector", nameof(propertySelector));
    //     }
    //
    //     var parameter = Expression.Parameter(typeof(T), "x");
    //     var property = Expression.Property(parameter, propertyExpression.Member.Name);
    //
    //     Expression comparisonExpression = GetExpression<int>(comparison, property, value);
    //
    //     var lambda = Expression.Lambda<Func<T, bool>>(comparisonExpression, parameter);
    //     return queryable.Where(lambda);
    // }


}
