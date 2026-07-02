using System;
using System.Collections.Generic;
using System.Linq;
using Kavita.Models.DTOs.KavitaPlus.Metadata;
using Kavita.Models.DTOs.Recommendation;
using Kavita.Models.Entities;
using Kavita.Models.Entities.Enums;
using Kavita.Services.Plus;

namespace Kavita.Services.Helpers;

public static class RecommendationHelper
{
    /// <summary>
    /// Computes the effective age rating for an external recommendation: the provider's base rating raised by
    /// Kavita's own tag/genre age-rating mappings. Fails closed - when no rating can be determined the most
    /// restrictive rating is returned so restricted users never see unrated (potentially explicit) content.
    /// </summary>
    /// <param name="baseRating">Base rating supplied by the provider</param>
    /// <param name="genres">Genres from the provider</param>
    /// <param name="tags">Tag names from the provider</param>
    /// <param name="settings">Metadata settings holding the age-rating mappings and field mappings</param>
    public static AgeRating ComputeExternalAgeRating(AgeRating baseRating, IEnumerable<string> genres,
        IEnumerable<string> tags, MetadataSettingsDto settings)
    {
        ExternalMetadataService.GenerateExternalGenreAndTagsList(
            (genres ?? []).ToList(), (tags ?? []).ToList(), settings,
            out var processedTags, out var processedGenres);

        var tagDerived = ExternalMetadataService.DetermineAgeRating(
            processedGenres.Concat(processedTags), settings.AgeRatingMappings);

        var effective = (AgeRating) Math.Max((int) baseRating, (int) tagDerived);

        // Fail closed: an indeterminate rating is treated as the most restrictive so it is hidden from
        // any age-restricted user until the provider supplies a rating we can map.
        return effective == AgeRating.Unknown ? AgeRating.X18Plus : effective;
    }

    /// <summary>
    /// Whether content with the given age rating is visible under the supplied age restriction.
    /// </summary>
    public static bool IsWithinAgeRestriction(AgeRating ageRating, AgeRestriction restriction)
    {
        if (restriction.AgeRating == AgeRating.NotApplicable) return true;
        if (ageRating > restriction.AgeRating) return false;
        if (!restriction.IncludeUnknowns && ageRating == AgeRating.Unknown) return false;
        return true;
    }

    public static IList<ExternalSeriesDto> FilterExternalRecommendations(IEnumerable<ExternalSeriesDto> recommendations,
        AgeRestriction restriction)
    {
        return (recommendations ?? [])
            .Where(r => IsWithinAgeRestriction(r.AgeRating, restriction))
            .ToList();
    }
}
