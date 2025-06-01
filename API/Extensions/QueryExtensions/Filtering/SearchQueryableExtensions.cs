using System;
using System.Collections.Generic;
using System.Linq;
using API.Data.Misc;
using API.Data.Repositories;
using API.Entities;
using API.Entities.Metadata;
using API.Entities.Person;
using Microsoft.EntityFrameworkCore;

namespace API.Extensions.QueryExtensions.Filtering;

public static class SearchQueryableExtensions
{
    public static IQueryable<AppUserCollection> Search(this IQueryable<AppUserCollection> queryable,
        string searchQuery, int userId, AgeRestriction userRating)
    {
        return queryable
            .Where(uc => uc.Promoted || uc.AppUserId == userId)
            .Where(s => EF.Functions.Like(s.Title!, $"%{searchQuery}%")
                        || EF.Functions.Like(s.NormalizedTitle!, $"%{searchQuery}%"))
            .RestrictAgainstAgeRestriction(userRating)
            .OrderBy(s => s.NormalizedTitle);
    }

    public static IQueryable<ReadingList> Search(this IQueryable<ReadingList> queryable,
        string searchQuery, int userId, AgeRestriction userRating)
    {
        return queryable
            .Where(rl => rl.AppUserId == userId || rl.Promoted)
            .Where(rl => EF.Functions.Like(rl.Title, $"%{searchQuery}%"))
            .RestrictAgainstAgeRestriction(userRating)
            .OrderBy(s => s.NormalizedTitle);
    }

    public static IQueryable<Library> Search(this IQueryable<Library> queryable,
        string searchQuery, int userId, IEnumerable<int> libraryIds)
    {
        return queryable
            .Where(l => libraryIds.Contains(l.Id))
            .Where(l => EF.Functions.Like(l.Name, $"%{searchQuery}%"))
            .IsRestricted(QueryContext.Search)
            .AsSplitQuery()
            .OrderBy(l => l.Name.ToLower());
    }

    public static IQueryable<Person> SearchPeople(this IQueryable<SeriesMetadata> queryable,
        string searchQuery, IEnumerable<int> seriesIds)
    {
        // Get people from SeriesMetadata
        var peopleFromSeriesMetadata = queryable
            .Where(sm => seriesIds.Contains(sm.SeriesId))
            .SelectMany(sm => sm.People.Select(sp => sp.Person))
            .Where(p =>
                EF.Functions.Like(p.Name, $"%{searchQuery}%") ||
                p.Aliases.Any(pa => EF.Functions.Like(pa.Alias, $"%{searchQuery}%"))
            );

        var peopleFromChapterPeople = queryable
            .Where(sm => seriesIds.Contains(sm.SeriesId))
            .SelectMany(sm => sm.Series.Volumes)
            .SelectMany(v => v.Chapters)
            .SelectMany(ch => ch.People.Select(cp => cp.Person))
            .Where(p =>
                EF.Functions.Like(p.Name, $"%{searchQuery}%") ||
                p.Aliases.Any(pa => EF.Functions.Like(pa.Alias, $"%{searchQuery}%"))
            );

        // Combine both queries and ensure distinct results
        return peopleFromSeriesMetadata
            .Union(peopleFromChapterPeople)
            .Select(p => p)
            .OrderBy(p => p.NormalizedName);
    }

    public static IQueryable<Genre> SearchGenres(this IQueryable<SeriesMetadata> queryable,
        string searchQuery, IEnumerable<int> seriesIds)
    {
        return queryable
            .Where(sm => seriesIds.Contains(sm.SeriesId))
            .SelectMany(sm => sm.Genres.Where(t => EF.Functions.Like(t.Title, $"%{searchQuery}%")))
            .Distinct()
            .OrderBy(t => t.NormalizedTitle);
    }

    public static IQueryable<Tag> SearchTags(this IQueryable<SeriesMetadata> queryable,
        string searchQuery, IEnumerable<int> seriesIds)
    {
        return queryable
            .Where(sm => seriesIds.Contains(sm.SeriesId))
            .SelectMany(sm => sm.Tags.Where(t => EF.Functions.Like(t.Title, $"%{searchQuery}%")))
            .AsSplitQuery()
            .Distinct()
            .OrderBy(t => t.NormalizedTitle);
    }
}
