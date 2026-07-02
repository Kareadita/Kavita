using System.Collections.Generic;
using System.Linq;
using Kavita.Models.DTOs.KavitaPlus.Metadata;
using Kavita.Models.DTOs.Recommendation;
using Kavita.Models.Entities;
using Kavita.Models.Entities.Enums;
using Kavita.Services.Helpers;
using Xunit;

namespace Kavita.Services.Tests;

public class RecommendationHelperTests
{
    #region IsWithinAgeRestriction

    [Fact]
    public void IsWithinAgeRestriction_NotApplicable_AllowsEverything()
    {
        var restriction = new AgeRestriction { AgeRating = AgeRating.NotApplicable, IncludeUnknowns = false };

        Assert.True(RecommendationHelper.IsWithinAgeRestriction(AgeRating.X18Plus, restriction));
        Assert.True(RecommendationHelper.IsWithinAgeRestriction(AgeRating.Unknown, restriction));
        Assert.True(RecommendationHelper.IsWithinAgeRestriction(AgeRating.G, restriction));
    }

    [Theory]
    [InlineData(AgeRating.G, true)]
    [InlineData(AgeRating.Teen, true)]
    [InlineData(AgeRating.Mature, false)]
    [InlineData(AgeRating.X18Plus, false)]
    public void IsWithinAgeRestriction_RespectsMaxRating(AgeRating rating, bool expected)
    {
        var restriction = new AgeRestriction { AgeRating = AgeRating.Teen, IncludeUnknowns = true };
        Assert.Equal(expected, RecommendationHelper.IsWithinAgeRestriction(rating, restriction));
    }

    [Fact]
    public void IsWithinAgeRestriction_UnknownFilteredWhenIncludeUnknownsFalse()
    {
        var restriction = new AgeRestriction { AgeRating = AgeRating.Teen, IncludeUnknowns = false };
        Assert.False(RecommendationHelper.IsWithinAgeRestriction(AgeRating.Unknown, restriction));
    }

    [Fact]
    public void IsWithinAgeRestriction_UnknownAllowedWhenIncludeUnknownsTrue()
    {
        var restriction = new AgeRestriction { AgeRating = AgeRating.Teen, IncludeUnknowns = true };
        Assert.True(RecommendationHelper.IsWithinAgeRestriction(AgeRating.Unknown, restriction));
    }

    #endregion

    #region FilterExternalRecommendations

    [Fact]
    public void FilterExternalRecommendations_RestrictedUser_ExcludesExplicit()
    {
        var recs = new List<ExternalSeriesDto>
        {
            Rec("Safe", AgeRating.G),
            Rec("TeenSeries", AgeRating.Teen),
            Rec("MatureSeries", AgeRating.Mature),
            Rec("Explicit", AgeRating.X18Plus),
        };
        var restriction = new AgeRestriction { AgeRating = AgeRating.Teen, IncludeUnknowns = false };

        var result = RecommendationHelper.FilterExternalRecommendations(recs, restriction);

        Assert.Equal(new[] { "Safe", "TeenSeries" }, result.Select(r => r.Name).ToArray());
        // Regression: a non-admin with a restriction must NOT see an X18+ external rec
        Assert.DoesNotContain(result, r => r.AgeRating == AgeRating.X18Plus);
    }

    [Fact]
    public void FilterExternalRecommendations_FailClosedRec_HiddenFromRestrictedUser()
    {
        // An indeterminate rec is stored as X18Plus (fail closed); it must not surface to a restricted user
        var recs = new List<ExternalSeriesDto> { Rec("Unrated", AgeRating.X18Plus) };
        var restriction = new AgeRestriction { AgeRating = AgeRating.Mature, IncludeUnknowns = true };

        var result = RecommendationHelper.FilterExternalRecommendations(recs, restriction);

        Assert.Empty(result);
    }

    [Fact]
    public void FilterExternalRecommendations_NotApplicable_ReturnsAll()
    {
        var recs = new List<ExternalSeriesDto>
        {
            Rec("Safe", AgeRating.G),
            Rec("Explicit", AgeRating.X18Plus),
        };
        var restriction = new AgeRestriction { AgeRating = AgeRating.NotApplicable, IncludeUnknowns = false };

        var result = RecommendationHelper.FilterExternalRecommendations(recs, restriction);

        Assert.Equal(2, result.Count);
    }

    #endregion

    #region ComputeExternalAgeRating

    [Fact]
    public void ComputeExternalAgeRating_ReturnsBaseWhenNoTagSignal()
    {
        var settings = Settings();
        var result = RecommendationHelper.ComputeExternalAgeRating(AgeRating.Teen, [], [], settings);
        Assert.Equal(AgeRating.Teen, result);
    }

    [Fact]
    public void ComputeExternalAgeRating_TakesMaxOfBaseAndTagDerived()
    {
        var settings = Settings(new Dictionary<string, AgeRating> { { "adult", AgeRating.X18Plus } });

        // Base is only Teen, but a tag maps to X18Plus -> the higher rating wins
        var result = RecommendationHelper.ComputeExternalAgeRating(AgeRating.Teen, [], ["Adult"], settings);

        Assert.Equal(AgeRating.X18Plus, result);
    }

    [Fact]
    public void ComputeExternalAgeRating_BaseWinsWhenHigherThanTagDerived()
    {
        var settings = Settings(new Dictionary<string, AgeRating> { { "action", AgeRating.Teen } });

        var result = RecommendationHelper.ComputeExternalAgeRating(AgeRating.Mature, ["Action"], [], settings);

        Assert.Equal(AgeRating.Mature, result);
    }

    [Fact]
    public void ComputeExternalAgeRating_Indeterminate_FailsClosedToX18Plus()
    {
        var settings = Settings();

        // No base rating and nothing maps -> most restrictive so restricted users never see it
        var result = RecommendationHelper.ComputeExternalAgeRating(AgeRating.Unknown, ["Slice of Life"], ["Comedy"], settings);

        Assert.Equal(AgeRating.X18Plus, result);
    }

    #endregion

    private static ExternalSeriesDto Rec(string name, AgeRating ageRating) => new()
    {
        Name = name,
        CoverUrl = "https://example.com/cover.jpg",
        Url = "https://example.com/" + name,
        AgeRating = ageRating
    };

    private static MetadataSettingsDto Settings(Dictionary<string, AgeRating>? ageRatingMappings = null) => new()
    {
        EnableExtendedMetadataProcessing = false,
        AgeRatingMappings = ageRatingMappings ?? new Dictionary<string, AgeRating>(),
        FieldMappings = [],
        Blacklist = [],
        Whitelist = [],
        Overrides = [],
        PersonRoles = []
    };
}
