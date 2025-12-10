using System.Linq;
using API.DTOs.Statistics;
using API.Entities;
using API.Entities.Enums;
using API.Entities.Enums.UserPreferences;
using API.Entities.Progress;
using API.Entities.User;

namespace API.Extensions.QueryExtensions.Filtering;

public static class ActivityFilter
{

    /// <summary>
    /// Filter AppUserReadingSessionActivityData for the given filter, viewer, and owner
    /// </summary>
    /// <param name="queryable">source</param>
    /// <param name="filter">stats filter from the UI</param>
    /// <param name="userId">user id of the user <b>owing</b> the data</param>
    /// <param name="socialPreferences">social preferences of the user <b>owing</b> the data</param>
    /// <param name="requestingUser">the user <b>requesting</b> the data</param>
    /// <param name="onlyCompleted">return only data for fully read chapters</param>
    /// <param name="isAggregate">If this is aggregate data (counts, etc), the filter will opt out of restricting based on Social Libraries/Age Rating</param>
    /// <returns></returns>
    public static IQueryable<AppUserReadingSessionActivityData> ApplyStatsFilter(
        this IQueryable<AppUserReadingSessionActivityData> queryable,
        StatsFilterDto filter,
        int userId,
        AppUserSocialPreferences  socialPreferences,
        AppUser requestingUser,
        bool onlyCompleted = true,
        bool isAggregate = false
        )
    {
        var startTime = filter.StartDate?.ToUniversalTime();
        var endTime = filter.EndDate?.ToUniversalTime();
        var isOwnRequest = userId == requestingUser.Id;

        var shouldLimitOnSocialLibraries = !isOwnRequest && socialPreferences.SocialLibraries.Count > 0;
        var shouldLimitOnSocialAgeRating =
            !isOwnRequest && socialPreferences.SocialMaxAgeRating != AgeRating.NotApplicable;
        var shouldLimitOnAgeRating = !isOwnRequest && requestingUser.AgeRestriction != AgeRating.NotApplicable;

        queryable = queryable
            .Where(d => filter.Libraries.Contains(d.LibraryId) && d.ReadingSession.AppUserId == userId)
            .WhereIf(onlyCompleted, d => d.EndPage >= d.Chapter.Pages)
            .WhereIf(startTime != null, d => d.StartTime >= startTime)
            .WhereIf(endTime != null, d => d.EndTime <= endTime);

        if (isAggregate)
        {
            return queryable;
        }

        return queryable
            .WhereIf(shouldLimitOnSocialLibraries, d => socialPreferences.SocialLibraries.Contains(d.LibraryId))
            .WhereIf(shouldLimitOnSocialAgeRating, d =>
                (socialPreferences.SocialMaxAgeRating >= d.Chapter.Volume.Series.Metadata.AgeRating && d.Chapter.Volume.Series.Metadata.AgeRating != AgeRating.Unknown)
                || (socialPreferences.SocialIncludeUnknowns && d.Chapter.Volume.Series.Metadata.AgeRating == AgeRating.Unknown )
                )
            .WhereIf(shouldLimitOnAgeRating, d =>
                (requestingUser.AgeRestriction >= d.Chapter.Volume.Series.Metadata.AgeRating && d.Chapter.Volume.Series.Metadata.AgeRating != AgeRating.Unknown)
                || (requestingUser.AgeRestrictionIncludeUnknowns && d.Chapter.Volume.Series.Metadata.AgeRating == AgeRating.Unknown )
                )
            ;
    }

}
