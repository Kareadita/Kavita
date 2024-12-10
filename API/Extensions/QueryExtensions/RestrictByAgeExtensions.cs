using System;
using System.Linq;
using API.Data.Misc;
using API.Entities;
using API.Entities.Enums;

namespace API.Extensions.QueryExtensions;
#nullable enable

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

        return q;
    }

    public static IQueryable<Chapter> RestrictAgainstAgeRestriction(this IQueryable<Chapter> queryable, AgeRestriction restriction)
    {
        if (restriction.AgeRating == AgeRating.NotApplicable) return queryable;
        var q = queryable.Where(chapter => chapter.Volume.Series.Metadata.AgeRating <= restriction.AgeRating);

        if (!restriction.IncludeUnknowns)
        {
            return q.Where(s => s.Volume.Series.Metadata.AgeRating != AgeRating.Unknown);
        }

        return q;
    }

    [Obsolete]
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

    public static IQueryable<AppUserCollection> RestrictAgainstAgeRestriction(this IQueryable<AppUserCollection> queryable, AgeRestriction restriction)
    {
        if (restriction.AgeRating == AgeRating.NotApplicable) return queryable;

        if (restriction.IncludeUnknowns)
        {
            return queryable.Where(c => c.Items.All(sm =>
                sm.Metadata.AgeRating <= restriction.AgeRating));
        }

        return queryable.Where(c => c.Items.All(sm =>
            sm.Metadata.AgeRating <= restriction.AgeRating && sm.Metadata.AgeRating > AgeRating.Unknown));
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
            return queryable.Where(c =>
                c.SeriesMetadataPeople.Any(sm => sm.SeriesMetadata.AgeRating <= restriction.AgeRating) ||
                c.ChapterPeople.Any(cp => cp.Chapter.AgeRating <= restriction.AgeRating));
        }

        return queryable.Where(c =>
            c.SeriesMetadataPeople.Any(sm => sm.SeriesMetadata.AgeRating <= restriction.AgeRating && sm.SeriesMetadata.AgeRating != AgeRating.Unknown) ||
            c.ChapterPeople.Any(cp => cp.Chapter.AgeRating <= restriction.AgeRating && cp.Chapter.AgeRating != AgeRating.Unknown)
        );
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
