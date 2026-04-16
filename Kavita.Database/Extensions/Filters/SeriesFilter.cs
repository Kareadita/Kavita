using System;
using System.Collections.Generic;
using System.Linq;
using Kavita.Common;
using Kavita.Common.Extensions;
using Kavita.Models.DTOs.Filtering.v2;
using Kavita.Models.Entities;
using Kavita.Models.Entities.Enums;
using Microsoft.EntityFrameworkCore;

namespace Kavita.Database.Extensions.Filters;

public static class SeriesFilter
{
    private const float FloatingPointTolerance = 0.001f;

    private static readonly HashSet<FilterComparison> ListWithMatches =
    [
        FilterComparison.Equal, FilterComparison.NotEqual,
        FilterComparison.Contains, FilterComparison.NotContains, FilterComparison.MustContains,
        FilterComparison.Matches
    ];

    private static readonly HashSet<FilterComparison> NumericWithEmpty =
    [
        FilterComparison.Equal, FilterComparison.NotEqual,
        FilterComparison.GreaterThan, FilterComparison.GreaterThanEqual,
        FilterComparison.LessThan, FilterComparison.LessThanEqual,
        FilterComparison.IsEmpty, FilterComparison.IsNotEmpty
    ];

    private static readonly HashSet<FilterComparison> NumericWithList =
    [
        FilterComparison.Equal, FilterComparison.NotEqual,
        FilterComparison.GreaterThan, FilterComparison.GreaterThanEqual,
        FilterComparison.LessThan, FilterComparison.LessThanEqual,
        FilterComparison.Contains, FilterComparison.NotContains
    ];

    private static readonly HashSet<FilterComparison> ListBasic =
    [
        FilterComparison.Equal, FilterComparison.NotEqual,
        FilterComparison.Contains, FilterComparison.NotContains
    ];

    private static readonly HashSet<FilterComparison> NumericWithBeforeAfter =
    [
        FilterComparison.Equal, FilterComparison.NotEqual,
        FilterComparison.GreaterThan, FilterComparison.GreaterThanEqual,
        FilterComparison.LessThan, FilterComparison.LessThanEqual,
        FilterComparison.IsAfter, FilterComparison.IsBefore
    ];

    private static readonly HashSet<FilterComparison> StringWithEmpty =
    [
        FilterComparison.Equal, FilterComparison.NotEqual,
        FilterComparison.BeginsWith, FilterComparison.EndsWith,
        FilterComparison.Matches, FilterComparison.IsEmpty,
        FilterComparison.IsNotEmpty
    ];

    private static readonly HashSet<FilterComparison> NumericNoNotEqual =
    [
        FilterComparison.Equal,
        FilterComparison.GreaterThan, FilterComparison.GreaterThanEqual,
        FilterComparison.LessThan, FilterComparison.LessThanEqual
    ];

    extension(IQueryable<Series> queryable)
    {
        public IQueryable<Series> HasLanguage(bool condition, FilterComparison comparison, IList<string> languages)
        {
            if (languages.Count == 0 || !condition) return queryable;
            ComparisonProfile.Validate(comparison, ListWithMatches, "Series.Language");

            return comparison switch
            {
                FilterComparison.Equal => queryable.Where(s => s.Metadata.Language.Equals(languages[0])),
                FilterComparison.Contains => queryable.Where(s => languages.Contains(s.Metadata.Language)),
                FilterComparison.MustContains => queryable.Where(s =>
                    languages.All(s2 => s2.Equals(s.Metadata.Language))),
                FilterComparison.NotContains => queryable.Where(s => !languages.Contains(s.Metadata.Language)),
                FilterComparison.NotEqual => queryable.Where(s => !s.Metadata.Language.Equals(languages[0])),
                FilterComparison.Matches => queryable.Where(s =>
                    EF.Functions.Like(s.Metadata.Language, $"{languages[0]}%")),
                _ => throw new ArgumentOutOfRangeException(nameof(comparison), comparison, null)
            };
        }

        public IQueryable<Series> HasReleaseYear(bool condition, FilterComparison comparison, int? releaseYear)
        {
            if (!condition || releaseYear == null) return queryable;
            ComparisonProfile.Validate(comparison, ComparisonProfile.Date, "Series.ReleaseYear");

            return comparison switch
            {
                FilterComparison.Equal => queryable.Where(s => s.Metadata.ReleaseYear == releaseYear),
                FilterComparison.NotEqual => queryable.Where(s => s.Metadata.ReleaseYear != releaseYear),
                FilterComparison.GreaterThan or FilterComparison.IsAfter => queryable.Where(s =>
                    s.Metadata.ReleaseYear > releaseYear),
                FilterComparison.GreaterThanEqual => queryable.Where(s => s.Metadata.ReleaseYear >= releaseYear),
                FilterComparison.LessThan or FilterComparison.IsBefore => queryable.Where(s =>
                    s.Metadata.ReleaseYear < releaseYear),
                FilterComparison.LessThanEqual => queryable.Where(s => s.Metadata.ReleaseYear <= releaseYear),
                FilterComparison.IsInLast => queryable.Where(s =>
                    s.Metadata.ReleaseYear >= DateTime.Now.Year - (int) releaseYear),
                FilterComparison.IsNotInLast => queryable.Where(s =>
                    s.Metadata.ReleaseYear < DateTime.Now.Year - (int) releaseYear),
                FilterComparison.IsEmpty => queryable.Where(s => s.Metadata.ReleaseYear == 0),
                FilterComparison.IsNotEmpty => queryable.Where(s => s.Metadata.ReleaseYear != 0),
                _ => throw new ArgumentOutOfRangeException(nameof(comparison), comparison, null)
            };
        }

        public IQueryable<Series> HasRating(bool condition, FilterComparison comparison, float rating, int userId)
        {
            if (rating < 0 || !condition || userId <= 0) return queryable;
            ComparisonProfile.Validate(comparison, NumericWithEmpty, "Series.Rating");

            // AppUserRating stores a 5-digit number.
            rating = Math.Clamp(rating, 0f, 5f);

            return comparison switch
            {
                FilterComparison.Equal => queryable.Where(s =>
                    s.Ratings.Any(r => Math.Abs(r.Rating - rating) <= FloatingPointTolerance && r.AppUserId == userId)),
                FilterComparison.GreaterThan => queryable.Where(s =>
                    s.Ratings.Any(r => r.Rating > rating && r.AppUserId == userId)),
                FilterComparison.GreaterThanEqual => queryable.Where(s =>
                    s.Ratings.Any(r => r.Rating >= rating && r.AppUserId == userId)),
                FilterComparison.LessThan => queryable.Where(s =>
                    s.Ratings.Any(r => r.Rating < rating && r.AppUserId == userId)),
                FilterComparison.LessThanEqual => queryable.Where(s =>
                    s.Ratings.Any(r => r.Rating <= rating && r.AppUserId == userId)),
                FilterComparison.NotEqual => queryable.Where(s =>
                    s.Ratings.Any(r => Math.Abs(r.Rating - rating) >= FloatingPointTolerance && r.AppUserId == userId)),
                FilterComparison.IsEmpty => queryable.Where(s => s.Ratings.All(r => r.AppUserId != userId)),
                FilterComparison.IsNotEmpty => queryable.Where(s => s.Ratings.Any(r => r.AppUserId == userId)),
                _ => throw new ArgumentOutOfRangeException(nameof(comparison), comparison, null)
            };
        }

        public IQueryable<Series> HasAgeRating(bool condition,
            FilterComparison comparison, IList<AgeRating> ratings)
        {
            if (!condition || ratings.Count == 0) return queryable;
            ComparisonProfile.Validate(comparison, NumericWithList, "Series.AgeRating");

            var firstRating = ratings[0];
            return comparison switch
            {
                FilterComparison.Equal => queryable.Where(s => s.Metadata.AgeRating == firstRating),
                FilterComparison.GreaterThan => queryable.Where(s => s.Metadata.AgeRating > firstRating),
                FilterComparison.GreaterThanEqual => queryable.Where(s => s.Metadata.AgeRating >= firstRating),
                FilterComparison.LessThan => queryable.Where(s => s.Metadata.AgeRating < firstRating),
                FilterComparison.LessThanEqual => queryable.Where(s => s.Metadata.AgeRating <= firstRating),
                FilterComparison.Contains => queryable.Where(s => ratings.Contains(s.Metadata.AgeRating)),
                FilterComparison.NotContains => queryable.Where(s => !ratings.Contains(s.Metadata.AgeRating)),
                FilterComparison.NotEqual => queryable.Where(s => s.Metadata.AgeRating != firstRating),
                _ => throw new ArgumentOutOfRangeException(nameof(comparison), comparison, null)
            };
        }

        public IQueryable<Series> HasAverageReadTime(bool condition, FilterComparison comparison, int avgReadTime)
        {
            if (!condition || avgReadTime < 0) return queryable;
            ComparisonProfile.Validate(comparison, ComparisonProfile.Numeric, "Series.AverageReadTime");

            return comparison switch
            {
                FilterComparison.NotEqual => queryable.WhereNotEqual(s => s.AvgHoursToRead, avgReadTime),
                FilterComparison.Equal => queryable.WhereEqual(s => s.AvgHoursToRead, avgReadTime),
                FilterComparison.GreaterThan => queryable.WhereGreaterThan(s => s.AvgHoursToRead, avgReadTime),
                FilterComparison.GreaterThanEqual => queryable.WhereGreaterThanOrEqual(s => s.AvgHoursToRead,
                    avgReadTime),
                FilterComparison.LessThan => queryable.WhereLessThan(s => s.AvgHoursToRead, avgReadTime),
                FilterComparison.LessThanEqual => queryable.WhereLessThanOrEqual(s => s.AvgHoursToRead, avgReadTime),
                _ => throw new ArgumentOutOfRangeException(nameof(comparison), comparison, null)
            };
        }

        public IQueryable<Series> HasPublicationStatus(bool condition,
            FilterComparison comparison, IList<PublicationStatus> pubStatues)
        {
            if (!condition || pubStatues.Count == 0) return queryable;
            ComparisonProfile.Validate(comparison, ListBasic, "Series.PublicationStatus");

            var firstStatus = pubStatues[0];
            return comparison switch
            {
                FilterComparison.Equal => queryable.Where(s => s.Metadata.PublicationStatus == firstStatus),
                FilterComparison.Contains => queryable.Where(s => pubStatues.Contains(s.Metadata.PublicationStatus)),
                FilterComparison.NotContains =>
                    queryable.Where(s => !pubStatues.Contains(s.Metadata.PublicationStatus)),
                FilterComparison.NotEqual => queryable.Where(s => s.Metadata.PublicationStatus != firstStatus),
                _ => throw new ArgumentOutOfRangeException(nameof(comparison), comparison, null)
            };
        }

        /// <summary>
        ///
        /// </summary>
        /// <remarks>This is more taxing on memory as the percentage calculation must be done in Memory</remarks>
        /// <exception cref="KavitaException"></exception>
        /// <exception cref="ArgumentOutOfRangeException"></exception>
        public IQueryable<Series> HasReadingProgress(bool condition, FilterComparison comparison, float readProgress, int userId)
        {
            if (!condition) return queryable;
            ComparisonProfile.Validate(comparison, ComparisonProfile.Numeric, "Series.ReadProgress");

            var subQuery = queryable
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
                default:
                    throw new ArgumentOutOfRangeException(nameof(comparison), comparison, null);
            }

            var ids = subQuery.Select(s => s.SeriesId);
            return queryable.Where(s => ids.Contains(s.Id));
        }

        public IQueryable<Series> HasAverageRating(bool condition, FilterComparison comparison, float rating)
        {
            if (!condition) return queryable;
            ComparisonProfile.Validate(comparison, ComparisonProfile.Numeric, "Series.AverageRating");

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
        public IQueryable<Series> HasReadLast(bool condition, FilterComparison comparison, int timeDeltaDays, int userId)
        {
            if (!condition || timeDeltaDays == 0) return queryable;
            ComparisonProfile.Validate(comparison, NumericWithBeforeAfter, "Series.ReadLast");

            var subQuery = queryable
                .Include(s => s.Progress)
                .Where(s => s.Progress.Any())
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
                default:
                    throw new ArgumentOutOfRangeException(nameof(comparison), comparison, null);
            }

            var ids = subQuery.Select(s => s.SeriesId);
            return queryable.Where(s => ids.Contains(s.Id));
        }

        public IQueryable<Series> HasReadingDate(bool condition, FilterComparison comparison, DateTime? date, int userId)
        {
            if (!condition || !date.HasValue) return queryable;
            ComparisonProfile.Validate(comparison, NumericWithBeforeAfter, "Series.ReadingDate");

            var subQuery = queryable
                .Include(s => s.Progress)
                .Where(s => s.Progress.Any())
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
                default:
                    throw new ArgumentOutOfRangeException(nameof(comparison), comparison, null);
            }

            var ids = subQuery.Select(s => s.SeriesId);
            return queryable.Where(s => ids.Contains(s.Id));
        }

        public IQueryable<Series> HasTags(bool condition, FilterComparison comparison, IList<int> tags)
        {
            if (!condition || (comparison != FilterComparison.IsEmpty && comparison != FilterComparison.IsNotEmpty && tags.Count == 0)) return queryable;
            ComparisonProfile.Validate(comparison, ComparisonProfile.ListWithEmpty, "Series.Tags");

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
                    return queryable.Where(s => s.Metadata.Tags.Count == 0);
                case FilterComparison.IsNotEmpty:
                    return queryable.Where(s => s.Metadata.Tags.Count > 0);
                default:
                    throw new ArgumentOutOfRangeException(nameof(comparison), comparison, null);
            }
        }

        public IQueryable<Series> HasPeople(bool condition, FilterComparison comparison, IList<int> people, PersonRole role)
        {
            if (!condition || (comparison != FilterComparison.IsEmpty && comparison != FilterComparison.IsNotEmpty && people.Count == 0)) return queryable;
            ComparisonProfile.Validate(comparison, ComparisonProfile.ListWithEmpty, "Series.People");

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
                case FilterComparison.IsNotEmpty:
                    return queryable.Where(s => s.Metadata.People.Any(p => p.Role == role));
                default:
                    throw new ArgumentOutOfRangeException(nameof(comparison), comparison, null);
            }
        }

        public IQueryable<Series> HasPeopleLegacy(bool condition, FilterComparison comparison, IList<int> people)
        {
            if (!condition || people.Count == 0) return queryable;
            ComparisonProfile.Validate(comparison, ComparisonProfile.List, "Series.People");

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
                default:
                    throw new ArgumentOutOfRangeException(nameof(comparison), comparison, null);
            }
        }

        public IQueryable<Series> HasGenre(bool condition, FilterComparison comparison, IList<int> genres)
        {
            if (!condition || (comparison != FilterComparison.IsEmpty && comparison != FilterComparison.IsNotEmpty && genres.Count == 0)) return queryable;
            ComparisonProfile.Validate(comparison, ComparisonProfile.ListWithEmpty, "Series.Genres");

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
                case FilterComparison.IsNotEmpty:
                    return queryable.Where(s => s.Metadata.Genres.Count > 0);
                default:
                    throw new ArgumentOutOfRangeException(nameof(comparison), comparison, null);
            }
        }

        public IQueryable<Series> HasFormat(bool condition, FilterComparison comparison, IList<MangaFormat> formats)
        {
            if (!condition || formats.Count == 0) return queryable;
            ComparisonProfile.Validate(comparison, ListBasic, "Series.Format");

            switch (comparison)
            {
                case FilterComparison.Equal:
                case FilterComparison.Contains:
                    return queryable.Where(s => formats.Contains(s.Format));
                case FilterComparison.NotContains:
                case FilterComparison.NotEqual:
                    return queryable.Where(s => !formats.Contains(s.Format));
                default:
                    throw new ArgumentOutOfRangeException(nameof(comparison), comparison, null);
            }
        }

        public IQueryable<Series> HasCollectionTags(bool condition, FilterComparison comparison, IList<int> collectionTags, IList<int> collectionSeries)
        {
            if (!condition || (comparison != FilterComparison.IsEmpty && comparison != FilterComparison.IsNotEmpty && collectionTags.Count == 0)) return queryable;
            ComparisonProfile.Validate(comparison, ComparisonProfile.ListWithEmpty, "Series.CollectionTags");

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
                    return queryable.Where(s => s.Collections.Count == 0);
                case FilterComparison.IsNotEmpty:
                    return queryable.Where(s => s.Collections.Count > 0);
                default:
                    throw new ArgumentOutOfRangeException(nameof(comparison), comparison, null);
            }
        }

        public IQueryable<Series> HasName(bool condition, FilterComparison comparison, string queryString)
        {
            if (string.IsNullOrEmpty(queryString) || !condition) return queryable;
            ComparisonProfile.Validate(comparison, ComparisonProfile.String, "Series.Name");

            return comparison switch
            {
                FilterComparison.Equal => queryable.Where(s =>
                    s.Name.Equals(queryString) || s.OriginalName.Equals(queryString) ||
                    s.LocalizedName.Equals(queryString) || s.SortName.Equals(queryString)),
                FilterComparison.BeginsWith => queryable.Where(s =>
                    EF.Functions.Like(s.Name, $"{queryString}%") ||
                    EF.Functions.Like(s.OriginalName, $"{queryString}%") ||
                    EF.Functions.Like(s.LocalizedName, $"{queryString}%") ||
                    EF.Functions.Like(s.SortName, $"{queryString}%")),
                FilterComparison.EndsWith => queryable.Where(s =>
                    EF.Functions.Like(s.Name, $"%{queryString}") ||
                    EF.Functions.Like(s.OriginalName, $"%{queryString}") ||
                    EF.Functions.Like(s.LocalizedName, $"%{queryString}") ||
                    EF.Functions.Like(s.SortName, $"%{queryString}")),
                FilterComparison.Matches => queryable.Where(s =>
                    EF.Functions.Like(s.Name, $"%{queryString}%") ||
                    EF.Functions.Like(s.OriginalName, $"%{queryString}%") ||
                    EF.Functions.Like(s.LocalizedName, $"%{queryString}%") ||
                    EF.Functions.Like(s.SortName, $"%{queryString}%")),
                FilterComparison.NotEqual => queryable.Where(s =>
                    s.Name != queryString || s.OriginalName != queryString || s.LocalizedName != queryString ||
                    s.SortName != queryString),
                _ => throw new ArgumentOutOfRangeException(nameof(comparison), comparison,
                    "Filter Comparison is not supported")
            };
        }

        public IQueryable<Series> HasSummary(bool condition, FilterComparison comparison, string queryString)
        {
            if (!condition) return queryable;
            ComparisonProfile.Validate(comparison, StringWithEmpty, "Series.Summary");

            return comparison switch
            {
                FilterComparison.Equal => queryable.Where(s => s.Metadata.Summary.Equals(queryString)),
                FilterComparison.BeginsWith => queryable.Where(s =>
                    EF.Functions.Like(s.Metadata.Summary, $"{queryString}%")),
                FilterComparison.EndsWith => queryable.Where(s =>
                    EF.Functions.Like(s.Metadata.Summary, $"%{queryString}")),
                FilterComparison.Matches => queryable.Where(s =>
                    EF.Functions.Like(s.Metadata.Summary, $"%{queryString}%")),
                FilterComparison.NotEqual => queryable.Where(s => s.Metadata.Summary != queryString),
                FilterComparison.IsEmpty => queryable.Where(s => string.IsNullOrEmpty(s.Metadata.Summary)),
                FilterComparison.IsNotEmpty => queryable.Where(s => !string.IsNullOrEmpty(s.Metadata.Summary)),
                _ => throw new ArgumentOutOfRangeException(nameof(comparison), comparison,
                    "Filter Comparison is not supported")
            };
        }

        public IQueryable<Series> HasPath(bool condition, FilterComparison comparison, string queryString)
        {
            if (!condition) return queryable;
            ComparisonProfile.Validate(comparison, ComparisonProfile.String, "Series.FolderPath");

            var normalizedPath = queryString.NormalizePath();

            return comparison switch
            {
                FilterComparison.Equal => queryable.Where(s =>
                    s.FolderPath != null && s.FolderPath.Equals(normalizedPath)),
                FilterComparison.BeginsWith => queryable.Where(s =>
                    s.FolderPath != null && EF.Functions.Like(s.FolderPath, $"{normalizedPath}%")),
                FilterComparison.EndsWith => queryable.Where(s =>
                    s.FolderPath != null && EF.Functions.Like(s.FolderPath, $"%{normalizedPath}")),
                FilterComparison.Matches => queryable.Where(s =>
                    s.FolderPath != null && EF.Functions.Like(s.FolderPath, $"%{normalizedPath}%")),
                FilterComparison.NotEqual =>
                    queryable.Where(s => s.FolderPath != null && s.FolderPath != normalizedPath),
                _ => throw new ArgumentOutOfRangeException(nameof(comparison), comparison,
                    "Filter Comparison is not supported")
            };
        }

        public IQueryable<Series> HasFilePath(bool condition, FilterComparison comparison, string queryString)
        {
            if (!condition) return queryable;
            ComparisonProfile.Validate(comparison, ComparisonProfile.String, "Series.FilePath");

            var normalizedPath = queryString.NormalizePath();

            return comparison switch
            {
                FilterComparison.Equal => queryable.Where(s => s.Volumes.Any(v =>
                    v.Chapters.Any(c => c.Files.Any(f => f.FilePath != null && f.FilePath.Equals(normalizedPath))))),
                FilterComparison.BeginsWith => queryable.Where(s => s.Volumes.Any(v =>
                    v.Chapters.Any(c =>
                        c.Files.Any(f => f.FilePath != null && EF.Functions.Like(f.FilePath, $"{normalizedPath}%"))))),
                FilterComparison.EndsWith => queryable.Where(s => s.Volumes.Any(v =>
                    v.Chapters.Any(c =>
                        c.Files.Any(f => f.FilePath != null && EF.Functions.Like(f.FilePath, $"%{normalizedPath}"))))),
                FilterComparison.Matches => queryable.Where(s => s.Volumes.Any(v =>
                    v.Chapters.Any(c =>
                        c.Files.Any(f => f.FilePath != null && EF.Functions.Like(f.FilePath, $"%{normalizedPath}%"))))),
                FilterComparison.NotEqual => queryable.Where(s => s.Volumes.Any(v =>
                    v.Chapters.Any(c => c.Files.Any(f => f.FilePath == null || !f.FilePath.Equals(normalizedPath))))),
                _ => throw new ArgumentOutOfRangeException(nameof(comparison), comparison,
                    "Filter Comparison is not supported")
            };
        }

        public IQueryable<Series> HasFileSize(bool condition, FilterComparison comparison, float fileSize)
        {
            if (fileSize == 0f || !condition) return queryable;
            ComparisonProfile.Validate(comparison, NumericNoNotEqual, "Series.FileSize");

            return comparison switch
            {
                FilterComparison.Equal => queryable.Where(s => Math.Abs(s.Volumes.Sum(v => v.Chapters.Sum(c => c.Files.Sum(f => f.Bytes))) - fileSize) < FloatingPointTolerance),
                FilterComparison.LessThan => queryable.Where(s => s.Volumes.Sum(v => v.Chapters.Sum(c => c.Files.Sum(f => f.Bytes))) < fileSize),
                FilterComparison.LessThanEqual => queryable.Where(s => s.Volumes.Sum(v => v.Chapters.Sum(c => c.Files.Sum(f => f.Bytes))) <= fileSize),
                FilterComparison.GreaterThan => queryable.Where(s => s.Volumes.Sum(v => v.Chapters.Sum(c => c.Files.Sum(f => f.Bytes))) > fileSize),
                FilterComparison.GreaterThanEqual => queryable.Where(s => s.Volumes.Sum(v => v.Chapters.Sum(c => c.Files.Sum(f => f.Bytes))) >= fileSize),
                _ => throw new ArgumentOutOfRangeException(nameof(comparison), comparison, "Filter Comparison is not supported"),
            };
        }
    }
}
