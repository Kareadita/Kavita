using System;
using System.Collections.Generic;
using System.Linq;
using Kavita.Models.DTOs.Filtering.v2;
using Kavita.Models.Entities.Enums;
using Kavita.Models.Entities.Person;
using Microsoft.EntityFrameworkCore;

namespace Kavita.Database.Extensions.Filters;

public static class PersonFilter
{
    extension(IQueryable<Person> queryable)
    {
        public IQueryable<Person> HasPersonName(bool condition, FilterComparison comparison, string queryString)
        {
            if (string.IsNullOrEmpty(queryString) || !condition) return queryable;
            ComparisonProfile.Validate(comparison, ComparisonProfile.String, "Person.Name");

            return comparison switch
            {
                FilterComparison.Equal => queryable.Where(p => p.Name.Equals(queryString)),
                FilterComparison.NotEqual => queryable.Where(p => p.Name != queryString),
                FilterComparison.BeginsWith => queryable.Where(p => EF.Functions.Like(p.Name, $"{queryString}%")),
                FilterComparison.EndsWith => queryable.Where(p => EF.Functions.Like(p.Name, $"%{queryString}")),
                FilterComparison.Matches => queryable.Where(p => EF.Functions.Like(p.Name, $"%{queryString}%")),
                _ => throw new ArgumentOutOfRangeException(nameof(comparison), comparison, "Filter Comparison is not supported")
            };
        }

        public IQueryable<Person> HasPersonRole(bool condition, FilterComparison comparison, IList<PersonRole> roles)
        {
            if (roles is {Count: 0}|| !condition) return queryable;
            ComparisonProfile.Validate(comparison, ComparisonProfile.List, "Person.Role");

            return comparison switch
            {
                FilterComparison.Equal => queryable.Where(p =>
                    p.SeriesMetadataPeople.Any(smp => roles.Contains(smp.Role)) ||
                    p.ChapterPeople.Any(cmp => roles.Contains(cmp.Role))),
                FilterComparison.NotEqual => queryable.Where(p =>
                    !p.SeriesMetadataPeople.Any(smp => roles.Contains(smp.Role)) &&
                    !p.ChapterPeople.Any(cmp => roles.Contains(cmp.Role))),
                FilterComparison.Contains => queryable.Where(p =>
                    p.SeriesMetadataPeople.Any(smp => roles.Contains(smp.Role)) ||
                    p.ChapterPeople.Any(cmp => roles.Contains(cmp.Role))),
                FilterComparison.MustContains => MustContainAllRoles(queryable, roles),
                FilterComparison.NotContains => queryable.Where(p =>
                    !p.SeriesMetadataPeople.Any(smp => roles.Contains(smp.Role)) &&
                    !p.ChapterPeople.Any(cmp => roles.Contains(cmp.Role))),
                _ => throw new ArgumentOutOfRangeException(nameof(comparison), comparison, "Filter Comparison is not supported")
            };
        }

        public IQueryable<Person> HasPersonSeriesCount(bool condition, FilterComparison comparison, int count)
        {
            if (!condition) return queryable;
            ComparisonProfile.Validate(comparison, ComparisonProfile.Numeric, "Person.SeriesCount");

            return comparison switch
            {
                FilterComparison.Equal => queryable.Where(p => p.SeriesMetadataPeople
                    .Select(smp => smp.SeriesMetadata.SeriesId)
                    .Distinct()
                    .Count() == count),
                FilterComparison.GreaterThan => queryable.Where(p => p.SeriesMetadataPeople
                    .Select(smp => smp.SeriesMetadata.SeriesId)
                    .Distinct()
                    .Count() > count),
                FilterComparison.GreaterThanEqual => queryable.Where(p => p.SeriesMetadataPeople
                    .Select(smp => smp.SeriesMetadata.SeriesId)
                    .Distinct()
                    .Count() >= count),
                FilterComparison.LessThan => queryable.Where(p => p.SeriesMetadataPeople
                    .Select(smp => smp.SeriesMetadata.SeriesId)
                    .Distinct()
                    .Count() < count),
                FilterComparison.LessThanEqual => queryable.Where(p => p.SeriesMetadataPeople
                    .Select(smp => smp.SeriesMetadata.SeriesId)
                    .Distinct()
                    .Count() <= count),
                FilterComparison.NotEqual => queryable.Where(p => p.SeriesMetadataPeople
                    .Select(smp => smp.SeriesMetadata.SeriesId)
                    .Distinct()
                    .Count() != count),
                _ => throw new ArgumentOutOfRangeException(nameof(comparison), comparison, "Filter Comparison is not supported")
            };
        }

        public IQueryable<Person> HasPersonChapterCount(bool condition, FilterComparison comparison, int count)
        {
            if (!condition) return queryable;
            ComparisonProfile.Validate(comparison, ComparisonProfile.Numeric, "Person.ChapterCount");

            return comparison switch
            {
                FilterComparison.Equal => queryable.Where(p =>
                    p.ChapterPeople.Select(cp => cp.Chapter.Id).Distinct().Count() == count),
                FilterComparison.GreaterThan => queryable.Where(p => p.ChapterPeople
                    .Select(cp => cp.Chapter.Id)
                    .Distinct()
                    .Count() > count),
                FilterComparison.GreaterThanEqual => queryable.Where(p => p.ChapterPeople
                    .Select(cp => cp.Chapter.Id)
                    .Distinct()
                    .Count() >= count),
                FilterComparison.LessThan => queryable.Where(p =>
                    p.ChapterPeople.Select(cp => cp.Chapter.Id).Distinct().Count() < count),
                FilterComparison.LessThanEqual => queryable.Where(p => p.ChapterPeople
                    .Select(cp => cp.Chapter.Id)
                    .Distinct()
                    .Count() <= count),
                FilterComparison.NotEqual => queryable.Where(p =>
                    p.ChapterPeople.Select(cp => cp.Chapter.Id).Distinct().Count() != count),
                _ => throw new ArgumentOutOfRangeException(nameof(comparison), comparison, "Filter Comparison is not supported")
            };
        }

        public IQueryable<Person> HasChaptersInLibrary(bool condition, FilterComparison comparison, IList<int> libraryIds)
        {
            if (libraryIds.Count == 0 || !condition) return queryable;
            ComparisonProfile.Validate(comparison, ComparisonProfile.List, "Person.Library");

            return comparison switch
            {
                FilterComparison.Equal => queryable.Where(a => a.ChapterPeople.Any(cp => cp.Chapter.Volume.Series.LibraryId == libraryIds[0])),
                FilterComparison.Contains => queryable.Where(a => a.ChapterPeople.Any(cp => libraryIds.Contains(cp.Chapter.Volume.Series.LibraryId))),
                FilterComparison.MustContains => queryable.Where(a => a.ChapterPeople.All(cp => libraryIds.Contains(cp.Chapter.Volume.Series.LibraryId))),
                FilterComparison.NotContains => queryable.Where(a => a.ChapterPeople.All(cp => !libraryIds.Contains(cp.Chapter.Volume.Series.LibraryId))),
                FilterComparison.NotEqual => queryable.Where(a => a.ChapterPeople.All(cp => cp.Chapter.Volume.Series.LibraryId != libraryIds[0])),
                _ => throw new ArgumentOutOfRangeException(nameof(comparison), comparison, "Filter Comparison is not supported")
            };
        }
    }

    private static IQueryable<Person> MustContainAllRoles(IQueryable<Person> queryable, IList<PersonRole> roles)
    {
        var queries = new List<IQueryable<Person>> { queryable };
        queries.AddRange(roles.Select(role => queryable.Where(p =>
            p.SeriesMetadataPeople.Any(smp => smp.Role == role) ||
            p.ChapterPeople.Any(cmp => cmp.Role == role))));

        return queries.Aggregate((q1, q2) => q1.Intersect(q2));
    }
}
