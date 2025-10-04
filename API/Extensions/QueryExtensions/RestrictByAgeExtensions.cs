using System;
using System.Collections.Generic;
using System.Linq;
using API.Data.Misc;
using API.Entities;
using API.Entities.Enums;
using API.Entities.Metadata;
using API.Entities.Person;

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

    public static IQueryable<SeriesMetadataPeople> RestrictAgainstAgeRestriction(this IQueryable<SeriesMetadataPeople> queryable, AgeRestriction restriction)
    {
        if (restriction.AgeRating == AgeRating.NotApplicable) return queryable;
        var q = queryable.Where(s => s.SeriesMetadata.AgeRating <= restriction.AgeRating);

        if (!restriction.IncludeUnknowns)
        {
            return q.Where(s => s.SeriesMetadata.AgeRating != AgeRating.Unknown);
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

    public static IQueryable<ChapterPeople> RestrictAgainstAgeRestriction(this IQueryable<ChapterPeople> queryable, AgeRestriction restriction)
    {
        if (restriction.AgeRating == AgeRating.NotApplicable) return queryable;
        var q = queryable.Where(cp => cp.Chapter.Volume.Series.Metadata.AgeRating <= restriction.AgeRating);

        if (!restriction.IncludeUnknowns)
        {
            return q.Where(cp => cp.Chapter.Volume.Series.Metadata.AgeRating != AgeRating.Unknown);
        }

        return q;
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

    /// <summary>
    /// Returns all Genres where any of the linked Series/Chapters are less than or equal to restriction age rating
    /// </summary>
    /// <param name="queryable"></param>
    /// <param name="restriction"></param>
    /// <returns></returns>
    public static IQueryable<Genre> RestrictAgainstAgeRestriction(this IQueryable<Genre> queryable, AgeRestriction restriction)
    {
        if (restriction.AgeRating == AgeRating.NotApplicable) return queryable;

        if (restriction.IncludeUnknowns)
        {
            return queryable.Where(c =>
                c.SeriesMetadatas.Any(sm => sm.AgeRating <= restriction.AgeRating) ||
                c.Chapters.Any(cp => cp.AgeRating <= restriction.AgeRating));
        }

        return queryable.Where(c =>
            c.SeriesMetadatas.Any(sm => sm.AgeRating <= restriction.AgeRating && sm.AgeRating != AgeRating.Unknown) ||
            c.Chapters.Any(cp => cp.AgeRating <= restriction.AgeRating && cp.AgeRating != AgeRating.Unknown)
        );
    }

    public static IQueryable<Tag> RestrictAgainstAgeRestriction(this IQueryable<Tag> queryable, AgeRestriction restriction)
    {
        if (restriction.AgeRating == AgeRating.NotApplicable) return queryable;

        if (restriction.IncludeUnknowns)
        {
            return queryable.Where(c =>
                c.SeriesMetadatas.Any(sm => sm.AgeRating <= restriction.AgeRating) ||
                c.Chapters.Any(cp => cp.AgeRating <= restriction.AgeRating));
        }

        return queryable.Where(c =>
            c.SeriesMetadatas.Any(sm => sm.AgeRating <= restriction.AgeRating && sm.AgeRating != AgeRating.Unknown) ||
            c.Chapters.Any(cp => cp.AgeRating <= restriction.AgeRating && cp.AgeRating != AgeRating.Unknown)
        );
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

    private static IQueryable<AppUserRating> RestrictAgainstAgeRestriction(this IQueryable<AppUserRating> queryable, AgeRestriction restriction, int userId)
    {
        if (restriction.AgeRating == AgeRating.NotApplicable) return queryable;
        var q = queryable.Where(r => r.Series.Metadata.AgeRating <= restriction.AgeRating || r.AppUserId == userId);

        if (!restriction.IncludeUnknowns)
        {
            return q.Where(a => a.Series.Metadata.AgeRating != AgeRating.Unknown || a.AppUserId == userId);
        }

        return q;
    }

    private static IQueryable<AppUserChapterRating> RestrictAgainstAgeRestriction(this IQueryable<AppUserChapterRating> queryable, AgeRestriction restriction, int userId)
    {
        if (restriction.AgeRating == AgeRating.NotApplicable) return queryable;
        var q = queryable.Where(r => r.Series.Metadata.AgeRating <= restriction.AgeRating || r.AppUserId == userId);

        if (!restriction.IncludeUnknowns)
        {
            return q.Where(a => a.Series.Metadata.AgeRating != AgeRating.Unknown || a.AppUserId == userId);
        }

        return q;
    }

    private static IQueryable<AppUserAnnotation> RestrictAgainstAgeRestriction(this IQueryable<AppUserAnnotation> queryable, AgeRestriction restriction, int userId)
    {
        if (restriction.AgeRating == AgeRating.NotApplicable) return queryable;
        var q = queryable.Where(a => a.Series.Metadata.AgeRating <= restriction.AgeRating || a.AppUserId == userId);

        if (!restriction.IncludeUnknowns)
        {
            return q.Where(a => a.Series.Metadata.AgeRating != AgeRating.Unknown || a.AppUserId == userId);
        }

        return q;
    }

    // TODO: After updating to .net 10, leverage new Complex Data type queries to inline all db operations here
    /// <summary>
    /// Filter annotations by social preferences of users
    /// </summary>
    /// <param name="queryable"></param>
    /// <param name="userId"></param>
    /// <param name="userPreferences">List of user preferences for every user on the server</param>
    /// <returns></returns>
    public static IQueryable<AppUserAnnotation> RestrictBySocialPreferences(this IQueryable<AppUserAnnotation> queryable, int userId, IList<AppUserPreferences> userPreferences)
    {
        var preferencesById = userPreferences.ToDictionary(p => p.AppUserId, p => p.SocialPreferences);
        var socialPreferences = preferencesById[userId];

        if (socialPreferences.ViewOtherAnnotations)
        {
            // We are unable to do dictionary lookups in Sqlite; This means we need to translate them to X IN Y.
            var sharingUserIds = userPreferences
                .Where(p => p.SocialPreferences.ShareAnnotations)
                .Select(p => p.AppUserId)
                .ToHashSet();

            // Only include the users' annotations, or those of users that are sharing
            queryable = queryable.Where(a => a.AppUserId == userId || sharingUserIds.Contains(a.AppUserId));

            // For other users' annotation
            foreach (var sharingUserId in sharingUserIds.Where(id => id != userId))
            {
                // Filter out libs if enabled
                var libs = preferencesById[sharingUserId].SocialLibraries;
                if (libs.Count > 0)
                {
                    queryable = queryable.Where(a => a.AppUserId != sharingUserId ||  libs.Contains(a.LibraryId));
                }

                // Filter on age rating
                var ageRating = preferencesById[sharingUserId].SocialMaxAgeRating;
                var includeUnknowns = preferencesById[sharingUserId].SocialIncludeUnknowns;
                if (ageRating != AgeRating.NotApplicable)
                {
                    queryable = queryable.Where(a => a.AppUserId != sharingUserId || a.Series.Metadata.AgeRating <= ageRating)
                        .WhereIf(!includeUnknowns,
                            a => a.AppUserId != sharingUserId || a.Series.Metadata.AgeRating != AgeRating.Unknown);
                }
            }
        }
        else
        {
            queryable = queryable.Where(a => a.AppUserId == userId);
        }

        return queryable
            .WhereIf(socialPreferences.SocialLibraries.Count > 0,
                a => a.AppUserId == userId || socialPreferences.SocialLibraries.Contains(a.LibraryId))
            .RestrictAgainstAgeRestriction(new AgeRestriction
            {
                AgeRating = socialPreferences.SocialMaxAgeRating,
                IncludeUnknowns = socialPreferences.SocialIncludeUnknowns,
            }, userId);
    }

    // TODO: After updating to .net 10, leverage new Complex Data type queries to inline all db operations here
    /// <summary>
    /// Filter user reviews social preferences of users
    /// </summary>
    /// <param name="queryable"></param>
    /// <param name="userId"></param>
    /// <param name="userPreferences">List of user preferences for every user on the server</param>
    /// <returns></returns>
    public static IQueryable<AppUserRating> RestrictBySocialPreferences(this IQueryable<AppUserRating> queryable, int userId, IList<AppUserPreferences> userPreferences)
    {
        var preferencesById = userPreferences.ToDictionary(p => p.AppUserId, p => p.SocialPreferences);
        var socialPreferences = preferencesById[userId];

        var sharingUserIds = userPreferences
            .Where(p => p.SocialPreferences.ShareReviews)
            .Select(p => p.AppUserId)
            .ToHashSet();

        queryable = queryable.Where(r => r.AppUserId == userId || sharingUserIds.Contains(r.AppUserId));

        foreach (var sharingUserId in sharingUserIds.Where(id => id != userId))
        {
            var libs = preferencesById[sharingUserId].SocialLibraries;
            if (libs.Count > 0)
            {
                queryable = queryable.Where(r => r.AppUserId != sharingUserId || libs.Contains(r.Series.LibraryId));
            }

            var ageRating = preferencesById[sharingUserId].SocialMaxAgeRating;
            var includeUnknowns = preferencesById[sharingUserId].SocialIncludeUnknowns;
            if (ageRating != AgeRating.NotApplicable)
            {
                queryable = queryable.Where(r => r.AppUserId != sharingUserId || r.Series.Metadata.AgeRating <= ageRating)
                    .WhereIf(!includeUnknowns,
                        r => r.AppUserId != sharingUserId || r.Series.Metadata.AgeRating != AgeRating.Unknown);
            }
        }

        return queryable
            .WhereIf(socialPreferences.SocialLibraries.Count > 0,
                r => r.AppUserId == userId || socialPreferences.SocialLibraries.Contains(r.Series.LibraryId))
            .RestrictAgainstAgeRestriction(new AgeRestriction
            {
                AgeRating = socialPreferences.SocialMaxAgeRating,
                IncludeUnknowns = socialPreferences.SocialIncludeUnknowns,
            }, userId);
    }

    // TODO: After updating to .net 10, leverage new Complex Data type queries to inline all db operations here
    /// <summary>
    /// Filter user chapter reviews social preferences of users
    /// </summary>
    /// <param name="queryable"></param>
    /// <param name="userId"></param>
    /// <param name="userPreferences">List of user preferences for every user on the server</param>
    /// <returns></returns>
    public static IQueryable<AppUserChapterRating> RestrictBySocialPreferences(this IQueryable<AppUserChapterRating> queryable, int userId, IList<AppUserPreferences> userPreferences)
    {
        var preferencesById = userPreferences.ToDictionary(p => p.AppUserId, p => p.SocialPreferences);
        var socialPreferences = preferencesById[userId];

        var sharingUserIds = userPreferences
            .Where(p => p.SocialPreferences.ShareReviews)
            .Select(p => p.AppUserId)
            .ToHashSet();

        queryable = queryable.Where(r => r.AppUserId == userId || sharingUserIds.Contains(r.AppUserId));

        foreach (var sharingUserId in sharingUserIds.Where(id => id != userId))
        {
            var libs = preferencesById[sharingUserId].SocialLibraries;
            if (libs.Count > 0)
            {
                queryable = queryable.Where(r => r.AppUserId != sharingUserId || libs.Contains(r.Series.LibraryId));
            }

            var ageRating = preferencesById[sharingUserId].SocialMaxAgeRating;
            var includeUnknowns = preferencesById[sharingUserId].SocialIncludeUnknowns;
            if (ageRating != AgeRating.NotApplicable)
            {
                queryable = queryable.Where(r => r.AppUserId != sharingUserId || r.Series.Metadata.AgeRating <= ageRating)
                    .WhereIf(!includeUnknowns,
                        r => r.AppUserId != sharingUserId || r.Series.Metadata.AgeRating != AgeRating.Unknown);
            }
        }

        return queryable
            .WhereIf(socialPreferences.SocialLibraries.Count > 0,
                r => r.AppUserId == userId || socialPreferences.SocialLibraries.Contains(r.Series.LibraryId))
            .RestrictAgainstAgeRestriction(new AgeRestriction
            {
                AgeRating = socialPreferences.SocialMaxAgeRating,
                IncludeUnknowns = socialPreferences.SocialIncludeUnknowns,
            }, userId);
    }
}
