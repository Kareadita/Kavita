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
            case FilterComparison.Contains:
                return queryable.Where(s => languages.Contains(s.Metadata.Language));
            case FilterComparison.NotContains:
                return queryable.Where(s => !languages.Contains(s.Metadata.Language));
            case FilterComparison.NotEqual:
                return queryable.Where(s => !s.Metadata.Language.Equals(languages.First()));
            case FilterComparison.Matches:
                return queryable.Where(s => EF.Functions.Like(s.Metadata.Language, $"{languages.First()}%"));
            case FilterComparison.GreaterThan:
            case FilterComparison.GreaterThanEqual:
            case FilterComparison.LessThan:
            case FilterComparison.LessThanEqual:
            case FilterComparison.BeginsWith:
            case FilterComparison.EndsWith:
            case FilterComparison.IsBefore:
            case FilterComparison.IsAfter:
            case FilterComparison.IsInLast:
            case FilterComparison.IsNotInLast:
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
            case FilterComparison.IsBefore:
            case FilterComparison.IsAfter:
            case FilterComparison.IsInLast:
            case FilterComparison.IsNotInLast:
            case FilterComparison.Matches:
            case FilterComparison.Contains:
            case FilterComparison.NotContains:
            case FilterComparison.NotEqual:
            case FilterComparison.BeginsWith:
            case FilterComparison.EndsWith:
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
            case FilterComparison.NotContains:
            case FilterComparison.NotEqual:
            case FilterComparison.BeginsWith:
            case FilterComparison.EndsWith:
            case FilterComparison.IsBefore:
            case FilterComparison.IsAfter:
            case FilterComparison.IsInLast:
            case FilterComparison.IsNotInLast:
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
            case FilterComparison.NotContains:
                return queryable.Where(s => !ratings.Contains(s.Metadata.AgeRating));
            case FilterComparison.NotEqual:
                return queryable.Where(s => s.Metadata.AgeRating != firstRating);
            case FilterComparison.Matches:
            case FilterComparison.BeginsWith:
            case FilterComparison.EndsWith:
            case FilterComparison.IsBefore:
            case FilterComparison.IsAfter:
            case FilterComparison.IsInLast:
            case FilterComparison.IsNotInLast:
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
            case FilterComparison.NotContains:
            case FilterComparison.NotEqual:
            case FilterComparison.BeginsWith:
            case FilterComparison.EndsWith:
            case FilterComparison.IsBefore:
            case FilterComparison.IsAfter:
            case FilterComparison.IsInLast:
            case FilterComparison.IsNotInLast:
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
            case FilterComparison.Contains:
                return queryable.Where(s => pubStatues.Contains(s.Metadata.PublicationStatus));
            case FilterComparison.NotContains:
                return queryable.Where(s => !pubStatues.Contains(s.Metadata.PublicationStatus));
            case FilterComparison.NotEqual:
                return queryable.Where(s => s.Metadata.PublicationStatus != firstStatus);
            case FilterComparison.GreaterThan:
            case FilterComparison.GreaterThanEqual:
            case FilterComparison.LessThan:
            case FilterComparison.LessThanEqual:
            case FilterComparison.BeginsWith:
            case FilterComparison.EndsWith:
            case FilterComparison.IsBefore:
            case FilterComparison.IsAfter:
            case FilterComparison.IsInLast:
            case FilterComparison.IsNotInLast:
            case FilterComparison.Matches:
                throw new KavitaException($"{comparison} not applicable for Series.PublicationStatus");
            default:
                throw new ArgumentOutOfRangeException(nameof(comparison), comparison, null);
        }
    }

    /// <summary>
    ///
    /// </summary>
    /// <remarks>This is more taxing on memory as the percentage calculation must be done in Memory</remarks>
    /// <exception cref="KavitaException"></exception>
    /// <exception cref="ArgumentOutOfRangeException"></exception>
    public static IQueryable<Series> HasReadingProgress(this IQueryable<Series> queryable, bool condition,
        FilterComparison comparison, int readProgress, int userId)
    {
        if (!condition) return queryable;

        var subQuery = queryable
            .Include(s => s.Progress)
            .Where(s => s.Progress != null)
            .Select(s => new
            {
                Series = s,
                Percentage = Math.Truncate(((double) s.Progress
                    .Where(p => p != null && p.AppUserId == userId)
                    .Sum(p => p != null ? (p.PagesRead * 1.0f / s.Pages) : 0) * 100))
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
            case FilterComparison.NotContains:
            case FilterComparison.NotEqual:
            case FilterComparison.BeginsWith:
            case FilterComparison.EndsWith:
            case FilterComparison.IsBefore:
            case FilterComparison.IsAfter:
            case FilterComparison.IsInLast:
            case FilterComparison.IsNotInLast:
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
            case FilterComparison.Contains:
                return queryable.Where(s => s.Metadata.Tags.Any(t => tags.Contains(t.Id)));
            case FilterComparison.GreaterThan:
            case FilterComparison.GreaterThanEqual:
            case FilterComparison.LessThan:
            case FilterComparison.LessThanEqual:
            case FilterComparison.Matches:
            case FilterComparison.Equal:
            case FilterComparison.NotContains:
            case FilterComparison.NotEqual:
            case FilterComparison.BeginsWith:
            case FilterComparison.EndsWith:
            case FilterComparison.IsBefore:
            case FilterComparison.IsAfter:
            case FilterComparison.IsInLast:
            case FilterComparison.IsNotInLast:
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
            case FilterComparison.Contains:
                return queryable.Where(s => s.Metadata.People.Any(p => people.Contains(p.Id)));
            case FilterComparison.Equal:
            case FilterComparison.GreaterThan:
            case FilterComparison.GreaterThanEqual:
            case FilterComparison.LessThan:
            case FilterComparison.LessThanEqual:
            case FilterComparison.NotContains:
            case FilterComparison.NotEqual:
            case FilterComparison.BeginsWith:
            case FilterComparison.EndsWith:
            case FilterComparison.IsBefore:
            case FilterComparison.IsAfter:
            case FilterComparison.IsInLast:
            case FilterComparison.IsNotInLast:
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
            case FilterComparison.NotEqual:
            case FilterComparison.NotContains:
                return queryable.Where(s => s.Metadata.Genres.All(p => !genres.Contains(p.Id)));
            case FilterComparison.GreaterThan:
            case FilterComparison.GreaterThanEqual:
            case FilterComparison.LessThan:
            case FilterComparison.LessThanEqual:
            case FilterComparison.Matches:
            case FilterComparison.BeginsWith:
            case FilterComparison.EndsWith:
            case FilterComparison.IsBefore:
            case FilterComparison.IsAfter:
            case FilterComparison.IsInLast:
            case FilterComparison.IsNotInLast:
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
            case FilterComparison.NotContains:
            case FilterComparison.NotEqual:
                return queryable.Where(s => !formats.Contains(s.Format));
            case FilterComparison.GreaterThan:
            case FilterComparison.GreaterThanEqual:
            case FilterComparison.LessThan:
            case FilterComparison.LessThanEqual:
            case FilterComparison.Matches:
            case FilterComparison.BeginsWith:
            case FilterComparison.EndsWith:
            case FilterComparison.IsBefore:
            case FilterComparison.IsAfter:
            case FilterComparison.IsInLast:
            case FilterComparison.IsNotInLast:
                throw new KavitaException($"{comparison} not applicable for Series.Format");
            default:
                throw new ArgumentOutOfRangeException(nameof(comparison), comparison, null);
        }
    }

    public static IQueryable<Series> HasCollectionTags(this IQueryable<Series> queryable, bool condition,
        FilterComparison comparison, IList<int> collectionTags)
    {
        if (!condition || collectionTags.Count == 0) return queryable;

        //var first = collectionTags.First();
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
            case FilterComparison.NotContains:
            case FilterComparison.NotEqual:
            case FilterComparison.BeginsWith:
            case FilterComparison.EndsWith:
            case FilterComparison.IsBefore:
            case FilterComparison.IsAfter:
            case FilterComparison.IsInLast:
            case FilterComparison.IsNotInLast:
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
            case FilterComparison.BeginsWith:
                return queryable.Where(s => EF.Functions.Like(s.Name, $"{queryString}%")
                                            ||EF.Functions.Like(s.OriginalName, $"{queryString}%")
                                            || EF.Functions.Like(s.LocalizedName, $"{queryString}%")
                                            || EF.Functions.Like(s.SortName, $"{queryString}%"));
            case FilterComparison.EndsWith:
                return queryable.Where(s => EF.Functions.Like(s.Name, $"%{queryString}")
                                            ||EF.Functions.Like(s.OriginalName, $"%{queryString}")
                                            || EF.Functions.Like(s.LocalizedName, $"%{queryString}")
                                            || EF.Functions.Like(s.SortName, $"%{queryString}"));
            case FilterComparison.Matches:
                return queryable.Where(s => EF.Functions.Like(s.Name, $"%{queryString}%")
                                            ||EF.Functions.Like(s.OriginalName, $"%{queryString}%")
                                            || EF.Functions.Like(s.LocalizedName, $"%{queryString}%")
                                            || EF.Functions.Like(s.SortName, $"%{queryString}%"));
            case FilterComparison.NotEqual:
                return queryable.Where(s => s.Name != queryString
                                            || s.OriginalName != queryString
                                            || s.LocalizedName != queryString
                                            || s.SortName != queryString);
            case FilterComparison.NotContains:
            case FilterComparison.GreaterThan:
            case FilterComparison.GreaterThanEqual:
            case FilterComparison.LessThan:
            case FilterComparison.LessThanEqual:
            case FilterComparison.Contains:
            case FilterComparison.IsBefore:
            case FilterComparison.IsAfter:
            case FilterComparison.IsInLast:
            case FilterComparison.IsNotInLast:
                throw new KavitaException($"{comparison} not applicable for Series.Name");
            default:
                throw new ArgumentOutOfRangeException(nameof(comparison), comparison, "Filter Comparison is not supported");
        }
    }

    public static IQueryable<Series> HasSummary(this IQueryable<Series> queryable, bool condition,
        FilterComparison comparison, string queryString)
    {
        if (string.IsNullOrEmpty(queryString) || !condition) return queryable;

        switch (comparison)
        {
            case FilterComparison.Equal:
                return queryable.Where(s => s.Metadata.Summary.Equals(queryString));
            case FilterComparison.BeginsWith:
                return queryable.Where(s => EF.Functions.Like(s.Metadata.Summary, $"{queryString}%"));
            case FilterComparison.EndsWith:
                return queryable.Where(s => EF.Functions.Like(s.Metadata.Summary, $"%{queryString}"));
            case FilterComparison.Matches:
                return queryable.Where(s => EF.Functions.Like(s.Metadata.Summary, $"%{queryString}%"));
            case FilterComparison.NotEqual:
                return queryable.Where(s => s.Metadata.Summary != queryString);
            case FilterComparison.NotContains:
            case FilterComparison.GreaterThan:
            case FilterComparison.GreaterThanEqual:
            case FilterComparison.LessThan:
            case FilterComparison.LessThanEqual:
            case FilterComparison.Contains:
            case FilterComparison.IsBefore:
            case FilterComparison.IsAfter:
            case FilterComparison.IsInLast:
            case FilterComparison.IsNotInLast:
                throw new KavitaException($"{comparison} not applicable for Series.Metadata.Summary");
            default:
                throw new ArgumentOutOfRangeException(nameof(comparison), comparison, "Filter Comparison is not supported");
        }
    }
}
