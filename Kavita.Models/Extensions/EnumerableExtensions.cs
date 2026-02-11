using System.Collections.Generic;
using System.Linq;
using Kavita.Models.DTOs.SeriesDetail;
using Kavita.Models.Entities;
using Kavita.Models.Entities.Enums;
using Kavita.Models.Entities.Metadata;

namespace Kavita.Models.Extensions;

public static class EnumerableExtensions
{
    public static IEnumerable<RecentlyAddedSeriesDto> RestrictAgainstAgeRestriction(this IEnumerable<RecentlyAddedSeriesDto> items, AgeRestriction restriction)
    {
        if (restriction.AgeRating == AgeRating.NotApplicable) return items;
        var q = items.Where(s => s.AgeRating <= restriction.AgeRating);
        if (!restriction.IncludeUnknowns)
        {
            return q.Where(s => s.AgeRating != AgeRating.Unknown);
        }

        return q;
    }

    public static IEnumerable<SeriesMetadata> RestrictAgainstAgeRestriction(this IEnumerable<SeriesMetadata> items, AgeRestriction restriction)
    {
        if (restriction.AgeRating == AgeRating.NotApplicable) return items;
        var q = items.Where(s => s.AgeRating <= restriction.AgeRating);
        if (!restriction.IncludeUnknowns)
        {
            return q.Where(s => s.AgeRating != AgeRating.Unknown);
        }

        return q;
    }

    public static IEnumerable<Chapter> RestrictAgainstAgeRestriction(this IEnumerable<Chapter> items, AgeRestriction restriction)
    {
        if (restriction.AgeRating == AgeRating.NotApplicable) return items;
        var q = items.Where(s => s.AgeRating <= restriction.AgeRating);
        if (!restriction.IncludeUnknowns)
        {
            return q.Where(s => s.AgeRating != AgeRating.Unknown);
        }

        return q;
    }
}
