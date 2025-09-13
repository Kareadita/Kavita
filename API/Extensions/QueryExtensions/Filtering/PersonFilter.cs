using System;
using System.Collections.Generic;
using System.Linq;
using API.DTOs.Filtering.v2;
using API.Entities.Enums;
using API.Entities.Person;
using Kavita.Common;
using Microsoft.EntityFrameworkCore;

namespace API.Extensions.QueryExtensions.Filtering;

public static class PersonFilter
{
    public static IQueryable<Person> HasPersonName(this IQueryable<Person> queryable, bool condition,
        FilterComparison comparison, string queryString)
    {
        if (string.IsNullOrEmpty(queryString) || !condition) return queryable;

        return comparison switch
        {
            FilterComparison.Equal => queryable.Where(p => p.Name.Equals(queryString)),
            FilterComparison.BeginsWith => queryable.Where(p => EF.Functions.Like(p.Name, $"{queryString}%")),
            FilterComparison.EndsWith => queryable.Where(p => EF.Functions.Like(p.Name, $"%{queryString}")),
            FilterComparison.Matches => queryable.Where(p => EF.Functions.Like(p.Name, $"%{queryString}%")),
            FilterComparison.NotEqual => queryable.Where(p => p.Name != queryString),
            FilterComparison.NotContains or FilterComparison.GreaterThan or FilterComparison.GreaterThanEqual
                or FilterComparison.LessThan or FilterComparison.LessThanEqual or FilterComparison.Contains
                or FilterComparison.IsBefore or FilterComparison.IsAfter or FilterComparison.IsInLast
                or FilterComparison.IsNotInLast or FilterComparison.MustContains
                or FilterComparison.IsEmpty =>
                throw new KavitaException($"{comparison} not applicable for Person.Name"),
            _ => throw new ArgumentOutOfRangeException(nameof(comparison), comparison,
                "Filter Comparison is not supported")
        };
    }
    public static IQueryable<Person> HasPersonRole(this IQueryable<Person> queryable, bool condition,
    FilterComparison comparison, IList<PersonRole> roles)
    {
        if (roles == null || roles.Count == 0 || !condition) return queryable;

        return comparison switch
        {
            FilterComparison.Contains or FilterComparison.MustContains => queryable.Where(p =>
                p.SeriesMetadataPeople.Any(smp => roles.Contains(smp.Role)) ||
                p.ChapterPeople.Any(cmp => roles.Contains(cmp.Role))),
            FilterComparison.NotContains => queryable.Where(p =>
                !p.SeriesMetadataPeople.Any(smp => roles.Contains(smp.Role)) &&
                !p.ChapterPeople.Any(cmp => roles.Contains(cmp.Role))),
            FilterComparison.Equal or FilterComparison.NotEqual or FilterComparison.BeginsWith
                or FilterComparison.EndsWith or FilterComparison.Matches or FilterComparison.GreaterThan
                or FilterComparison.GreaterThanEqual or FilterComparison.LessThan or FilterComparison.LessThanEqual
                or FilterComparison.IsBefore or FilterComparison.IsAfter or FilterComparison.IsInLast
                or FilterComparison.IsNotInLast
                or FilterComparison.IsEmpty =>
                throw new KavitaException($"{comparison} not applicable for Person.Role"),
            _ => throw new ArgumentOutOfRangeException(nameof(comparison), comparison,
                "Filter Comparison is not supported")
        };
    }

    public static IQueryable<Person> HasPersonSeriesCount(this IQueryable<Person> queryable, bool condition,
        FilterComparison comparison, int count)
    {
        if (!condition) return queryable;

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
            FilterComparison.BeginsWith or FilterComparison.EndsWith or FilterComparison.Matches
                or FilterComparison.Contains or FilterComparison.NotContains or FilterComparison.IsBefore
                or FilterComparison.IsAfter or FilterComparison.IsInLast or FilterComparison.IsNotInLast
                or FilterComparison.MustContains
                or FilterComparison.IsEmpty => throw new KavitaException(
                    $"{comparison} not applicable for Person.SeriesCount"),
            _ => throw new ArgumentOutOfRangeException(nameof(comparison), comparison, "Filter Comparison is not supported")
        };
    }

    public static IQueryable<Person> HasPersonChapterCount(this IQueryable<Person> queryable, bool condition,
        FilterComparison comparison, int count)
    {
        if (!condition) return queryable;

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
            FilterComparison.BeginsWith or FilterComparison.EndsWith or FilterComparison.Matches
                or FilterComparison.Contains or FilterComparison.NotContains or FilterComparison.IsBefore
                or FilterComparison.IsAfter or FilterComparison.IsInLast or FilterComparison.IsNotInLast
                or FilterComparison.MustContains
                or FilterComparison.IsEmpty => throw new KavitaException(
                    $"{comparison} not applicable for Person.ChapterCount"),
            _ => throw new ArgumentOutOfRangeException(nameof(comparison), comparison, "Filter Comparison is not supported")
        };
    }
}
