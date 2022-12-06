using System.Collections.Generic;
using System.Linq;
using API.Data;
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
                Name = "Test 1",
                NormalizedName = "Test 1".ToNormalized(),
                Metadata = new SeriesMetadata()
                {
                    AgeRating = AgeRating.Teen,
                }
            },
            new Series()
            {
                Name = "Test 2",
                NormalizedName = "Test 2".ToNormalized(),
                Metadata = new SeriesMetadata()
                {
                    AgeRating = AgeRating.Unknown,
                }
            },
            new Series()
            {
                Name = "Test 3",
                NormalizedName = "Test 3".ToNormalized(),
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
                Title = "Test",
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
            new Person()
            {
                SeriesMetadatas = new List<SeriesMetadata>()
                {
                    new SeriesMetadata()
                    {
                        AgeRating = AgeRating.Teen,
                    }
                }
            },
            new Person()
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
            new Person()
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
