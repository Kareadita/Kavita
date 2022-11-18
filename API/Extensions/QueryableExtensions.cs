using System.Linq;
using System.Threading.Tasks;
using API.Data.Misc;
using API.Data.Repositories;
using API.Entities;
using API.Entities.Enums;
using Microsoft.EntityFrameworkCore;

namespace API.Extensions;

public static class QueryableExtensions
{
    public static IQueryable<Series> RestrictAgainstAgeRestriction(this IQueryable<Series> queryable, AgeRestriction restriction)
    {
        if (restriction.AgeRating == AgeRating.NotApplicable) return queryable;
        var q = queryable.Where(s => s.Metadata.AgeRating <= restriction.AgeRating);
        if (!restriction.IncludeUnknowns)
        {
            return q.Where(s => s.Metadata.AgeRating != AgeRating.Unknown);
        }

        return q;
    }

    public static IQueryable<CollectionTag> RestrictAgainstAgeRestriction(this IQueryable<CollectionTag> queryable, AgeRestriction restriction)
    {
        if (restriction.AgeRating == AgeRating.NotApplicable) return queryable;

        if (restriction.IncludeUnknowns)
        {
            return queryable.Where(c => c.SeriesMetadatas.All(sm =>
                sm.AgeRating <= restriction.AgeRating));
        }

        return queryable.Where(c => c.SeriesMetadatas.All(sm =>
            sm.AgeRating <= restriction.AgeRating && sm.AgeRating > AgeRating.Unknown));
    }

    public static IQueryable<Genre> RestrictAgainstAgeRestriction(this IQueryable<Genre> queryable, AgeRestriction restriction)
    {
        if (restriction.AgeRating == AgeRating.NotApplicable) return queryable;

        if (restriction.IncludeUnknowns)
        {
            return queryable.Where(c => c.SeriesMetadatas.All(sm =>
                sm.AgeRating <= restriction.AgeRating));
        }

        return queryable.Where(c => c.SeriesMetadatas.All(sm =>
            sm.AgeRating <= restriction.AgeRating && sm.AgeRating > AgeRating.Unknown));
    }

    public static IQueryable<Tag> RestrictAgainstAgeRestriction(this IQueryable<Tag> queryable, AgeRestriction restriction)
    {
        if (restriction.AgeRating == AgeRating.NotApplicable) return queryable;

        if (restriction.IncludeUnknowns)
        {
            return queryable.Where(c => c.SeriesMetadatas.All(sm =>
                sm.AgeRating <= restriction.AgeRating));
        }

        return queryable.Where(c => c.SeriesMetadatas.All(sm =>
            sm.AgeRating <= restriction.AgeRating && sm.AgeRating > AgeRating.Unknown));
    }

    public static IQueryable<Person> RestrictAgainstAgeRestriction(this IQueryable<Person> queryable, AgeRestriction restriction)
    {
        if (restriction.AgeRating == AgeRating.NotApplicable) return queryable;

        if (restriction.IncludeUnknowns)
        {
            return queryable.Where(c => c.SeriesMetadatas.All(sm =>
                sm.AgeRating <= restriction.AgeRating));
        }

        return queryable.Where(c => c.SeriesMetadatas.All(sm =>
            sm.AgeRating <= restriction.AgeRating && sm.AgeRating > AgeRating.Unknown));
    }

    public static IQueryable<ReadingList> RestrictAgainstAgeRestriction(this IQueryable<ReadingList> queryable, AgeRestriction restriction)
    {
        if (restriction.AgeRating == AgeRating.NotApplicable) return queryable;
        var q = queryable.Where(rl => rl.AgeRating <= restriction.AgeRating);

        if (!restriction.IncludeUnknowns)
        {
            return q.Where(rl => rl.AgeRating != AgeRating.Unknown);
        }

        return q;
    }

    public static Task<AgeRestriction> GetUserAgeRestriction(this DbSet<AppUser> queryable, int userId)
    {
        if (userId < 1)
        {
            return Task.FromResult(new AgeRestriction()
            {
                AgeRating = AgeRating.NotApplicable,
                IncludeUnknowns = true
            });
        }
        return queryable
            .AsNoTracking()
            .Where(u => u.Id == userId)
            .Select(u =>
                new AgeRestriction(){
                    AgeRating = u.AgeRestriction,
                    IncludeUnknowns = u.AgeRestrictionIncludeUnknowns
                })
            .SingleAsync();
    }

    public static IQueryable<CollectionTag> Includes(this IQueryable<CollectionTag> queryable,
        CollectionTagIncludes includes)
    {
        if (includes.HasFlag(CollectionTagIncludes.SeriesMetadata))
        {
            queryable = queryable.Include(c => c.SeriesMetadatas);
        }

        return queryable.AsSplitQuery();
    }

    public static IQueryable<Series> Includes(this IQueryable<Series> query,
        SeriesIncludes includeFlags)
    {
        if (includeFlags.HasFlag(SeriesIncludes.Library))
        {
            query = query.Include(u => u.Library);
        }

        if (includeFlags.HasFlag(SeriesIncludes.Volumes))
        {
            query = query.Include(s => s.Volumes);
        }

        if (includeFlags.HasFlag(SeriesIncludes.Related))
        {
            query = query.Include(s => s.Relations)
                .ThenInclude(r => r.TargetSeries)
                .Include(s => s.RelationOf);
        }

        if (includeFlags.HasFlag(SeriesIncludes.Metadata))
        {
            query = query.Include(s => s.Metadata)
                .ThenInclude(m => m.CollectionTags.OrderBy(g => g.NormalizedTitle))
                .Include(s => s.Metadata)
                .ThenInclude(m => m.Genres.OrderBy(g => g.NormalizedTitle))
                .Include(s => s.Metadata)
                .ThenInclude(m => m.People)
                .Include(s => s.Metadata)
                .ThenInclude(m => m.Tags.OrderBy(g => g.NormalizedTitle));
        }


        return query.AsSplitQuery();
    }

    /// <summary>
    /// Applies restriction based on if the Library has restrictions (like include in search)
    /// </summary>
    /// <param name="query"></param>
    /// <param name="type"></param>
    /// <returns></returns>
    public static IQueryable<Library> IsRestricted(this IQueryable<Library> query, LibraryQueryType type)
    {
        if (type.HasFlag(LibraryQueryType.None)) return query;

        if (type.HasFlag(LibraryQueryType.Dashboard))
        {
            query = query.Where(l => l.IncludeInDashboard);
        }

        if (type.HasFlag(LibraryQueryType.Recommended))
        {
            query = query.Where(l => l.IncludeInRecommended);
        }

        if (type.HasFlag(LibraryQueryType.Search))
        {
            query = query.Where(l => l.IncludeInSearch);
        }

        return query;
    }
}
