using System;
using System.Collections.Generic;
using System.Linq;
using API.DTOs.Filtering.v2;
using API.Entities;
using API.Entities.Enums;
using API.Services.Tasks.Scanner.Parser;
using Kavita.Common;
using Microsoft.EntityFrameworkCore;

namespace API.Extensions.QueryExtensions.Filtering;
#nullable enable

public static class SeriesFilter
{
    private const float FloatingPointTolerance = 0.001f;

    public static IQueryable<Series> HasLanguage(this IQueryable<Series> queryable, bool condition,
        FilterComparison comparison, IList<string> languages)
    {
        if (languages.Count == 0 || !condition) return queryable;

        switch (comparison)
        {
            case FilterComparison.Equal:
                return queryable.Where(s => s.Metadata.Language.Equals(languages[0]));
            case FilterComparison.Contains:
                return queryable.Where(s => languages.Contains(s.Metadata.Language));
            case FilterComparison.MustContains:
                return queryable.Where(s => languages.All(s2 => s2.Equals(s.Metadata.Language)));
            case FilterComparison.NotContains:
                return queryable.Where(s => !languages.Contains(s.Metadata.Language));
            case FilterComparison.NotEqual:
                return queryable.Where(s => !s.Metadata.Language.Equals(languages[0]));
            case FilterComparison.Matches:
                return queryable.Where(s => EF.Functions.Like(s.Metadata.Language, $"{languages[0]}%"));
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
            case FilterComparison.IsEmpty:
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
            case FilterComparison.IsAfter:
                return queryable.Where(s => s.Metadata.ReleaseYear > releaseYear);
            case FilterComparison.GreaterThanEqual:
                return queryable.Where(s => s.Metadata.ReleaseYear >= releaseYear);
            case FilterComparison.LessThan:
            case FilterComparison.IsBefore:
                return queryable.Where(s => s.Metadata.ReleaseYear < releaseYear);
            case FilterComparison.LessThanEqual:
                return queryable.Where(s => s.Metadata.ReleaseYear <= releaseYear);
            case FilterComparison.IsInLast:
                return queryable.Where(s => s.Metadata.ReleaseYear >= DateTime.Now.Year - (int) releaseYear);
            case FilterComparison.IsNotInLast:
                return queryable.Where(s => s.Metadata.ReleaseYear < DateTime.Now.Year - (int) releaseYear);
            case FilterComparison.IsEmpty:
                return queryable.Where(s => s.Metadata.ReleaseYear == 0);
            case FilterComparison.Matches:
            case FilterComparison.Contains:
            case FilterComparison.NotContains:
            case FilterComparison.NotEqual:
            case FilterComparison.BeginsWith:
            case FilterComparison.EndsWith:
            case FilterComparison.MustContains:
                throw new KavitaException($"{comparison} not applicable for Series.ReleaseYear");
            default:
                throw new ArgumentOutOfRangeException(nameof(comparison), comparison, null);
        }
    }


    public static IQueryable<Series> HasRating(this IQueryable<Series> queryable, bool condition,
        FilterComparison comparison, float rating, int userId)
    {
        if (rating < 0 || !condition || userId <= 0) return queryable;

        // AppUserRating stores a 5-digit number.
        rating = Math.Clamp(rating, 0f, 5f);


        switch (comparison)
        {
            case FilterComparison.Equal:
                return queryable.Where(s => s.Ratings.Any(r => Math.Abs(r.Rating - rating) <= FloatingPointTolerance && r.AppUserId == userId));
            case FilterComparison.GreaterThan:
                return queryable.Where(s => s.Ratings.Any(r => r.Rating > rating && r.AppUserId == userId));
            case FilterComparison.GreaterThanEqual:
                return queryable.Where(s => s.Ratings.Any(r => r.Rating >= rating && r.AppUserId == userId));
            case FilterComparison.LessThan:
                return queryable.Where(s => s.Ratings.Any(r => r.Rating < rating && r.AppUserId == userId));
            case FilterComparison.LessThanEqual:
                return queryable.Where(s => s.Ratings.Any(r => r.Rating <= rating && r.AppUserId == userId));
            case FilterComparison.NotEqual:
                return queryable.Where(s => s.Ratings.Any(r => Math.Abs(r.Rating - rating) >= FloatingPointTolerance && r.AppUserId == userId));
            case FilterComparison.IsEmpty:
                return queryable.Where(s => s.Ratings.All(r => r.AppUserId != userId));
            case FilterComparison.Contains:
            case FilterComparison.Matches:
            case FilterComparison.NotContains:
            case FilterComparison.BeginsWith:
            case FilterComparison.EndsWith:
            case FilterComparison.IsBefore:
            case FilterComparison.IsAfter:
            case FilterComparison.IsInLast:
            case FilterComparison.IsNotInLast:
            case FilterComparison.MustContains:
                throw new KavitaException($"{comparison} not applicable for Series.Rating");
            default:
                throw new ArgumentOutOfRangeException(nameof(comparison), comparison, null);
        }
    }

    public static IQueryable<Series> HasAgeRating(this IQueryable<Series> queryable, bool condition,
        FilterComparison comparison, IList<AgeRating> ratings)
    {
        if (!condition || ratings.Count == 0) return queryable;

        var firstRating = ratings[0];
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
            case FilterComparison.MustContains:
            case FilterComparison.IsEmpty:
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
            case FilterComparison.NotEqual:
                return queryable.WhereNotEqual(s => s.AvgHoursToRead, avgReadTime);
            case FilterComparison.Equal:
                return queryable.WhereEqual(s => s.AvgHoursToRead, avgReadTime);
            case FilterComparison.GreaterThan:
                return queryable.WhereGreaterThan(s => s.AvgHoursToRead, avgReadTime);
            case FilterComparison.GreaterThanEqual:
                return queryable.WhereGreaterThanOrEqual(s => s.AvgHoursToRead, avgReadTime);
            case FilterComparison.LessThan:
                return queryable.WhereLessThan(s => s.AvgHoursToRead, avgReadTime);
            case FilterComparison.LessThanEqual:
                return queryable.WhereLessThanOrEqual(s => s.AvgHoursToRead, avgReadTime);
            case FilterComparison.Contains:
            case FilterComparison.Matches:
            case FilterComparison.NotContains:
            case FilterComparison.BeginsWith:
            case FilterComparison.EndsWith:
            case FilterComparison.IsBefore:
            case FilterComparison.IsAfter:
            case FilterComparison.IsInLast:
            case FilterComparison.IsNotInLast:
            case FilterComparison.MustContains:
            case FilterComparison.IsEmpty:
                throw new KavitaException($"{comparison} not applicable for Series.AverageReadTime");
            default:
                throw new ArgumentOutOfRangeException(nameof(comparison), comparison, null);
        }
    }

    public static IQueryable<Series> HasPublicationStatus(this IQueryable<Series> queryable, bool condition,
        FilterComparison comparison, IList<PublicationStatus> pubStatues)
    {
        if (!condition || pubStatues.Count == 0) return queryable;

        var firstStatus = pubStatues[0];
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
            case FilterComparison.MustContains:
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
            case FilterComparison.IsEmpty:
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
        FilterComparison comparison, float readProgress, int userId)
    {
        if (!condition) return queryable;

        var subQuery = queryable
            .Include(s => s.Progress)
            .Where(s => s.Progress != null)
            .Select(s => new
            {
                SeriesId = s.Id,
                SeriesName = s.Name,
                Percentage = s.Progress
                    .Where(p => p != null && p.AppUserId == userId)
                    .Sum(p => p != null ? (p.PagesRead * 1.0f / s.Pages) : 0f) * 100f
            })
            .AsSplitQuery();

        switch (comparison)
        {
            case FilterComparison.Equal:
                subQuery = subQuery.WhereEqual(s => s.Percentage, readProgress);
                break;
            case FilterComparison.GreaterThan:
                subQuery = subQuery.WhereGreaterThan(s => s.Percentage, readProgress);
                break;
            case FilterComparison.GreaterThanEqual:
                subQuery = subQuery.WhereGreaterThanOrEqual(s => s.Percentage, readProgress);
                break;
            case FilterComparison.LessThan:
                subQuery = subQuery.WhereLessThan(s => s.Percentage, readProgress);
                break;
            case FilterComparison.LessThanEqual:
                subQuery = subQuery.WhereLessThanOrEqual(s => s.Percentage, readProgress);
                break;
            case FilterComparison.NotEqual:
                subQuery = subQuery.WhereNotEqual(s => s.Percentage, readProgress);
                break;
            case FilterComparison.IsEmpty:
            case FilterComparison.Matches:
            case FilterComparison.Contains:
            case FilterComparison.NotContains:
            case FilterComparison.BeginsWith:
            case FilterComparison.EndsWith:
            case FilterComparison.IsBefore:
            case FilterComparison.IsAfter:
            case FilterComparison.IsInLast:
            case FilterComparison.IsNotInLast:
            case FilterComparison.MustContains:
                throw new KavitaException($"{comparison} not applicable for Series.ReadProgress");
            default:
                throw new ArgumentOutOfRangeException(nameof(comparison), comparison, null);
        }

        var ids = subQuery.Select(s => s.SeriesId);
        return queryable.Where(s => ids.Contains(s.Id));
    }

    public static IQueryable<Series> HasAverageRating(this IQueryable<Series> queryable, bool condition,
        FilterComparison comparison, float rating)
    {
        if (!condition) return queryable;

        var subQuery = queryable
            .Where(s => s.ExternalSeriesMetadata != null)
            .Include(s => s.ExternalSeriesMetadata)
            .Select(s => new
            {
                SeriesId = s.Id,
                SeriesName = s.Name,
                AverageRating = s.ExternalSeriesMetadata.AverageExternalRating
            })
            .AsSplitQuery()
            .AsQueryable();

        switch (comparison)
        {
            case FilterComparison.Equal:
                subQuery = subQuery.WhereEqual(s => s.AverageRating, rating);
                break;
            case FilterComparison.GreaterThan:
                subQuery = subQuery.WhereGreaterThan(s => s.AverageRating, rating);
                break;
            case FilterComparison.GreaterThanEqual:
                subQuery = subQuery.WhereGreaterThanOrEqual(s => s.AverageRating, rating);
                break;
            case FilterComparison.LessThan:
                subQuery = subQuery.WhereLessThan(s => s.AverageRating, rating);
                break;
            case FilterComparison.LessThanEqual:
                subQuery = subQuery.WhereLessThanOrEqual(s => s.AverageRating, rating);
                break;
            case FilterComparison.NotEqual:
                subQuery = subQuery.WhereNotEqual(s => s.AverageRating, rating);
                break;
            case FilterComparison.Matches:
            case FilterComparison.Contains:
            case FilterComparison.NotContains:
            case FilterComparison.BeginsWith:
            case FilterComparison.EndsWith:
            case FilterComparison.IsBefore:
            case FilterComparison.IsAfter:
            case FilterComparison.IsInLast:
            case FilterComparison.IsNotInLast:
            case FilterComparison.MustContains:
            case FilterComparison.IsEmpty:
                throw new KavitaException($"{comparison} not applicable for Series.AverageRating");
            default:
                throw new ArgumentOutOfRangeException(nameof(comparison), comparison, null);
        }

        var ids = subQuery.Select(s => s.SeriesId);
        return queryable.Where(s => ids.Contains(s.Id));
    }

    /// <summary>
    /// HasReadingDate but used to filter where last reading point was TODAY() - timeDeltaDays. This allows the user
    /// to build smart filters "Haven't read in a month"
    /// </summary>
    public static IQueryable<Series> HasReadLast(this IQueryable<Series> queryable, bool condition,
        FilterComparison comparison, int timeDeltaDays, int userId)
    {
        if (!condition || timeDeltaDays == 0) return queryable;

        var subQuery = queryable
            .Include(s => s.Progress)
            .Where(s => s.Progress != null)
            .Select(s => new
            {
                SeriesId = s.Id,
                SeriesName = s.Name,
                MaxDate = s.Progress.Where(p => p != null && p.AppUserId == userId)
                    .Select(p => (DateTime?) p.LastModified)
                    .DefaultIfEmpty()
                    .Max()
            })
            .Where(s => s.MaxDate != null)
            .AsSplitQuery()
            .AsEnumerable();

        var date = DateTime.Now.AddDays(-timeDeltaDays);

        switch (comparison)
        {
            case FilterComparison.Equal:
                subQuery = subQuery.Where(s => s.MaxDate != null && s.MaxDate.Equals(date));
                break;
            case FilterComparison.IsAfter:
            case FilterComparison.GreaterThan:
                subQuery = subQuery.Where(s => s.MaxDate != null && s.MaxDate > date);
                break;
            case FilterComparison.GreaterThanEqual:
                subQuery = subQuery.Where(s => s.MaxDate != null && s.MaxDate >= date);
                break;
            case FilterComparison.IsBefore:
            case FilterComparison.LessThan:
                subQuery = subQuery.Where(s => s.MaxDate != null && s.MaxDate < date);
                break;
            case FilterComparison.LessThanEqual:
                subQuery = subQuery.Where(s => s.MaxDate != null && s.MaxDate <= date);
                break;
            case FilterComparison.NotEqual:
                subQuery = subQuery.Where(s => s.MaxDate != null && !s.MaxDate.Equals(date));
                break;
            case FilterComparison.Matches:
            case FilterComparison.Contains:
            case FilterComparison.NotContains:
            case FilterComparison.BeginsWith:
            case FilterComparison.EndsWith:
            case FilterComparison.IsInLast:
            case FilterComparison.IsNotInLast:
            case FilterComparison.MustContains:
            case FilterComparison.IsEmpty:
                throw new KavitaException($"{comparison} not applicable for Series.ReadProgress");
            default:
                throw new ArgumentOutOfRangeException(nameof(comparison), comparison, null);
        }

        var ids = subQuery.Select(s => s.SeriesId);
        return queryable.Where(s => ids.Contains(s.Id));
    }

    public static IQueryable<Series> HasReadingDate(this IQueryable<Series> queryable, bool condition,
        FilterComparison comparison, DateTime? date, int userId)
    {
        if (!condition || !date.HasValue) return queryable;

        var subQuery = queryable
            .Include(s => s.Progress)
            .Where(s => s.Progress != null)
            .Select(s => new
            {
                SeriesId = s.Id,
                SeriesName = s.Name,
                MaxDate = s.Progress.Where(p => p != null && p.AppUserId == userId)
                    .Select(p => (DateTime?) p.LastModified)
                    .DefaultIfEmpty()
                    .Max()
            })
            .Where(s => s.MaxDate != null)
            .AsSplitQuery()
            .AsEnumerable();

        switch (comparison)
        {
            case FilterComparison.Equal:
                subQuery = subQuery.Where(s => s.MaxDate != null && s.MaxDate.Equals(date));
                break;
            case FilterComparison.IsAfter:
            case FilterComparison.GreaterThan:
                subQuery = subQuery.Where(s => s.MaxDate != null && s.MaxDate > date);
                break;
            case FilterComparison.GreaterThanEqual:
                subQuery = subQuery.Where(s => s.MaxDate != null && s.MaxDate >= date);
                break;
            case FilterComparison.IsBefore:
            case FilterComparison.LessThan:
                subQuery = subQuery.Where(s => s.MaxDate != null && s.MaxDate < date);
                break;
            case FilterComparison.LessThanEqual:
                subQuery = subQuery.Where(s => s.MaxDate != null && s.MaxDate <= date);
                break;
            case FilterComparison.NotEqual:
                subQuery = subQuery.Where(s => s.MaxDate != null && !s.MaxDate.Equals(date));
                break;
            case FilterComparison.Matches:
            case FilterComparison.Contains:
            case FilterComparison.NotContains:
            case FilterComparison.BeginsWith:
            case FilterComparison.EndsWith:
            case FilterComparison.IsInLast:
            case FilterComparison.IsNotInLast:
            case FilterComparison.MustContains:
            case FilterComparison.IsEmpty:
                throw new KavitaException($"{comparison} not applicable for Series.ReadProgress");
            default:
                throw new ArgumentOutOfRangeException(nameof(comparison), comparison, null);
        }

        var ids = subQuery.Select(s => s.SeriesId);
        return queryable.Where(s => ids.Contains(s.Id));
    }

    public static IQueryable<Series> HasTags(this IQueryable<Series> queryable, bool condition,
        FilterComparison comparison, IList<int> tags)
    {
        if (!condition || (comparison != FilterComparison.IsEmpty && tags.Count == 0)) return queryable;

        switch (comparison)
        {
            case FilterComparison.Equal:
            case FilterComparison.Contains:
                return queryable.Where(s => s.Metadata.Tags.Any(t => tags.Contains(t.Id)));
            case FilterComparison.NotEqual:
            case FilterComparison.NotContains:
                return queryable.Where(s => s.Metadata.Tags.All(t => !tags.Contains(t.Id)));
            case FilterComparison.MustContains:
                // Deconstruct and do a Union of a bunch of where statements since this doesn't translate
                var queries = new List<IQueryable<Series>>()
                {
                    queryable
                };
                queries.AddRange(tags.Select(gId => queryable.Where(s => s.Metadata.Tags.Any(p => p.Id == gId))));

                return queries.Aggregate((q1, q2) => q1.Intersect(q2));
            case FilterComparison.IsEmpty:
                return queryable.Where(s => s.Metadata.Tags == null || s.Metadata.Tags.Count == 0);
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
                throw new KavitaException($"{comparison} not applicable for Series.Tags");
            default:
                throw new ArgumentOutOfRangeException(nameof(comparison), comparison, null);
        }
    }

    public static IQueryable<Series> HasPeople(this IQueryable<Series> queryable, bool condition,
        FilterComparison comparison, IList<int> people, PersonRole role)
    {
        if (!condition || (comparison != FilterComparison.IsEmpty && people.Count == 0)) return queryable;

        switch (comparison)
        {
            case FilterComparison.Equal:
            case FilterComparison.Contains:
                return queryable.Where(s => s.Metadata.People.Any(p => people.Contains(p.PersonId) && p.Role == role));
            case FilterComparison.NotEqual:
            case FilterComparison.NotContains:
                return queryable.Where(s => s.Metadata.People.All(p => !people.Contains(p.PersonId) || p.Role != role));
            case FilterComparison.MustContains:
                var queries = new List<IQueryable<Series>>()
                {
                    queryable
                };
                queries.AddRange(people.Select(personId =>
                    queryable.Where(s => s.Metadata.People.Any(p => p.PersonId == personId && p.Role == role))));

                return queries.Aggregate((q1, q2) => q1.Intersect(q2));
            case FilterComparison.IsEmpty:
                // Ensure no person with the given role exists
                return queryable.Where(s => s.Metadata.People.All(p => p.Role != role));
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
                throw new KavitaException($"{comparison} not applicable for Series.People");
            default:
                throw new ArgumentOutOfRangeException(nameof(comparison), comparison, null);
        }
    }

    public static IQueryable<Series> HasPeopleLegacy(this IQueryable<Series> queryable, bool condition,
        FilterComparison comparison, IList<int> people)
    {
        if (!condition || people.Count == 0) return queryable;

        switch (comparison)
        {
            case FilterComparison.Equal:
            case FilterComparison.Contains:
                return queryable.Where(s => s.Metadata.People.Any(p => people.Contains(p.PersonId)));
            case FilterComparison.NotEqual:
            case FilterComparison.NotContains:
                return queryable.Where(s => s.Metadata.People.All(t => !people.Contains(t.PersonId)));
            case FilterComparison.MustContains:
                // Deconstruct and do a Union of a bunch of where statements since this doesn't translate
                var queries = new List<IQueryable<Series>>()
                {
                    queryable
                };
                queries.AddRange(people.Select(gId => queryable.Where(s => s.Metadata.People.Any(p => p.PersonId == gId))));

                return queries.Aggregate((q1, q2) => q1.Intersect(q2));
            case FilterComparison.IsEmpty:
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
                throw new KavitaException($"{comparison} not applicable for Series.People");
            default:
                throw new ArgumentOutOfRangeException(nameof(comparison), comparison, null);
        }
    }

    public static IQueryable<Series> HasGenre(this IQueryable<Series> queryable, bool condition,
        FilterComparison comparison, IList<int> genres)
    {
        if (!condition || (comparison != FilterComparison.IsEmpty && genres.Count == 0)) return queryable;

        switch (comparison)
        {
            case FilterComparison.Equal:
            case FilterComparison.Contains:
                return queryable.Where(s => s.Metadata.Genres.Any(p => genres.Contains(p.Id)));
            case FilterComparison.NotEqual:
            case FilterComparison.NotContains:
                return queryable.Where(s => s.Metadata.Genres.All(p => !genres.Contains(p.Id)));
            case FilterComparison.MustContains:
                // Deconstruct and do a Union of a bunch of where statements since this doesn't translate
                var queries = new List<IQueryable<Series>>()
                {
                    queryable
                };
                queries.AddRange(genres.Select(gId => queryable.Where(s => s.Metadata.Genres.Any(p => p.Id == gId))));

                return queries.Aggregate((q1, q2) => q1.Intersect(q2));
            case FilterComparison.IsEmpty:
                return queryable.Where(s => s.Metadata.Genres.Count == 0);
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
            case FilterComparison.MustContains:
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
            case FilterComparison.IsEmpty:
                throw new KavitaException($"{comparison} not applicable for Series.Format");
            default:
                throw new ArgumentOutOfRangeException(nameof(comparison), comparison, null);
        }
    }

    public static IQueryable<Series> HasCollectionTags(this IQueryable<Series> queryable, bool condition,
        FilterComparison comparison, IList<int> collectionTags, IList<int> collectionSeries)
    {
        if (!condition || (comparison != FilterComparison.IsEmpty && collectionTags.Count == 0)) return queryable;


        switch (comparison)
        {
            case FilterComparison.Equal:
            case FilterComparison.Contains:
                return queryable.Where(s => collectionSeries.Contains(s.Id));
            case FilterComparison.NotContains:
            case FilterComparison.NotEqual:
                return queryable.Where(s => !collectionSeries.Contains(s.Id));
            case FilterComparison.MustContains:
                // // Deconstruct and do a Union of a bunch of where statements since this doesn't translate
                var queries = new List<IQueryable<Series>>()
                {
                    queryable
                };
                queries.AddRange(collectionSeries.Select(gId => queryable.Where(s => collectionSeries.Any(p => p == s.Id))));

                return queries.Aggregate((q1, q2) => q1.Intersect(q2));
            case FilterComparison.IsEmpty:
                return queryable.Where(s => collectionSeries.All(c => c != s.Id));
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
            case FilterComparison.MustContains:
            case FilterComparison.IsEmpty:
                throw new KavitaException($"{comparison} not applicable for Series.Name");
            default:
                throw new ArgumentOutOfRangeException(nameof(comparison), comparison, "Filter Comparison is not supported");
        }
    }

    public static IQueryable<Series> HasSummary(this IQueryable<Series> queryable, bool condition,
        FilterComparison comparison, string queryString)
    {
        if (!condition) return queryable;

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
            case FilterComparison.IsEmpty:
                return queryable.Where(s => string.IsNullOrEmpty(s.Metadata.Summary));
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
            case FilterComparison.MustContains:
                throw new KavitaException($"{comparison} not applicable for Series.Metadata.Summary");
            default:
                throw new ArgumentOutOfRangeException(nameof(comparison), comparison, "Filter Comparison is not supported");
        }
    }

    public static IQueryable<Series> HasPath(this IQueryable<Series> queryable, bool condition,
        FilterComparison comparison, string queryString)
    {
        if (!condition) return queryable;

        var normalizedPath = Parser.NormalizePath(queryString);

        switch (comparison)
        {
            case FilterComparison.Equal:
                return queryable.Where(s => s.FolderPath != null && s.FolderPath.Equals(normalizedPath));
            case FilterComparison.BeginsWith:
                return queryable.Where(s => s.FolderPath != null && EF.Functions.Like(s.FolderPath, $"{normalizedPath}%"));
            case FilterComparison.EndsWith:
                return queryable.Where(s => s.FolderPath != null && EF.Functions.Like(s.FolderPath, $"%{normalizedPath}"));
            case FilterComparison.Matches:
                return queryable.Where(s => s.FolderPath != null && EF.Functions.Like(s.FolderPath, $"%{normalizedPath}%"));
            case FilterComparison.NotEqual:
                return queryable.Where(s => s.FolderPath != null && s.FolderPath != normalizedPath);
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
            case FilterComparison.MustContains:
            case FilterComparison.IsEmpty:
                throw new KavitaException($"{comparison} not applicable for Series.FolderPath");
            default:
                throw new ArgumentOutOfRangeException(nameof(comparison), comparison, "Filter Comparison is not supported");
        }
    }

    public static IQueryable<Series> HasFilePath(this IQueryable<Series> queryable, bool condition,
        FilterComparison comparison, string queryString)
    {
        if (!condition) return queryable;

        var normalizedPath = Parser.NormalizePath(queryString);

        switch (comparison)
        {
            case FilterComparison.Equal:
                return queryable.Where(s =>
                    s.Volumes.Any(v =>
                        v.Chapters.Any(c =>
                            c.Files.Any(f =>
                                f.FilePath != null && f.FilePath.Equals(normalizedPath)
                            )
                        )
                    )
                );
            case FilterComparison.BeginsWith:
                return queryable.Where(s =>
                    s.Volumes.Any(v =>
                        v.Chapters.Any(c =>
                            c.Files.Any(f =>
                                f.FilePath != null && EF.Functions.Like(f.FilePath, $"{normalizedPath}%")
                            )
                        )
                    )
                );
            case FilterComparison.EndsWith:
                return queryable.Where(s =>
                    s.Volumes.Any(v =>
                        v.Chapters.Any(c =>
                            c.Files.Any(f =>
                                f.FilePath != null && EF.Functions.Like(f.FilePath, $"%{normalizedPath}")
                            )
                        )
                    )
                );
            case FilterComparison.Matches:
                return queryable.Where(s =>
                    s.Volumes.Any(v =>
                        v.Chapters.Any(c =>
                            c.Files.Any(f =>
                                f.FilePath != null && EF.Functions.Like(f.FilePath, $"%{normalizedPath}%")
                            )
                        )
                    )
                );
            case FilterComparison.NotEqual:
                return queryable.Where(s =>
                    s.Volumes.Any(v =>
                        v.Chapters.Any(c =>
                            c.Files.Any(f =>
                                f.FilePath == null || !f.FilePath.Equals(normalizedPath)
                            )
                        )
                    )
                );
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
            case FilterComparison.MustContains:
            case FilterComparison.IsEmpty:
                throw new KavitaException($"{comparison} not applicable for Series.FolderPath");
            default:
                throw new ArgumentOutOfRangeException(nameof(comparison), comparison, "Filter Comparison is not supported");
        }
    }


}
