﻿using System.Linq;
using API.Data.Misc;
using API.Entities;
using API.Entities.Enums;

namespace API.Extensions.QueryExtensions;

/// <summary>
/// Responsible for restricting Entities based on an AgeRestriction
/// </summary>
public static class RestrictByAgeExtensions
{
    public static IQueryable<Series> RestrictAgainstAgeRestriction(this IQueryable<Series> queryable, AgeRestriction restriction)
    {
        if (restriction.AgeRating == AgeRating.NotApplicable) return queryable;
        var q = queryable.Where(s => s.Metadata.AgeRating <= restriction.AgeRating);

        if (!restriction.IncludeUnknowns)
        {
            return q.Where(s => s.Metadata.AgeRating != AgeRating.Unknown);
        }

        //q.WhereIf(!restriction.IncludeUnknowns, s => s.Metadata.AgeRating != AgeRating.Unknown);

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
}
