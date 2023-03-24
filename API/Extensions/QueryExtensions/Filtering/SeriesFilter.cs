using System;
using System.Collections;
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



        //var parameter = Expression.Parameter(typeof(T), "x");
        //var property = Expression.Property(parameter, propertyExpression.Member.Name);

        // var parameter = propertySelector.Parameters[0];
        // // This is wrong, it's not the end type, it's the end code that we actually need
        // var nestedPropertyNames = typeof(TPV).FullName.Split('.');
        //
        // MemberExpression? property = null;
        // foreach (var propertyName in nestedPropertyNames)
        // {
        //     if (property == null)
        //     {
        //         property = Expression.Property(parameter, propertyName);
        //         continue;
        //     }
        //     property = Expression.Property(property, propertyName);
        // }
        var parameter = propertySelector.Parameters[0];
        var property = GetNestedProperty(parameter, propertyExpression);

        var comparisonExpression = GetExpression<T, TPV, TV>(comparison, property, value);

        var lambda = Expression.Lambda<Func<T, bool>>(comparisonExpression, parameter);
        return queryable.Where(lambda);
    }
    private static Expression GetNestedProperty(ParameterExpression parameter, MemberExpression propertyExpression)
    {
        if (propertyExpression.Expression != parameter)
        {
            var nestedProperty = GetNestedProperty(parameter, propertyExpression.Expression as MemberExpression);
            return Expression.Property(nestedProperty, propertyExpression.Member.Name);
        }

        return propertyExpression;
    }
    private static Expression GetExpression<T, TPV, TV>(FilterComparison comparison, Expression property, TV? value)
    {
        if (value == null) throw new NullReferenceException("value cannot be null");
        switch (comparison)
        {
            case FilterComparison.Equal:
                return Expression.Equal(property, Expression.Constant(value));
            case FilterComparison.GreaterThan:
                return Expression.GreaterThan(property, Expression.Constant(value));
            case FilterComparison.GreaterThanEqual:
                return Expression.GreaterThanOrEqual(property, Expression.Constant(value));
            case FilterComparison.LessThan:
                return Expression.LessThan(property, Expression.Constant(value));
            case FilterComparison.LessThanEqual:
                return Expression.LessThanOrEqual(property, Expression.Constant(value));
            case FilterComparison.Contains:
                if (!typeof(TV).FullName.Contains("IList"))
                {
                    throw new ArgumentException("Contains filter can only be applied to properties of type IList", nameof(property));
                }
                return Expression.Call(
                    Expression.Constant(value),
                    typeof(IList).GetMethod("Contains")!,
                    property);
            default:
                throw new ArgumentOutOfRangeException(nameof(comparison), comparison, null);
        }
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
