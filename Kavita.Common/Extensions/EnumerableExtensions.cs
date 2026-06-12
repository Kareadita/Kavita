#nullable enable
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;

namespace Kavita.Common.Extensions;

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

    /// <param name="source"></param>
    /// <typeparam name="TSource"></typeparam>
    extension<TSource>(IList<TSource> source)
    {
        /// <summary>
        /// Safety net around Max, returning the default value if the source contains no elements
        /// </summary>
        /// <param name="selector"></param>
        /// <param name="defaultValue"></param>
        /// <typeparam name="TResult"></typeparam>
        /// <returns></returns>
        public TResult? MaxOrDefault<TResult>(Func<TSource, TResult> selector,
            TResult? defaultValue)
        {
            return source.Count == 0 ? defaultValue : source.Max(selector);
        }

        /// <summary>
        /// Safety wrapper around Min, returning the default value if the source has no elements
        /// </summary>
        /// <param name="selector"></param>
        /// <param name="defaultValue"></param>
        /// <typeparam name="TResult"></typeparam>
        /// <returns></returns>
        public TResult? MinOrDefault<TResult>(Func<TSource, TResult> selector,
            TResult? defaultValue)
        {
            return source.Count == 0 ? defaultValue : source.Min(selector);
        }
    }

    public static IEnumerable<TSource> WhereIf<TSource>(this IEnumerable<TSource> source, bool guard, Func<TSource, bool> predicate)
        where TSource : class
    {
        return guard ? source.Where(predicate) : source;
    }

    public static IEnumerable<TSource> WhereNotNull<TSource>(this IEnumerable<TSource?> source)
        where TSource : class
    {
        return source.Where(item => item != null)!;
    }
}
