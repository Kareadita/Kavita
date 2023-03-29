using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Linq.Expressions;
using API.DTOs.Filtering.v2;
using API.Entities;
using API.Entities.Enums;
using Kavita.Common;
using Microsoft.EntityFrameworkCore;

namespace API.Extensions.QueryExtensions.Filtering;

#nullable enable

public static class SeriesFilter
{

    public static IQueryable<Series> HasLanguage(this IQueryable<Series> queryable, bool condition,
        FilterComparison comparison, IList<string> languages)
    {
        if (languages.Count == 0 || !condition) return queryable;

        switch (comparison)
        {
            case FilterComparison.Equal:
                return queryable.Where(s => s.Metadata.Language.Equals(languages.First()));
            case FilterComparison.GreaterThan:
            case FilterComparison.GreaterThanEqual:
            case FilterComparison.LessThan:
            case FilterComparison.LessThanEqual:
            case FilterComparison.Matches:
                throw new KavitaException($"{comparison} not applicable for Series.Language");
            case FilterComparison.Contains:
                return queryable.Where(s => languages.Contains(s.Metadata.Language));
            default:
                throw new ArgumentOutOfRangeException(nameof(comparison), comparison, null);
        }
    }

    public static IQueryable<Series> HasReleaseYear(this IQueryable<Series> queryable, bool condition,
        FilterComparison comparison, int? releaseYear)
    {
        if (!condition || releaseYear == null) return queryable;

        switch (comparison)
        {
            case FilterComparison.Equal:
                return queryable.Where(s => s.Metadata.ReleaseYear == releaseYear);
            case FilterComparison.GreaterThan:
                return queryable.Where(s => s.Metadata.ReleaseYear > releaseYear);
            case FilterComparison.GreaterThanEqual:
                return queryable.Where(s => s.Metadata.ReleaseYear >= releaseYear);
            case FilterComparison.LessThan:
                return queryable.Where(s => s.Metadata.ReleaseYear < releaseYear);
            case FilterComparison.LessThanEqual:
                return queryable.Where(s => s.Metadata.ReleaseYear <= releaseYear);
            case FilterComparison.Matches:
            case FilterComparison.Contains:
                throw new KavitaException($"{comparison} not applicable for Series.ReleaseYear");
            default:
                throw new ArgumentOutOfRangeException(nameof(comparison), comparison, null);
        }
    }


    public static IQueryable<Series> HasRating(this IQueryable<Series> queryable, bool condition,
        FilterComparison comparison, int rating, int userId)
    {
        if (rating < 0 || !condition || userId <= 0) return queryable;

        switch (comparison)
        {
            case FilterComparison.Equal:
                return queryable.Where(s => s.Ratings.Any(r => r.Rating == rating && r.AppUserId == userId));
            case FilterComparison.GreaterThan:
                return queryable.Where(s => s.Ratings.Any(r => r.Rating > rating && r.AppUserId == userId));
            case FilterComparison.GreaterThanEqual:
                return queryable.Where(s => s.Ratings.Any(r => r.Rating >= rating && r.AppUserId == userId));
            case FilterComparison.LessThan:
                return queryable.Where(s => s.Ratings.Any(r => r.Rating < rating && r.AppUserId == userId));
            case FilterComparison.LessThanEqual:
                return queryable.Where(s => s.Ratings.Any(r => r.Rating <= rating && r.AppUserId == userId));
            case FilterComparison.Contains:
            case FilterComparison.Matches:
                throw new KavitaException($"{comparison} not applicable for Series.Rating");
            default:
                throw new ArgumentOutOfRangeException(nameof(comparison), comparison, null);
        }
    }

    public static IQueryable<Series> HasAgeRating(this IQueryable<Series> queryable, bool condition,
        FilterComparison comparison, IList<AgeRating> ratings)
    {
        if (!condition || ratings.Count == 0) return queryable;

        var firstRating = ratings.First();
        switch (comparison)
        {
            case FilterComparison.Equal:
                return queryable.Where(s => s.Metadata.AgeRating == firstRating);
            case FilterComparison.GreaterThan:
                return queryable.Where(s => s.Metadata.AgeRating > firstRating);
            case FilterComparison.GreaterThanEqual:
                return queryable.Where(s => s.Metadata.AgeRating >= firstRating);
            case FilterComparison.LessThan:
                return queryable.Where(s => s.Metadata.AgeRating < firstRating);
            case FilterComparison.LessThanEqual:
                return queryable.Where(s => s.Metadata.AgeRating <= firstRating);
            case FilterComparison.Contains:
                return queryable.Where(s => ratings.Contains(s.Metadata.AgeRating));
            case FilterComparison.Matches:
                throw new KavitaException($"{comparison} not applicable for Series.AgeRating");
            default:
                throw new ArgumentOutOfRangeException(nameof(comparison), comparison, null);
        }
    }
    public static IQueryable<Series> HasAverageReadTime(this IQueryable<Series> queryable, bool condition,
        FilterComparison comparison, int avgReadTime)
    {
        if (!condition || avgReadTime < 0) return queryable;

        switch (comparison)
        {
            case FilterComparison.Equal:
                return queryable.Where(s => s.AvgHoursToRead == avgReadTime);
            case FilterComparison.GreaterThan:
                return queryable.Where(s => s.AvgHoursToRead > avgReadTime);
            case FilterComparison.GreaterThanEqual:
                return queryable.Where(s => s.AvgHoursToRead >= avgReadTime);
            case FilterComparison.LessThan:
                return queryable.Where(s => s.AvgHoursToRead < avgReadTime);
            case FilterComparison.LessThanEqual:
                return queryable.Where(s => s.AvgHoursToRead <= avgReadTime);
            case FilterComparison.Contains:
            case FilterComparison.Matches:
                throw new KavitaException($"{comparison} not applicable for Series.AverageReadTime");
            default:
                throw new ArgumentOutOfRangeException(nameof(comparison), comparison, null);
        }
    }

    public static IQueryable<Series> HasPublicationStatus(this IQueryable<Series> queryable, bool condition,
        FilterComparison comparison, IList<PublicationStatus> pubStatues)
    {
        if (!condition || pubStatues.Count == 0) return queryable;

        var firstStatus = pubStatues.First();
        switch (comparison)
        {
            case FilterComparison.Equal:
                return queryable.Where(s => s.Metadata.PublicationStatus == firstStatus);
            case FilterComparison.GreaterThan:
            case FilterComparison.GreaterThanEqual:
            case FilterComparison.LessThan:
            case FilterComparison.LessThanEqual:
            case FilterComparison.Matches:
                throw new KavitaException($"{comparison} not applicable for Series.PublicationStatus");
            case FilterComparison.Contains:
                return queryable.Where(s => pubStatues.Contains(s.Metadata.PublicationStatus));
            default:
                throw new ArgumentOutOfRangeException(nameof(comparison), comparison, null);
        }
    }

    /// <summary>
    ///
    /// </summary>
    /// <param name="queryable"></param>
    /// <param name="condition"></param>
    /// <param name="comparison"></param>
    /// <param name="readProgress"></param>
    /// <param name="userId"></param>
    /// <returns></returns>
    /// <remarks>This is more taxing on memory as the percentage calculation must be done in Memory</remarks>
    /// <exception cref="KavitaException"></exception>
    /// <exception cref="ArgumentOutOfRangeException"></exception>
    public static IQueryable<Series> HasReadingProgress(this IQueryable<Series> queryable, bool condition,
        FilterComparison comparison, int readProgress, int userId)
    {
        if (!condition) return queryable;

        var subQuery = queryable
            .Include(s => s.Progress)
            .Select(s => new
            {
                Series = s,
                Percentage = Math.Truncate(((double)s.Progress.Where(p => p.AppUserId == userId).Sum(p => p.PagesRead)
                                            / s.Pages) * 100)
            })
            .AsEnumerable();

        switch (comparison)
        {
            case FilterComparison.Equal:
                subQuery = subQuery.Where(s => s.Percentage == readProgress);
                break;
            case FilterComparison.GreaterThan:
                subQuery = subQuery.Where(s => s.Percentage > readProgress);
                break;
            case FilterComparison.GreaterThanEqual:
                subQuery = subQuery.Where(s => s.Percentage >= readProgress);
                break;
            case FilterComparison.LessThan:
                subQuery = subQuery.Where(s => s.Percentage < readProgress);
                break;
            case FilterComparison.LessThanEqual:
                subQuery = subQuery.Where(s => s.Percentage <= readProgress);
                break;
            case FilterComparison.Matches:
            case FilterComparison.Contains:
                throw new KavitaException($"{comparison} not applicable for Series.ReadProgress");
            default:
                throw new ArgumentOutOfRangeException(nameof(comparison), comparison, null);
        }

        var ids = subQuery.Select(s => s.Series.Id).ToList();
        return queryable.Where(s => ids.Contains(s.Id));
    }

    public static IQueryable<Series> HasTags(this IQueryable<Series> queryable, bool condition,
        FilterComparison comparison, IList<int> tags)
    {
        if (!condition || tags.Count == 0) return queryable;

        switch (comparison)
        {
            case FilterComparison.Equal:
            case FilterComparison.Contains:
                return queryable.Where(s => s.Metadata.Tags.Any(t => tags.Contains(t.Id)));
            case FilterComparison.GreaterThan:
            case FilterComparison.GreaterThanEqual:
            case FilterComparison.LessThan:
            case FilterComparison.LessThanEqual:
            case FilterComparison.Matches:
                throw new KavitaException($"{comparison} not applicable for Series.Tags");
            default:
                throw new ArgumentOutOfRangeException(nameof(comparison), comparison, null);
        }
    }

    public static IQueryable<Series> HasPeople(this IQueryable<Series> queryable, bool condition,
        FilterComparison comparison, IList<int> people)
    {
        if (!condition || people.Count == 0) return queryable;

        switch (comparison)
        {
            case FilterComparison.Equal:
            case FilterComparison.Contains:
                return queryable.Where(s => s.Metadata.People.Any(p => people.Contains(p.Id)));
            case FilterComparison.GreaterThan:
            case FilterComparison.GreaterThanEqual:
            case FilterComparison.LessThan:
            case FilterComparison.LessThanEqual:
            case FilterComparison.Matches:
                throw new KavitaException($"{comparison} not applicable for Series.People");
            default:
                throw new ArgumentOutOfRangeException(nameof(comparison), comparison, null);
        }
    }

    public static IQueryable<Series> HasGenre(this IQueryable<Series> queryable, bool condition,
        FilterComparison comparison, IList<int> genres)
    {
        if (!condition || genres.Count == 0) return queryable;

        switch (comparison)
        {
            case FilterComparison.Equal:
            case FilterComparison.Contains:
                return queryable.Where(s => s.Metadata.Genres.Any(p => genres.Contains(p.Id)));
            case FilterComparison.GreaterThan:
            case FilterComparison.GreaterThanEqual:
            case FilterComparison.LessThan:
            case FilterComparison.LessThanEqual:
            case FilterComparison.Matches:
                throw new KavitaException($"{comparison} not applicable for Series.Genres");
            default:
                throw new ArgumentOutOfRangeException(nameof(comparison), comparison, null);
        }
    }

    public static IQueryable<Series> HasFormat(this IQueryable<Series> queryable, bool condition,
        FilterComparison comparison, IList<MangaFormat> formats)
    {
        if (!condition || formats.Count == 0) return queryable;

        switch (comparison)
        {
            case FilterComparison.Equal:
            case FilterComparison.Contains:
                return queryable.Where(s => formats.Contains(s.Format));
            case FilterComparison.GreaterThan:
            case FilterComparison.GreaterThanEqual:
            case FilterComparison.LessThan:
            case FilterComparison.LessThanEqual:
            case FilterComparison.Matches:
                throw new KavitaException($"{comparison} not applicable for Series.Format");
            default:
                throw new ArgumentOutOfRangeException(nameof(comparison), comparison, null);
        }
    }

    public static IQueryable<Series> HasCollectionTags(this IQueryable<Series> queryable, bool condition,
        FilterComparison comparison, IList<int> collectionTags)
    {
        if (!condition || collectionTags.Count == 0) return queryable;

        var first = collectionTags.First();
        switch (comparison)
        {
            case FilterComparison.Equal:
            case FilterComparison.Contains:
                return queryable.Where(s => s.Metadata.CollectionTags.Any(t => collectionTags.Contains(t.Id)));
            case FilterComparison.GreaterThan:
            case FilterComparison.GreaterThanEqual:
            case FilterComparison.LessThan:
            case FilterComparison.LessThanEqual:
            case FilterComparison.Matches:
                throw new KavitaException($"{comparison} not applicable for Series.CollectionTags");
            default:
                throw new ArgumentOutOfRangeException(nameof(comparison), comparison, null);
        }
    }

    public static IQueryable<Series> HasName(this IQueryable<Series> queryable, bool condition,
        FilterComparison comparison, string queryString)
    {
        if (string.IsNullOrEmpty(queryString) || !condition) return queryable;

        switch (comparison)
        {
            case FilterComparison.Equal:
                return queryable.Where(s => s.Name.Equals(queryString)
                                            || s.OriginalName.Equals(queryString)
                                            || s.LocalizedName.Equals(queryString)
                                            || s.SortName.Equals(queryString));
            case FilterComparison.GreaterThan:
            case FilterComparison.GreaterThanEqual:
            case FilterComparison.LessThan:
            case FilterComparison.LessThanEqual:
            case FilterComparison.Contains:
                throw new KavitaException($"{comparison} not applicable for Series.Name");
            case FilterComparison.Matches:
                return queryable.Where(s => EF.Functions.Like(s.Name, $"%{queryString}%")
                                            ||EF.Functions.Like(s.OriginalName, $"%{queryString}%")
                                            || EF.Functions.Like(s.LocalizedName, $"%{queryString}%")
                                            || EF.Functions.Like(s.SortName, $"%{queryString}%"));
            default:
                throw new ArgumentOutOfRangeException(nameof(comparison), comparison, null);
        }
    }


    /// <summary>
    /// Performs a filter on a property selector for a given Entity only if a conditional is true
    /// </summary>
    /// <param name="condition">If true, the IQueryable will be modified</param>
    /// <param name="propertySelector">Selector of the property to use in Comparison</param>
    /// <param name="comparison">Type of Comparision to use. Not the limitations</param>
    /// <param name="value">Value to be used in the conversion</param>
    /// <typeparam name="T">Entity that is being passed for selector</typeparam>
    /// <typeparam name="TPV">Return type of the selector</typeparam>
    /// <typeparam name="TV">Type of the value</typeparam>
    /// <returns></returns>
    /// <exception cref="ArgumentNullException"></exception>
    /// <exception cref="ArgumentException"></exception>
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
            case FilterComparison.Matches:
                var matchesMethod = typeof(DbFunctionsExtensions).GetMethod(nameof(DbFunctionsExtensions.Like), new[] { typeof(DbFunctions), typeof(string), typeof(string) });
                var dbFunctions = typeof(EF).GetMethod(nameof(EF.Functions))?.Invoke(null, null);
                var searchExpression = Expression.Constant($"%{value}%");
                return Expression.Call(matchesMethod, Expression.Constant(dbFunctions), property, searchExpression);
            case FilterComparison.Contains:
                if (!typeof(TV).FullName.Contains("IList"))
                {
                    throw new ArgumentException("Contains filter can only be applied to properties of type IList", nameof(property));
                }

                try
                {
                    return Expression.Call(
                        Expression.Constant(value),
                        typeof(IList).GetMethod("Contains")!,
                        property);
                }
                catch
                {
                    // This doesn't work. I might need to rethink Contains
                    var method = typeof(List<TV>).MakeGenericType(typeof(TV)).GetMethod("Contains")!;
                    var boxedValue = Expression.Convert(Expression.Constant(value), typeof(TV));
                    return Expression.Call(boxedValue, method, property);
                }
            default:
                throw new ArgumentOutOfRangeException(nameof(comparison), comparison, null);
        }
    }


}
#nullable disable
