using System;
using System.Collections.Generic;
using System.Linq;
using API.DTOs.Filtering.v2;
using API.Entities;
using Kavita.Common;
using Microsoft.EntityFrameworkCore;

namespace API.Extensions.QueryExtensions.Filtering;

public static class AnnotationFilter
{

    public static IQueryable<AppUserAnnotation> IsOwnedBy(this IQueryable<AppUserAnnotation> queryable, bool condition,
        FilterComparison comparison, IList<int> ownerIds)
    {
        if (ownerIds.Count == 0 || !condition) return queryable;

        return comparison switch
        {
            FilterComparison.Equal => queryable.Where(a => a.AppUserId == ownerIds[0]),
            FilterComparison.Contains => queryable.Where(a => ownerIds.Contains(a.AppUserId)),
            FilterComparison.NotContains => queryable.Where(a => !ownerIds.Contains(a.AppUserId)),
            FilterComparison.NotEqual => queryable.Where(a => a.AppUserId != ownerIds[0]),
            _ => throw new ArgumentOutOfRangeException(nameof(comparison), comparison, null),
        };
    }

    public static IQueryable<AppUserAnnotation> IsInLibrary(this IQueryable<AppUserAnnotation> queryable, bool condition,
        FilterComparison comparison, IList<int> libraryIds)
    {
        if (libraryIds.Count == 0 || !condition) return queryable;

        return comparison switch
        {
            FilterComparison.Equal => queryable.Where(a => a.LibraryId == libraryIds[0]),
            FilterComparison.Contains => queryable.Where(a => libraryIds.Contains(a.LibraryId)),
            FilterComparison.NotContains => queryable.Where(a => !libraryIds.Contains(a.LibraryId)),
            FilterComparison.NotEqual => queryable.Where(a => a.LibraryId != libraryIds[0]),
            _ => throw new ArgumentOutOfRangeException(nameof(comparison), comparison, null),
        };
    }

    public static IQueryable<AppUserAnnotation> HasSeries(this IQueryable<AppUserAnnotation> queryable, bool condition,
        FilterComparison comparison, IList<int> seriesIds)
    {
        if (seriesIds.Count == 0 || !condition) return queryable;

        return comparison switch
        {
            FilterComparison.Equal => queryable.Where(a => a.SeriesId == seriesIds[0]),
            FilterComparison.Contains => queryable.Where(a => seriesIds.Contains(a.SeriesId)),
            FilterComparison.NotContains => queryable.Where(a => !seriesIds.Contains(a.SeriesId)),
            FilterComparison.NotEqual => queryable.Where(a => a.SeriesId != seriesIds[0]),
            _ => throw new ArgumentOutOfRangeException(nameof(comparison), comparison, null),
        };
    }

    public static IQueryable<AppUserAnnotation> IsUsingHighlights(this IQueryable<AppUserAnnotation> queryable, bool condition,
        FilterComparison comparison, IList<int> highlightSlotIdxs)
    {
        if (highlightSlotIdxs.Count == 0 || !condition) return queryable;

        return comparison switch
        {
            FilterComparison.Equal => queryable.Where(a => a.SelectedSlotIndex == highlightSlotIdxs[0]),
            FilterComparison.Contains => queryable.Where(a => highlightSlotIdxs.Contains(a.SelectedSlotIndex)),
            FilterComparison.NotContains => queryable.Where(a => !highlightSlotIdxs.Contains(a.SelectedSlotIndex)),
            FilterComparison.NotEqual => queryable.Where(a => a.SelectedSlotIndex != highlightSlotIdxs[0]),
            _ => throw new ArgumentOutOfRangeException(nameof(comparison), comparison, null),
        };
    }

    public static IQueryable<AppUserAnnotation> HasSelected(this IQueryable<AppUserAnnotation> queryable, bool condition,
        FilterComparison comparison, string value)
    {
        if (string.IsNullOrEmpty(value) || !condition) return queryable;

        return comparison switch
        {
            FilterComparison.Equal => queryable.Where(a => a.SelectedText == value),
            FilterComparison.NotEqual => queryable.Where(a => a.SelectedText != value),
            FilterComparison.BeginsWith => queryable.Where(a => EF.Functions.Like(a.SelectedText, $"{value}%")),
            FilterComparison.EndsWith => queryable.Where(a => EF.Functions.Like(a.SelectedText, $"%{value}")),
            FilterComparison.Matches => queryable.Where(a => EF.Functions.Like(a.SelectedText, $"%{value}%")),
            FilterComparison.GreaterThan or
            FilterComparison.GreaterThanEqual or
            FilterComparison.LessThan or
            FilterComparison.LessThanEqual or
            FilterComparison.Contains or
            FilterComparison.MustContains or
            FilterComparison.NotContains or
            FilterComparison.IsBefore or
            FilterComparison.IsAfter or
            FilterComparison.IsInLast or
            FilterComparison.IsNotInLast or
            FilterComparison.IsEmpty => throw new KavitaException($"{comparison} is not applicable for Annotation.SelectedText"),
            _ => throw new ArgumentOutOfRangeException(nameof(comparison), comparison, null),
        };
    }

    public static IQueryable<AppUserAnnotation> HasCommented(this IQueryable<AppUserAnnotation> queryable, bool condition,
        FilterComparison comparison, string value)
    {
        if (string.IsNullOrEmpty(value) || !condition) return queryable;

        return comparison switch
        {
            FilterComparison.Equal => queryable.Where(a => a.CommentPlainText == value),
            FilterComparison.NotEqual => queryable.Where(a => a.CommentPlainText != value),
            FilterComparison.BeginsWith => queryable.Where(a => EF.Functions.Like(a.CommentPlainText, $"{value}%")),
            FilterComparison.EndsWith => queryable.Where(a => EF.Functions.Like(a.CommentPlainText, $"%{value}")),
            FilterComparison.Matches => queryable.Where(a => EF.Functions.Like(a.CommentPlainText, $"%{value}%")),
            FilterComparison.GreaterThan or
            FilterComparison.GreaterThanEqual or
            FilterComparison.LessThan or
            FilterComparison.LessThanEqual or
            FilterComparison.Contains or
            FilterComparison.MustContains or
            FilterComparison.NotContains or
            FilterComparison.IsBefore or
            FilterComparison.IsAfter or
            FilterComparison.IsInLast or
            FilterComparison.IsNotInLast or
            FilterComparison.IsEmpty => throw new KavitaException($"{comparison} is not applicable for Annotation.CommentPlainText"),
            _ => throw new ArgumentOutOfRangeException(nameof(comparison), comparison, null),
        };
    }

    public static IQueryable<AppUserAnnotation> HasLikes(this IQueryable<AppUserAnnotation> queryable, bool condition,
        FilterComparison comparison, int value)
    {
        if (!condition) return queryable;

        return comparison switch
        {
            FilterComparison.Equal => queryable.Where(a => a.Likes.Count == value),
            FilterComparison.NotEqual => queryable.Where(a => a.Likes.Count != value),
            FilterComparison.GreaterThan => queryable.Where(a => a.Likes.Count > value),
            FilterComparison.GreaterThanEqual => queryable.Where(a => a.Likes.Count >= value),
            FilterComparison.LessThan => queryable.Where(a => a.Likes.Count < value),
            FilterComparison.LessThanEqual => queryable.Where(a => a.Likes.Count <= value),
            FilterComparison.BeginsWith or
            FilterComparison.EndsWith or
            FilterComparison.Matches or
            FilterComparison.Contains or
            FilterComparison.MustContains or
            FilterComparison.NotContains or
            FilterComparison.IsBefore or
            FilterComparison.IsAfter or
            FilterComparison.IsInLast or
            FilterComparison.IsNotInLast or
            FilterComparison.IsEmpty => throw new KavitaException($"{comparison} is not applicable for Annotation.Likes"),
            _ => throw new ArgumentOutOfRangeException(nameof(comparison), comparison, null),
        };
    }

    public static IQueryable<AppUserAnnotation> IsLikedBy(this IQueryable<AppUserAnnotation> queryable, bool condition,
        FilterComparison comparison, IList<int> value)
    {
        if (value.Count == 0 || !condition) return queryable;

        return comparison switch
        {
            FilterComparison.Equal => queryable.Where(a => a.Likes.Contains(value[0])),
            FilterComparison.NotEqual => queryable.Where(a => a!.Likes.Contains(value[0])),
            FilterComparison.Contains => queryable.Where(a => a.Likes.Any(value.Contains)),
            FilterComparison.NotContains => queryable.Where(a => !a.Likes.Any(value.Contains)),
            FilterComparison.GreaterThan or
            FilterComparison.GreaterThanEqual or
            FilterComparison.LessThan or
            FilterComparison.LessThanEqual or
            FilterComparison.BeginsWith or
            FilterComparison.EndsWith or
            FilterComparison.Matches or
            FilterComparison.MustContains or
            FilterComparison.IsBefore or
            FilterComparison.IsAfter or
            FilterComparison.IsInLast or
            FilterComparison.IsNotInLast or
            FilterComparison.IsEmpty => throw new KavitaException($"{comparison} is not applicable for Annotation.Likes"),
            _ => throw new ArgumentOutOfRangeException(nameof(comparison), comparison, null),
        };
    }


}
