using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using API.DTOs.Filtering.v2;
using API.DTOs.Reader;
using API.Entities;

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
            FilterComparison.Equal => queryable.Where(a => a.Series.LibraryId == libraryIds[0]),
            FilterComparison.Contains => queryable.Where(a => libraryIds.Contains(a.Series.LibraryId)),
            FilterComparison.NotContains => queryable.Where(a => !libraryIds.Contains(a.Series.LibraryId)),
            FilterComparison.NotEqual => queryable.Where(a => a.Series.LibraryId != libraryIds[0]),
            _ => throw new ArgumentOutOfRangeException(nameof(comparison), comparison, null),
        };
    }

    public static IQueryable<AppUserAnnotation> IsUsingHighlights(this IQueryable<AppUserAnnotation> queryable, bool condition,
        FilterComparison comparison, IList<int> highlightSlotIdxs)
    {
        if (highlightSlotIdxs.Count == 0 || !condition) return queryable;

        return comparison switch
        {
            FilterComparison.Equal => queryable.Where(a => a.SelectedSlotIndex== highlightSlotIdxs[0]),
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
            FilterComparison.Contains => queryable.Where(a => a.SelectedText.Contains(value)),
            FilterComparison.NotContains => queryable.Where(a => !a.SelectedText.Contains(value)),
            FilterComparison.NotEqual => queryable.Where(a => a.SelectedText != value),
            _ => throw new ArgumentOutOfRangeException(nameof(comparison), comparison, null),
        };
    }

    public static IQueryable<AppUserAnnotation> HasCommented(this IQueryable<AppUserAnnotation> queryable, bool condition,
        FilterComparison comparison, string value)
    {
        if (string.IsNullOrEmpty(value) || !condition) return queryable;

        return comparison switch
        {
            FilterComparison.Equal => queryable.Where(a => a.CommentPlainText != null && a.CommentPlainText == value),
            FilterComparison.NotEqual => queryable.Where(a => a.CommentPlainText != null && a.CommentPlainText != value),
            FilterComparison.Contains => queryable.Where(a => a.CommentPlainText != null && a.CommentPlainText.Contains(value)),
            FilterComparison.NotContains => queryable.Where(a => a.CommentPlainText != null && !a.CommentPlainText.Contains(value)),
            _ => throw new ArgumentOutOfRangeException(nameof(comparison), comparison, null),
        };
    }

}
