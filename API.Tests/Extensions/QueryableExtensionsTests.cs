using System.Collections.Generic;
using System.Linq;
using API.Data.Misc;
using API.Entities;
using API.Entities.Enums;
using API.Entities.Metadata;
using API.Extensions;
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
            new Series()
            {
                Metadata = new SeriesMetadata()
                {
                    AgeRating = AgeRating.Teen,
                }
            },
            new Series()
            {
                Metadata = new SeriesMetadata()
                {
                    AgeRating = AgeRating.Unknown,
                }
            },
            new Series()
            {
                Metadata = new SeriesMetadata()
                {
                    AgeRating = AgeRating.X18Plus,
                }
            },
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
        var items = new List<CollectionTag>()
        {
            new CollectionTag()
            {
                SeriesMetadatas = new List<SeriesMetadata>()
                {
                    new SeriesMetadata()
                    {
                        AgeRating = AgeRating.Teen,
                    }
                }
            },
            new CollectionTag()
            {
                SeriesMetadatas = new List<SeriesMetadata>()
                {
                    new SeriesMetadata()
                    {
                        AgeRating = AgeRating.Unknown,
                    },
                    new SeriesMetadata()
                    {
                        AgeRating = AgeRating.Teen,
                    }
                }
            },
            new CollectionTag()
            {
                SeriesMetadatas = new List<SeriesMetadata>()
                {
                    new SeriesMetadata()
                    {
                        AgeRating = AgeRating.X18Plus,
                    }
                }
            },
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
            new ReadingList()
            {
                AgeRating = AgeRating.Teen,
            },
            new ReadingList()
            {
                AgeRating = AgeRating.Unknown,
            },
            new ReadingList()
            {
                AgeRating = AgeRating.X18Plus
            },
        };

        var filtered = items.AsQueryable().RestrictAgainstAgeRestriction(new AgeRestriction()
        {
            AgeRating = AgeRating.Teen,
            IncludeUnknowns = includeUnknowns
        });
        Assert.Equal(expectedCount, filtered.Count());
    }
}
