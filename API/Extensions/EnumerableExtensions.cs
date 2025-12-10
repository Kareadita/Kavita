using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using API.Data.Misc;
using API.Entities;
using API.Entities.Enums;
using API.Entities.Metadata;

namespace API.Extensions;
#nullable enable

public static class EnumerableExtensions
{
    private static readonly Regex Regex = new Regex(@"\d+", RegexOptions.Compiled, TimeSpan.FromMilliseconds(500));

    /// <summary>
    /// A natural sort implementation
    /// </summary>
    /// <param name="items">IEnumerable to process</param>
    /// <param name="selector">Function that produces a string. Does not support null values</param>
    /// <param name="stringComparer">Defaults to CurrentCulture</param>
    /// <typeparam name="T"></typeparam>
    /// <returns>Sorted Enumerable</returns>
    public static IEnumerable<T> OrderByNatural<T>(this IEnumerable<T> items, Func<T, string> selector, StringComparer? stringComparer = null)
    {
        var list = items.ToList();
        var maxDigits = list
            .SelectMany(i => Regex.Matches(selector(i))
                .Select(digitChunk => (int?)digitChunk.Value.Length))
            .Max() ?? 0;

        return list.OrderBy(i => Regex.Replace(selector(i), match => match.Value.PadLeft(maxDigits, '0')), stringComparer ?? StringComparer.CurrentCulture);
    }

    public static IEnumerable<RecentlyAddedSeries> RestrictAgainstAgeRestriction(this IEnumerable<RecentlyAddedSeries> items, AgeRestriction restriction)
    {
        if (restriction.AgeRating == AgeRating.NotApplicable) return items;
        var q = items.Where(s => s.AgeRating <= restriction.AgeRating);
        if (!restriction.IncludeUnknowns)
        {
            return q.Where(s => s.AgeRating != AgeRating.Unknown);
        }

        return q;
    }

    public static IEnumerable<SeriesMetadata> RestrictAgainstAgeRestriction(this IEnumerable<SeriesMetadata> items, AgeRestriction restriction)
    {
        if (restriction.AgeRating == AgeRating.NotApplicable) return items;
        var q = items.Where(s => s.AgeRating <= restriction.AgeRating);
        if (!restriction.IncludeUnknowns)
        {
            return q.Where(s => s.AgeRating != AgeRating.Unknown);
        }

        return q;
    }

    public static IEnumerable<Chapter> RestrictAgainstAgeRestriction(this IEnumerable<Chapter> items, AgeRestriction restriction)
    {
        if (restriction.AgeRating == AgeRating.NotApplicable) return items;
        var q = items.Where(s => s.AgeRating <= restriction.AgeRating);
        if (!restriction.IncludeUnknowns)
        {
            return q.Where(s => s.AgeRating != AgeRating.Unknown);
        }

        return q;
    }

    /// <summary>
    /// Safety net around Max, returning the default value if source contains no elements
    /// </summary>
    /// <param name="source"></param>
    /// <param name="selector"></param>
    /// <param name="defaultValue"></param>
    /// <typeparam name="TSource"></typeparam>
    /// <typeparam name="TResult"></typeparam>
    /// <returns></returns>
    public static TResult? MaxOrDefault<TSource, TResult>(
        this IList<TSource> source,
        Func<TSource, TResult> selector,
        TResult? defaultValue)
    {
        return source.Count == 0 ? defaultValue : source.Max(selector);
    }

    /// <summary>
    /// Safety wrapper around Min, returning the default value if source has no elements
    /// </summary>
    /// <param name="source"></param>
    /// <param name="selector"></param>
    /// <param name="defaultValue"></param>
    /// <typeparam name="TSource"></typeparam>
    /// <typeparam name="TResult"></typeparam>
    /// <returns></returns>
    public static TResult? MinOrDefault<TSource, TResult>(
        this IList<TSource> source,
        Func<TSource, TResult> selector,
        TResult? defaultValue)
    {
        return source.Count == 0 ? defaultValue : source.Min(selector);
    }

    public static IEnumerable<TSource> WhereNotNull<TSource>(this IEnumerable<TSource?> source)
    where TSource : class
    {
        return source.Where(item => item != null)!;
    }
}
