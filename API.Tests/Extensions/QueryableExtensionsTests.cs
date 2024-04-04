﻿using System.Collections.Generic;
using System.Linq;
using API.Data;
using API.Data.Misc;
using API.Entities;
using API.Entities.Enums;
using API.Entities.Metadata;
using API.Extensions;
using API.Extensions.QueryExtensions;
using API.Helpers.Builders;
using Xunit;

namespace API.Tests.Extensions;

public class QueryableExtensionsTests
{
    [Theory]
    [InlineData(true, 2)]
    [InlineData(false, 1)]
    public void RestrictAgainstAgeRestriction_Series_ShouldRestrictEverythingAboveTeen(bool includeUnknowns, int expectedCount)
    {
        var items = new List<Series>()
        {
            new SeriesBuilder("Test 1")
                .WithMetadata(new SeriesMetadataBuilder().WithAgeRating(AgeRating.Teen).Build())
                .Build(),
            new SeriesBuilder("Test 2")
                .WithMetadata(new SeriesMetadataBuilder().WithAgeRating(AgeRating.Unknown).Build())
                .Build(),
            new SeriesBuilder("Test 3")
                .WithMetadata(new SeriesMetadataBuilder().WithAgeRating(AgeRating.X18Plus).Build())
                .Build()
        };

        var filtered = items.AsQueryable().RestrictAgainstAgeRestriction(new AgeRestriction()
        {
            AgeRating = AgeRating.Teen,
            IncludeUnknowns = includeUnknowns
        });
        Assert.Equal(expectedCount, filtered.Count());
    }

    [Theory]
    [InlineData(true, 2)]
    [InlineData(false, 1)]
    public void RestrictAgainstAgeRestriction_CollectionTag_ShouldRestrictEverythingAboveTeen(bool includeUnknowns, int expectedCount)
    {
        var items = new List<AppUserCollection>()
        {
            new AppUserCollectionBuilder("Test")
                .WithItem(new SeriesBuilder("S1").WithMetadata(new SeriesMetadataBuilder().WithAgeRating(AgeRating.Teen).Build()).Build())
                .Build(),
            new AppUserCollectionBuilder("Test 2")
                .WithItem(new SeriesBuilder("S2").WithMetadata(new SeriesMetadataBuilder().WithAgeRating(AgeRating.Unknown).Build()).Build())
                .WithItem(new SeriesBuilder("S1").WithMetadata(new SeriesMetadataBuilder().WithAgeRating(AgeRating.Teen).Build()).Build())
                .Build(),
            new AppUserCollectionBuilder("Test 3")
                .WithItem(new SeriesBuilder("S3").WithMetadata(new SeriesMetadataBuilder().WithAgeRating(AgeRating.X18Plus).Build()).Build())
                .Build(),
        };

        var filtered = items.AsQueryable().RestrictAgainstAgeRestriction(new AgeRestriction()
        {
            AgeRating = AgeRating.Teen,
            IncludeUnknowns = includeUnknowns
        });
        Assert.Equal(expectedCount, filtered.Count());
    }

    [Theory]
    [InlineData(true, 2)]
    [InlineData(false, 1)]
    public void RestrictAgainstAgeRestriction_Genre_ShouldRestrictEverythingAboveTeen(bool includeUnknowns, int expectedCount)
    {
        var items = new List<Genre>()
        {
            new GenreBuilder("A")
                .WithSeriesMetadata(new SeriesMetadataBuilder().WithAgeRating(AgeRating.Teen).Build())
                .Build(),
            new GenreBuilder("B")
                .WithSeriesMetadata(new SeriesMetadataBuilder().WithAgeRating(AgeRating.Unknown).Build())
                .WithSeriesMetadata(new SeriesMetadataBuilder().WithAgeRating(AgeRating.Teen).Build())
                .Build(),
            new GenreBuilder("C")
                .WithSeriesMetadata(new SeriesMetadataBuilder().WithAgeRating(AgeRating.X18Plus).Build())
                .Build(),
        };

        var filtered = items.AsQueryable().RestrictAgainstAgeRestriction(new AgeRestriction()
        {
            AgeRating = AgeRating.Teen,
            IncludeUnknowns = includeUnknowns
        });
        Assert.Equal(expectedCount, filtered.Count());
    }

    [Theory]
    [InlineData(true, 2)]
    [InlineData(false, 1)]
    public void RestrictAgainstAgeRestriction_Tag_ShouldRestrictEverythingAboveTeen(bool includeUnknowns, int expectedCount)
    {
        var items = new List<Tag>()
        {
            new TagBuilder("Test 1")
                .WithSeriesMetadata(new SeriesMetadataBuilder().WithAgeRating(AgeRating.Teen).Build())
                .Build(),
            new TagBuilder("Test 2")
                .WithSeriesMetadata(new SeriesMetadataBuilder().WithAgeRating(AgeRating.Unknown).Build())
                .WithSeriesMetadata(new SeriesMetadataBuilder().WithAgeRating(AgeRating.Teen).Build())
                .Build(),
            new TagBuilder("Test 3")
                .WithSeriesMetadata(new SeriesMetadataBuilder().WithAgeRating(AgeRating.X18Plus).Build())
                .Build(),
        };

        var filtered = items.AsQueryable().RestrictAgainstAgeRestriction(new AgeRestriction()
        {
            AgeRating = AgeRating.Teen,
            IncludeUnknowns = includeUnknowns
        });
        Assert.Equal(expectedCount, filtered.Count());
    }

    [Theory]
    [InlineData(true, 2)]
    [InlineData(false, 1)]
    public void RestrictAgainstAgeRestriction_Person_ShouldRestrictEverythingAboveTeen(bool includeUnknowns, int expectedCount)
    {
        var items = new List<Person>()
        {
            new PersonBuilder("Test", PersonRole.Character)
                .WithSeriesMetadata(new SeriesMetadataBuilder().WithAgeRating(AgeRating.Teen).Build())
                .Build(),
            new PersonBuilder("Test", PersonRole.Character)
                .WithSeriesMetadata(new SeriesMetadataBuilder().WithAgeRating(AgeRating.Unknown).Build())
                .WithSeriesMetadata(new SeriesMetadataBuilder().WithAgeRating(AgeRating.Teen).Build())
                .Build(),
            new PersonBuilder("Test", PersonRole.Character)
                .WithSeriesMetadata(new SeriesMetadataBuilder().WithAgeRating(AgeRating.X18Plus).Build())
                .Build(),
        };

        var filtered = items.AsQueryable().RestrictAgainstAgeRestriction(new AgeRestriction()
        {
            AgeRating = AgeRating.Teen,
            IncludeUnknowns = includeUnknowns
        });
        Assert.Equal(expectedCount, filtered.Count());
    }

    [Theory]
    [InlineData(true, 2)]
    [InlineData(false, 1)]
    public void RestrictAgainstAgeRestriction_ReadingList_ShouldRestrictEverythingAboveTeen(bool includeUnknowns, int expectedCount)
    {

        var items = new List<ReadingList>()
        {
            new ReadingListBuilder("Test List").WithRating(AgeRating.Teen).Build(),
            new ReadingListBuilder("Test List").WithRating(AgeRating.Unknown).Build(),
            new ReadingListBuilder("Test List").WithRating(AgeRating.X18Plus).Build(),
        };

        var filtered = items.AsQueryable().RestrictAgainstAgeRestriction(new AgeRestriction()
        {
            AgeRating = AgeRating.Teen,
            IncludeUnknowns = includeUnknowns
        });
        Assert.Equal(expectedCount, filtered.Count());
    }
}
