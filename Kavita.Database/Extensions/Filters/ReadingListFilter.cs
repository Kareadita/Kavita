using System;
using System.Collections.Generic;
using System.Linq;
using Kavita.Models.DTOs.Filtering.v2;
using Kavita.Models.Entities.Enums;
using Kavita.Models.Entities.ReadingLists;
using Microsoft.EntityFrameworkCore;

namespace Kavita.Database.Extensions.Filters;

public static class ReadingListFilter
{
    extension(IQueryable<ReadingList> queryable)
    {
        public IQueryable<ReadingList> HasTitle(bool condition, FilterComparison comparison, string queryString)
        {
            if (string.IsNullOrEmpty(queryString) || !condition) return queryable;
            ComparisonProfile.Validate(comparison, ComparisonProfile.String, "ReadingList.Title");

            return comparison switch
            {
                FilterComparison.Equal => queryable.Where(p => p.Title.Equals(queryString)),
                FilterComparison.NotEqual => queryable.Where(p => p.Title != queryString),
                FilterComparison.BeginsWith => queryable.Where(p => EF.Functions.Like(p.Title, $"{queryString}%")),
                FilterComparison.EndsWith => queryable.Where(p => EF.Functions.Like(p.Title, $"%{queryString}")),
                FilterComparison.Matches => queryable.Where(p => EF.Functions.Like(p.Title, $"%{queryString}%")),
                _ => throw new ArgumentOutOfRangeException(nameof(comparison), comparison, "Filter Comparison is not supported")
            };
        }

        public IQueryable<ReadingList> HasReleaseYear(bool condition, FilterComparison comparison, int? releaseYear)
        {
            if (!condition || releaseYear == null) return queryable;
            ComparisonProfile.Validate(comparison, ComparisonProfile.Date, "ReadingList.ReleaseYear");

            return comparison switch
            {
                FilterComparison.Equal => queryable.Where(s => s.StartingYear == releaseYear),
                FilterComparison.NotEqual => queryable.Where(s => s.StartingYear != releaseYear),
                FilterComparison.GreaterThan or FilterComparison.IsAfter => queryable.Where(s =>
                    s.StartingYear > releaseYear),
                FilterComparison.GreaterThanEqual => queryable.Where(s => s.StartingYear >= releaseYear),
                FilterComparison.LessThan or FilterComparison.IsBefore => queryable.Where(s =>
                    s.StartingYear < releaseYear),
                FilterComparison.LessThanEqual => queryable.Where(s => s.StartingYear <= releaseYear),
                FilterComparison.IsInLast => queryable.Where(s =>
                    s.StartingYear >= DateTime.Now.Year - (int) releaseYear),
                FilterComparison.IsNotInLast => queryable.Where(s =>
                    s.StartingYear < DateTime.Now.Year - (int) releaseYear),
                FilterComparison.IsEmpty => queryable.Where(s => s.StartingYear == 0),
                FilterComparison.IsNotEmpty => queryable.Where(s => s.StartingYear != 0),
                _ => throw new ArgumentOutOfRangeException(nameof(comparison), comparison, null)
            };
        }

        public IQueryable<ReadingList> HasItemCount(bool condition, FilterComparison comparison, int itemCount)
        {
            if (!condition) return queryable;
            ComparisonProfile.Validate(comparison, ComparisonProfile.Numeric, "ReadingList.ItemCount");

            return comparison switch
            {
                FilterComparison.NotEqual => queryable.WhereNotEqual(s => s.Items.Count, itemCount),
                FilterComparison.Equal => queryable.WhereEqual(s => s.Items.Count, itemCount),
                FilterComparison.GreaterThan => queryable.WhereGreaterThan(s => s.Items.Count, itemCount),
                FilterComparison.GreaterThanEqual => queryable.WhereGreaterThanOrEqual(s => s.Items.Count,
                    itemCount),
                FilterComparison.LessThan => queryable.WhereLessThan(s => s.Items.Count, itemCount),
                FilterComparison.LessThanEqual => queryable.WhereLessThanOrEqual(s => s.Items.Count, itemCount),
                _ => throw new ArgumentOutOfRangeException(nameof(comparison), comparison, null)
            };
        }

        public IQueryable<ReadingList> HasTags(bool condition, FilterComparison comparison, IList<int> tags)
        {
            if (!condition || (comparison != FilterComparison.IsEmpty && comparison != FilterComparison.IsNotEmpty && tags.Count == 0)) return queryable;
            ComparisonProfile.Validate(comparison, ComparisonProfile.ListWithEmpty, "ReadingList.Tags");

            switch (comparison)
            {
                case FilterComparison.Equal:
                case FilterComparison.Contains:
                    return queryable.Where(s => s.Tags.Any(t => tags.Contains(t.Id)));
                case FilterComparison.NotEqual:
                case FilterComparison.NotContains:
                    return queryable.Where(s => s.Tags.All(t => !tags.Contains(t.Id)));
                case FilterComparison.MustContains:
                    // Deconstruct and do a Union of a bunch of where statements since this doesn't translate
                    var queries = new List<IQueryable<ReadingList>>()
                    {
                        queryable
                    };
                    queries.AddRange(tags.Select(gId => queryable.Where(s => s.Tags.Any(p => p.Id == gId))));

                    return queries.Aggregate((q1, q2) => q1.Intersect(q2));
                case FilterComparison.IsEmpty:
                    return queryable.Where(s => s.Tags.Count == 0);
                case FilterComparison.IsNotEmpty:
                    return queryable.Where(s => s.Tags.Count > 0);
                default:
                    throw new ArgumentOutOfRangeException(nameof(comparison), comparison, null);
            }
        }

        public IQueryable<ReadingList> HasPeople(bool condition, FilterComparison comparison, IList<int> people, PersonRole role)
        {
            if (!condition || (comparison != FilterComparison.IsEmpty && comparison != FilterComparison.IsNotEmpty && people.Count == 0)) return queryable;
            ComparisonProfile.Validate(comparison, ComparisonProfile.ListWithEmpty, "ReadingList.People");

            switch (comparison)
            {
                case FilterComparison.Equal:
                case FilterComparison.Contains:
                    return queryable.Where(s => s.Items.Any(i => i.Chapter.People.Any(p => people.Contains(p.PersonId) && p.Role == role)));
                case FilterComparison.NotEqual:
                case FilterComparison.NotContains:
                    return queryable.Where(s => s.Items.All(i => i.Chapter.People.All(p => !people.Contains(p.PersonId) || p.Role != role)));
                case FilterComparison.MustContains:
                    var queries = new List<IQueryable<ReadingList>>()
                    {
                        queryable
                    };
                    queries.AddRange(people.Select(personId =>
                        queryable.Where(s => s.Items.Any(i => i.Chapter.People.Any(p => p.PersonId == personId && p.Role == role)))));

                    return queries.Aggregate((q1, q2) => q1.Intersect(q2));
                case FilterComparison.IsEmpty:
                    // Ensure no person with the given role exists across all items
                    return queryable.Where(s => s.Items.All(i => i.Chapter.People.All(p => p.Role != role)));
                case FilterComparison.IsNotEmpty:
                    return queryable.Where(s => s.Items.Any(i => i.Chapter.People.Any(p => p.Role == role)));
                default:
                    throw new ArgumentOutOfRangeException(nameof(comparison), comparison, null);
            }
        }
    }
}
