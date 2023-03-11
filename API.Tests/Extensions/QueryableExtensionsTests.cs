using System.Collections.Generic;
using System.Linq;
using API.Data;
using API.Data.Misc;
using API.Entities;
using API.Entities.Enums;
using API.Entities.Metadata;
using API.Extensions;
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
                .WithMetadata( new SeriesMetadata()
                {
                    AgeRating = AgeRating.Teen,
                })
                .Build(),
            new SeriesBuilder("Test 2")
                .WithMetadata( new SeriesMetadata()
                {
                    AgeRating = AgeRating.Unknown,
                })
                .Build(),
            new SeriesBuilder("Test 3")
                .WithMetadata( new SeriesMetadata()
                {
                    AgeRating = AgeRating.X18Plus,
                })
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
        var items = new List<CollectionTag>()
        {
            new CollectionTag()
            {
                Title = "Test",
                NormalizedTitle = "Test".ToNormalized(),
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
                Title = "Test",
                NormalizedTitle = "Test".ToNormalized(),
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
                Title = "Test",
                NormalizedTitle = "Test".ToNormalized(),
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
    public void RestrictAgainstAgeRestriction_Genre_ShouldRestrictEverythingAboveTeen(bool includeUnknowns, int expectedCount)
    {
        var items = new List<Genre>()
        {
            new Genre()
            {
                Title = "A",
                NormalizedTitle = "A".ToNormalized(),
                SeriesMetadatas = new List<SeriesMetadata>()
                {
                    new SeriesMetadata()
                    {
                        AgeRating = AgeRating.Teen,
                    }
                }
            },
            new Genre()
            {
                Title = "B",
                NormalizedTitle = "B".ToNormalized(),
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
            new Genre()
            {
                Title = "C",
                NormalizedTitle = "C".ToNormalized(),
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
    public void RestrictAgainstAgeRestriction_Tag_ShouldRestrictEverythingAboveTeen(bool includeUnknowns, int expectedCount)
    {
        var items = new List<Tag>()
        {
            new Tag()
            {
                Title = "Test 1",
                NormalizedTitle = "Test 1".ToNormalized(),
                SeriesMetadatas = new List<SeriesMetadata>()
                {
                    new SeriesMetadata()
                    {
                        AgeRating = AgeRating.Teen,
                    }
                }
            },
            new Tag()
            {
                Title = "Test 2",
                NormalizedTitle = "Test 2".ToNormalized(),
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
            new Tag()
            {
                Title = "Test 3",
                NormalizedTitle = "Test 3".ToNormalized(),
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
    public void RestrictAgainstAgeRestriction_Person_ShouldRestrictEverythingAboveTeen(bool includeUnknowns, int expectedCount)
    {
        var items = new List<Person>()
        {
            new PersonBuilder("Test", PersonRole.Character)
                .WithSeriesMetadata(new SeriesMetadata()
                {
                    AgeRating = AgeRating.Teen,
                })
                .Build(),
            new PersonBuilder("Test", PersonRole.Character)
                .WithSeriesMetadata(new SeriesMetadata()
                {
                    AgeRating = AgeRating.Unknown,
                })
                .WithSeriesMetadata(new SeriesMetadata()
                {
                    AgeRating = AgeRating.Teen,
                })
                .Build(),
            new PersonBuilder("Test", PersonRole.Character)
                .WithSeriesMetadata(new SeriesMetadata()
                {
                    AgeRating = AgeRating.X18Plus,
                })
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
            DbFactory.ReadingList("Test List", null, false, AgeRating.Teen),
            DbFactory.ReadingList("Test List", null, false, AgeRating.Unknown),
            DbFactory.ReadingList("Test List", null, false, AgeRating.X18Plus),
        };

        var filtered = items.AsQueryable().RestrictAgainstAgeRestriction(new AgeRestriction()
        {
            AgeRating = AgeRating.Teen,
            IncludeUnknowns = includeUnknowns
        });
        Assert.Equal(expectedCount, filtered.Count());
    }
}
