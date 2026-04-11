using System;
using System.Collections.Generic;
using System.Linq;
using Kavita.Models.DTOs.Filtering.v2;
using Kavita.Models.Entities.User;
using Microsoft.EntityFrameworkCore;

namespace Kavita.Database.Extensions.Filters;

public static class AnnotationFilter
{

    extension(IQueryable<AppUserAnnotation> queryable)
    {
        public IQueryable<AppUserAnnotation> IsOwnedBy(bool condition, FilterComparison comparison, IList<int> ownerIds)
        {
            if (ownerIds.Count == 0 || !condition) return queryable;
            ComparisonProfile.Validate(comparison, ComparisonProfile.List, "Annotation.Owner");

            return comparison switch
            {
                FilterComparison.Equal => queryable.Where(a => a.AppUserId == ownerIds[0]),
                FilterComparison.NotEqual => queryable.Where(a => a.AppUserId != ownerIds[0]),
                FilterComparison.Contains => queryable.Where(a => ownerIds.Contains(a.AppUserId)),
                FilterComparison.NotContains => queryable.Where(a => !ownerIds.Contains(a.AppUserId)),
                FilterComparison.MustContains => queryable.Where(a => ownerIds.All(o => o == a.AppUserId)),
                _ => throw new ArgumentOutOfRangeException(nameof(comparison), comparison, null),
            };
        }

        public IQueryable<AppUserAnnotation> IsInLibrary(bool condition, FilterComparison comparison, IList<int> libraryIds)
        {
            if (libraryIds.Count == 0 || !condition) return queryable;
            ComparisonProfile.Validate(comparison, ComparisonProfile.List, "Annotation.Library");

            return comparison switch
            {
                FilterComparison.Equal => queryable.Where(a => a.LibraryId == libraryIds[0]),
                FilterComparison.Contains => queryable.Where(a => libraryIds.Contains(a.LibraryId)),
                FilterComparison.NotContains => queryable.Where(a => !libraryIds.Contains(a.LibraryId)),
                FilterComparison.NotEqual => queryable.Where(a => a.LibraryId != libraryIds[0]),
                _ => throw new ArgumentOutOfRangeException(nameof(comparison), comparison, null),
            };
        }

        public IQueryable<AppUserAnnotation> HasSeries(bool condition, FilterComparison comparison, IList<int> seriesIds)
        {
            if (seriesIds.Count == 0 || !condition) return queryable;
            ComparisonProfile.Validate(comparison, ComparisonProfile.List, "Annotation.Series");

            return comparison switch
            {
                FilterComparison.Equal => queryable.Where(a => a.SeriesId == seriesIds[0]),
                FilterComparison.Contains => queryable.Where(a => seriesIds.Contains(a.SeriesId)),
                FilterComparison.NotContains => queryable.Where(a => !seriesIds.Contains(a.SeriesId)),
                FilterComparison.NotEqual => queryable.Where(a => a.SeriesId != seriesIds[0]),
                _ => throw new ArgumentOutOfRangeException(nameof(comparison), comparison, null),
            };
        }

        public IQueryable<AppUserAnnotation> IsUsingHighlights(bool condition, FilterComparison comparison, IList<int> highlightSlotIdxs)
        {
            if (highlightSlotIdxs.Count == 0 || !condition) return queryable;
            ComparisonProfile.Validate(comparison, ComparisonProfile.List, "Annotation.HighlightSlot");

            return comparison switch
            {
                FilterComparison.Equal => queryable.Where(a => a.SelectedSlotIndex == highlightSlotIdxs[0]),
                FilterComparison.Contains => queryable.Where(a => highlightSlotIdxs.Contains(a.SelectedSlotIndex)),
                FilterComparison.NotContains => queryable.Where(a => !highlightSlotIdxs.Contains(a.SelectedSlotIndex)),
                FilterComparison.NotEqual => queryable.Where(a => a.SelectedSlotIndex != highlightSlotIdxs[0]),
                _ => throw new ArgumentOutOfRangeException(nameof(comparison), comparison, null),
            };
        }

        public IQueryable<AppUserAnnotation> HasSelected(bool condition, FilterComparison comparison, string value)
        {
            if (string.IsNullOrEmpty(value) || !condition) return queryable;
            ComparisonProfile.Validate(comparison, ComparisonProfile.String, "Annotation.SelectedText");

            return comparison switch
            {
                FilterComparison.Equal => queryable.Where(a => a.SelectedText == value),
                FilterComparison.NotEqual => queryable.Where(a => a.SelectedText != value),
                FilterComparison.BeginsWith => queryable.Where(a => EF.Functions.Like(a.SelectedText, $"{value}%")),
                FilterComparison.EndsWith => queryable.Where(a => EF.Functions.Like(a.SelectedText, $"%{value}")),
                FilterComparison.Matches => queryable.Where(a => EF.Functions.Like(a.SelectedText, $"%{value}%")),
                _ => throw new ArgumentOutOfRangeException(nameof(comparison), comparison, null),
            };
        }

        public IQueryable<AppUserAnnotation> HasCommented(bool condition, FilterComparison comparison, string value)
        {
            if (string.IsNullOrEmpty(value) || !condition) return queryable;
            ComparisonProfile.Validate(comparison, ComparisonProfile.String, "Annotation.CommentPlainText");

            return comparison switch
            {
                FilterComparison.Equal => queryable.Where(a => a.CommentPlainText == value),
                FilterComparison.NotEqual => queryable.Where(a => a.CommentPlainText != value),
                FilterComparison.BeginsWith => queryable.Where(a => EF.Functions.Like(a.CommentPlainText, $"{value}%")),
                FilterComparison.EndsWith => queryable.Where(a => EF.Functions.Like(a.CommentPlainText, $"%{value}")),
                FilterComparison.Matches => queryable.Where(a => EF.Functions.Like(a.CommentPlainText, $"%{value}%")),
                _ => throw new ArgumentOutOfRangeException(nameof(comparison), comparison, null),
            };
        }

        public IQueryable<AppUserAnnotation> HasLikes(bool condition, FilterComparison comparison, int value)
        {
            if (!condition) return queryable;
            ComparisonProfile.Validate(comparison, ComparisonProfile.Numeric, "Annotation.Likes");

            return comparison switch
            {
                FilterComparison.Equal => queryable.Where(a => a.Likes.Count == value),
                FilterComparison.NotEqual => queryable.Where(a => a.Likes.Count != value),
                FilterComparison.GreaterThan => queryable.Where(a => a.Likes.Count > value),
                FilterComparison.GreaterThanEqual => queryable.Where(a => a.Likes.Count >= value),
                FilterComparison.LessThan => queryable.Where(a => a.Likes.Count < value),
                FilterComparison.LessThanEqual => queryable.Where(a => a.Likes.Count <= value),
                _ => throw new ArgumentOutOfRangeException(nameof(comparison), comparison, null),
            };
        }

        public IQueryable<AppUserAnnotation> IsLikedBy(bool condition, FilterComparison comparison, IList<int> value)
        {
            if (value.Count == 0 || !condition) return queryable;
            ComparisonProfile.Validate(comparison, ComparisonProfile.List, "Annotation.LikedBy");

            return comparison switch
            {
                FilterComparison.Equal => queryable.Where(a => a.Likes.Contains(value[0])),
                FilterComparison.NotEqual => queryable.Where(a => !a.Likes.Contains(value[0])),
                FilterComparison.Contains => queryable.Where(a => a.Likes.Any(l => value.Contains(l))),
                FilterComparison.NotContains => queryable.Where(a => !a.Likes.Any(l => value.Contains(l))),
                FilterComparison.MustContains => queryable.Where(a => value.All(v => a.Likes.Contains(v))),
                _ => throw new ArgumentOutOfRangeException(nameof(comparison), comparison, null),
            };
        }
    }
}
