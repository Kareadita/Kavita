using System.Linq;
using API.Entities;
using API.Entities.Enums;

namespace API.Extensions;

public static class QueryableExtensions
{
    public static IQueryable<Series> RestrictAgainstAgeRestriction(this IQueryable<Series> queryable, AgeRating rating)
    {
        return queryable.Where(s => rating == AgeRating.NotApplicable || s.Metadata.AgeRating <= rating);
    }

    public static IQueryable<CollectionTag> RestrictAgainstAgeRestriction(this IQueryable<CollectionTag> queryable, AgeRating rating)
    {
        return queryable.Where(c => c.SeriesMetadatas.All(sm => sm.AgeRating <= rating));
    }

    public static IQueryable<ReadingList> RestrictAgainstAgeRestriction(this IQueryable<ReadingList> queryable, AgeRating rating)
    {
        return queryable.Where(rl => rl.AgeRating <= rating);
    }
}
