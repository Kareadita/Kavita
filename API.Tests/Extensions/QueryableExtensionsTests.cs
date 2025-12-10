using System.Collections.Generic;
using System.Linq;
using API.Data.Misc;
using API.Entities;
using API.Entities.Enums;
using API.Entities.Enums.UserPreferences;
using API.Entities.Metadata;
using API.Entities.Person;
using API.Entities.User;
using API.Extensions.QueryExtensions;
using API.Helpers.Builders;
using Xunit;

namespace API.Tests.Extensions;

public class QueryableExtensionsTests
{
    [Theory]
    [InlineData(true, 2)]
    [InlineData(false, 1)]
    public void RestrictAgainstAgeRestriction_Series_ShouldRestrictEverythingAboveTeen(bool includeUnknowns,
        int expectedCount)
    {
        var items = new List<Series>
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

        var filtered = items.AsQueryable().RestrictAgainstAgeRestriction(new AgeRestriction
        {
            AgeRating = AgeRating.Teen,
            IncludeUnknowns = includeUnknowns
        });
        Assert.Equal(expectedCount, filtered.Count());
    }

    [Theory]
    [InlineData(true, 2)]
    [InlineData(false, 1)]
    public void RestrictAgainstAgeRestriction_CollectionTag_ShouldRestrictEverythingAboveTeen(bool includeUnknowns,
        int expectedCount)
    {
        var items = new List<AppUserCollection>
        {
            new AppUserCollectionBuilder("Test")
                .WithItem(new SeriesBuilder("S1")
                    .WithMetadata(new SeriesMetadataBuilder().WithAgeRating(AgeRating.Teen).Build()).Build())
                .Build(),
            new AppUserCollectionBuilder("Test 2")
                .WithItem(new SeriesBuilder("S2")
                    .WithMetadata(new SeriesMetadataBuilder().WithAgeRating(AgeRating.Unknown).Build()).Build())
                .WithItem(new SeriesBuilder("S1")
                    .WithMetadata(new SeriesMetadataBuilder().WithAgeRating(AgeRating.Teen).Build()).Build())
                .Build(),
            new AppUserCollectionBuilder("Test 3")
                .WithItem(new SeriesBuilder("S3")
                    .WithMetadata(new SeriesMetadataBuilder().WithAgeRating(AgeRating.X18Plus).Build()).Build())
                .Build()
        };

        var filtered = items.AsQueryable().RestrictAgainstAgeRestriction(new AgeRestriction
        {
            AgeRating = AgeRating.Teen,
            IncludeUnknowns = includeUnknowns
        });
        Assert.Equal(expectedCount, filtered.Count());
    }

    [Theory]
    [InlineData(true, 2)]
    [InlineData(false, 2)]
    public void RestrictAgainstAgeRestriction_Genre_ShouldRestrictEverythingAboveTeen(bool includeUnknowns,
        int expectedCount)
    {
        var items = new List<Genre>
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
                .Build()
        };

        var filtered = items.AsQueryable().RestrictAgainstAgeRestriction(new AgeRestriction
        {
            AgeRating = AgeRating.Teen,
            IncludeUnknowns = includeUnknowns
        });
        Assert.Equal(expectedCount, filtered.Count());
    }

    [Theory]
    [InlineData(true, 2)]
    [InlineData(false, 2)]
    public void RestrictAgainstAgeRestriction_Tag_ShouldRestrictEverythingAboveTeen(bool includeUnknowns,
        int expectedCount)
    {
        var items = new List<Tag>
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
                .Build()
        };

        var filtered = items.AsQueryable().RestrictAgainstAgeRestriction(new AgeRestriction
        {
            AgeRating = AgeRating.Teen,
            IncludeUnknowns = includeUnknowns
        });
        Assert.Equal(expectedCount, filtered.Count());
    }

    [Theory]
    [InlineData(true, 2)]
    [InlineData(false, 2)]
    public void RestrictAgainstAgeRestriction_Person_ShouldRestrictEverythingAboveTeen(bool includeUnknowns,
        int expectedPeopleCount)
    {

        var items = new List<Person>
        {
            CreatePersonWithSeriesMetadata("Test1", AgeRating.Teen),
            CreatePersonWithSeriesMetadata("Test2", AgeRating.Unknown,
                AgeRating.Teen), // 2 series on this person, restrict will still allow access
            CreatePersonWithSeriesMetadata("Test3", AgeRating.X18Plus)
        };

        var ageRestriction = new AgeRestriction
        {
            AgeRating = AgeRating.Teen,
            IncludeUnknowns = includeUnknowns
        };

        // Act
        var filtered = items.AsQueryable().RestrictAgainstAgeRestriction(ageRestriction);

        // Assert
        Assert.Equal(expectedPeopleCount, filtered.Count());
    }

    private static Person CreatePersonWithSeriesMetadata(string name, params AgeRating[] ageRatings)
    {
        var person = new PersonBuilder(name).Build();

        foreach (var ageRating in ageRatings)
        {
            var seriesMetadata = new SeriesMetadataBuilder().WithAgeRating(ageRating).Build();
            person.SeriesMetadataPeople.Add(new SeriesMetadataPeople
            {
                SeriesMetadata = seriesMetadata,
                Person = person,
                Role = PersonRole.Character // Role is now part of the relationship
            });
        }

        return person;
    }

    [Theory]
    [InlineData(true, 2)]
    [InlineData(false, 1)]
    public void RestrictAgainstAgeRestriction_ReadingList_ShouldRestrictEverythingAboveTeen(bool includeUnknowns,
        int expectedCount)
    {
        var items = new List<ReadingList>
        {
            new ReadingListBuilder("Test List").WithRating(AgeRating.Teen).Build(),
            new ReadingListBuilder("Test List").WithRating(AgeRating.Unknown).Build(),
            new ReadingListBuilder("Test List").WithRating(AgeRating.X18Plus).Build()
        };

        var filtered = items.AsQueryable().RestrictAgainstAgeRestriction(new AgeRestriction
        {
            AgeRating = AgeRating.Teen,
            IncludeUnknowns = includeUnknowns
        });
        Assert.Equal(expectedCount, filtered.Count());
    }

    [Fact]
    public void RestrictBySocialPreferences_SocialLibs()
    {
        IList<AppUserPreferences> userPreferences =
        [
            CreateUserPreferences(1, [], AgeRating.NotApplicable, true, true, true),
            CreateUserPreferences(2, [1], AgeRating.NotApplicable, true, true, true),
            CreateUserPreferences(3, [], AgeRating.NotApplicable, true, false, true)
        ];

        IList<AppUserAnnotation> annotations =
        [
            CreateAnnotationInLibraryWithAgeRating(1, 1, AgeRating.Unknown),
            CreateAnnotationInLibraryWithAgeRating(2, 1, AgeRating.Unknown),
            CreateAnnotationInLibraryWithAgeRating(2, 2, AgeRating.Unknown),
            CreateAnnotationInLibraryWithAgeRating(3, 1, AgeRating.Unknown),
            CreateAnnotationInLibraryWithAgeRating(3, 1, AgeRating.Unknown)
        ];

        // Own annotation, and the other in lib 1
        Assert.Equal(2, annotations.AsQueryable().RestrictBySocialPreferences(1, userPreferences).Count());

        // Own annotations, and from user 1 in lib 1
        Assert.Equal(3, annotations.AsQueryable().RestrictBySocialPreferences(2, userPreferences).Count());

        // Own annotations, and user 1 in lib 1 and user 2 in lib 1
        Assert.Equal(4, annotations.AsQueryable().RestrictBySocialPreferences(3, userPreferences).Count());
    }

    [Theory]
    [InlineData(true, 4, 3)]
    [InlineData(false, 3, 2)]
    public void RestrictBySocialPreferences_AgeRating(bool includeUnknowns, int expected1, int expected2)
    {
        IList<AppUserPreferences> userPreferences =
        [
            CreateUserPreferences(1, [], AgeRating.NotApplicable, true, true, true),
            CreateUserPreferences(2, [], AgeRating.Mature, includeUnknowns, true, true)
        ];

        IList<AppUserAnnotation> annotations =
        [
            CreateAnnotationInLibraryWithAgeRating(1, 1, AgeRating.AdultsOnly),
            CreateAnnotationInLibraryWithAgeRating(1, 1, AgeRating.Everyone),
            CreateAnnotationInLibraryWithAgeRating(1, 1, AgeRating.Unknown),
            CreateAnnotationInLibraryWithAgeRating(2, 1, AgeRating.Unknown)
        ];

        Assert.Equal(expected1, annotations.AsQueryable().RestrictBySocialPreferences(1, userPreferences).Count());
        Assert.Equal(expected2, annotations.AsQueryable().RestrictBySocialPreferences(2, userPreferences).Count());
    }

    [Fact]
    public void RestrictBySocialPreferences_UserNotSharingAnnotations()
    {
        IList<AppUserPreferences> userPreferences =
        [
            CreateUserPreferences(1, [], AgeRating.NotApplicable, true, false, true), // User 1 NOT sharing
            CreateUserPreferences(2, [], AgeRating.NotApplicable, true, true, true)
        ];

        IList<AppUserAnnotation> annotations =
        [
            CreateAnnotationInLibraryWithAgeRating(1, 1, AgeRating.Everyone),
            CreateAnnotationInLibraryWithAgeRating(2, 1, AgeRating.Everyone)
        ];

        // User 2 should only see their own annotation since User 1 is not sharing
        Assert.Equal(1, annotations.AsQueryable().RestrictBySocialPreferences(2, userPreferences).Count());

        // User 1 should see both (own + user 2's shared)
        Assert.Equal(2, annotations.AsQueryable().RestrictBySocialPreferences(1, userPreferences).Count());
    }

    [Fact]
    public void RestrictBySocialPreferences_UserNotViewingOtherAnnotations()
    {
        IList<AppUserPreferences> userPreferences =
        [
            CreateUserPreferences(1, [], AgeRating.NotApplicable, true, true, false), // User 1 NOT viewing others
            CreateUserPreferences(2, [], AgeRating.NotApplicable, true, true, true)
        ];

        IList<AppUserAnnotation> annotations =
        [
            CreateAnnotationInLibraryWithAgeRating(1, 1, AgeRating.Everyone),
            CreateAnnotationInLibraryWithAgeRating(2, 1, AgeRating.Everyone)
        ];

        // User 1 should only see their own annotation (not viewing others)
        Assert.Equal(1, annotations.AsQueryable().RestrictBySocialPreferences(1, userPreferences).Count());

        // User 2 should see both
        Assert.Equal(2, annotations.AsQueryable().RestrictBySocialPreferences(2, userPreferences).Count());
    }

    [Fact]
    public void RestrictBySocialPreferences_RequestingUserLibraryFilter()
    {
        IList<AppUserPreferences> userPreferences =
        [
            CreateUserPreferences(1, [1], AgeRating.NotApplicable, true, true, true), // User 1 only wants lib 1
            CreateUserPreferences(2, [], AgeRating.NotApplicable, true, true, true)
        ];

        IList<AppUserAnnotation> annotations =
        [
            CreateAnnotationInLibraryWithAgeRating(1, 1, AgeRating.Everyone),
            CreateAnnotationInLibraryWithAgeRating(1, 2, AgeRating.Everyone), // User 1's own in lib 2
            CreateAnnotationInLibraryWithAgeRating(2, 1, AgeRating.Everyone),
            CreateAnnotationInLibraryWithAgeRating(2, 2, AgeRating.Everyone)
        ];

        // User 1 should see: own (always) + user 2's in lib 1 only = 3
        Assert.Equal(3, annotations.AsQueryable().RestrictBySocialPreferences(1, userPreferences).Count());
    }

    [Fact]
    public void RestrictBySocialPreferences_RequestingUserAgeRatingFilter()
    {
        IList<AppUserPreferences> userPreferences =
        [
            CreateUserPreferences(1, [], AgeRating.Teen, false, true, true), // User 1 wants Teen max, no unknowns
            CreateUserPreferences(2, [], AgeRating.NotApplicable, true, true, true)
        ];

        IList<AppUserAnnotation> annotations =
        [
            CreateAnnotationInLibraryWithAgeRating(1, 1, AgeRating.AdultsOnly), // User 1's own - always included
            CreateAnnotationInLibraryWithAgeRating(2, 1, AgeRating.AdultsOnly),
            CreateAnnotationInLibraryWithAgeRating(2, 1, AgeRating.Teen),
            CreateAnnotationInLibraryWithAgeRating(2, 1, AgeRating.Unknown)
        ];

        // User 1 should see: own (1) + user 2's Teen (1) = 2
        Assert.Equal(2, annotations.AsQueryable().RestrictBySocialPreferences(1, userPreferences).Count());
    }

    [Fact]
    public void RestrictBySocialPreferences_CombinedLibraryAndAgeRatingFilters()
    {
        IList<AppUserPreferences> userPreferences =
        [
            CreateUserPreferences(1, [], AgeRating.NotApplicable, true, true, true),
            CreateUserPreferences(2, [1], AgeRating.Teen, true, true,
                true) // User 2: lib 1 only + Teen max + unknowns ok
        ];

        IList<AppUserAnnotation> annotations =
        [
            CreateAnnotationInLibraryWithAgeRating(1, 1, AgeRating.Everyone),
            CreateAnnotationInLibraryWithAgeRating(1, 2, AgeRating.Everyone),
            CreateAnnotationInLibraryWithAgeRating(1, 1, AgeRating.AdultsOnly),
            CreateAnnotationInLibraryWithAgeRating(2, 1, AgeRating.Teen),
            CreateAnnotationInLibraryWithAgeRating(2, 2, AgeRating.Everyone) // User 2's own in lib 2
        ];

        // User 2 should see:
        // - Own annotations (always): 2
        // - User 1's in lib 1 with age <= Teen: 1 (Everyone)
        // Total: 3
        Assert.Equal(3, annotations.AsQueryable().RestrictBySocialPreferences(2, userPreferences).Count());
    }

    [Fact]
    public void RestrictBySocialPreferences_MultipleUsersWithDifferentLibraryRestrictions()
    {
        IList<AppUserPreferences> userPreferences =
        [
            CreateUserPreferences(1, [], AgeRating.NotApplicable, true, true, true),
            CreateUserPreferences(2, [1], AgeRating.NotApplicable, true, true, true), // User 2 shares lib 1 only
            CreateUserPreferences(3, [2], AgeRating.NotApplicable, true, true, true) // User 3 shares lib 2 only
        ];

        IList<AppUserAnnotation> annotations =
        [
            CreateAnnotationInLibraryWithAgeRating(1, 1, AgeRating.Everyone),
            CreateAnnotationInLibraryWithAgeRating(2, 1, AgeRating.Everyone),
            CreateAnnotationInLibraryWithAgeRating(2, 2, AgeRating.Everyone),
            CreateAnnotationInLibraryWithAgeRating(3, 1, AgeRating.Everyone),
            CreateAnnotationInLibraryWithAgeRating(3, 2, AgeRating.Everyone)
        ];

        // User 1 should see: own (1) + user 2 lib 1 (1) + user 3 lib 2 (1) = 3
        Assert.Equal(3, annotations.AsQueryable().RestrictBySocialPreferences(1, userPreferences).Count());
    }

    [Fact]
    public void RestrictBySocialPreferences_NoOtherUsersSharing()
    {
        IList<AppUserPreferences> userPreferences =
        [
            CreateUserPreferences(1, [], AgeRating.NotApplicable, true, false, true),
            CreateUserPreferences(2, [], AgeRating.NotApplicable, true, false, true)
        ];

        IList<AppUserAnnotation> annotations =
        [
            CreateAnnotationInLibraryWithAgeRating(1, 1, AgeRating.Everyone),
            CreateAnnotationInLibraryWithAgeRating(2, 1, AgeRating.Everyone)
        ];

        // Each user should only see their own
        Assert.Equal(1, annotations.AsQueryable().RestrictBySocialPreferences(1, userPreferences).Count());
        Assert.Equal(1, annotations.AsQueryable().RestrictBySocialPreferences(2, userPreferences).Count());
    }

    [Theory]
    [InlineData(AgeRating.Everyone, true, 3)]
    [InlineData(AgeRating.Everyone, false, 2)]
    [InlineData(AgeRating.Teen, true, 4)]
    [InlineData(AgeRating.Mature17Plus, false, 3)]
    public void RestrictBySocialPreferences_RequestingUserAgeRatingVariations(AgeRating maxRating, bool includeUnknowns,
        int expected)
    {
        IList<AppUserPreferences> userPreferences =
        [
            CreateUserPreferences(1, [], maxRating, includeUnknowns, true, true),
            CreateUserPreferences(2, [], AgeRating.NotApplicable, true, true, true)
        ];

        IList<AppUserAnnotation> annotations =
        [
            CreateAnnotationInLibraryWithAgeRating(1, 1, AgeRating.AdultsOnly), // Own - always included
            CreateAnnotationInLibraryWithAgeRating(2, 1, AgeRating.Everyone),
            CreateAnnotationInLibraryWithAgeRating(2, 1, AgeRating.Teen),
            CreateAnnotationInLibraryWithAgeRating(2, 1, AgeRating.Mature),
            CreateAnnotationInLibraryWithAgeRating(2, 1, AgeRating.Unknown)
        ];

        Assert.Equal(expected, annotations.AsQueryable().RestrictBySocialPreferences(1, userPreferences).Count());
    }

    private static AppUserPreferences CreateUserPreferences(int user, IList<int> libs, AgeRating ageRating,
        bool includeUnknowns, bool share, bool seeAn)
    {
        return new AppUserPreferences
        {
            AppUserId = user,
            Theme = null,
            SocialPreferences = new AppUserSocialPreferences
            {
                ShareReviews = share,
                ShareAnnotations = share,
                ViewOtherAnnotations = seeAn,
                SocialLibraries = libs,
                SocialMaxAgeRating = ageRating,
                SocialIncludeUnknowns = includeUnknowns
            }
        };
    }

    private static AppUserAnnotation CreateAnnotationInLibraryWithAgeRating(int user, int lib, AgeRating ageRating)
    {
        return new AppUserAnnotation
        {
            XPath = null,
            LibraryId = lib,
            SeriesId = 0,
            VolumeId = 0,
            ChapterId = 0,
            AppUserId = user,
            Series = new Series
            {
                Name = null,
                NormalizedName = null,
                NormalizedLocalizedName = null,
                SortName = null,
                LocalizedName = null,
                OriginalName = null,
                Metadata = new SeriesMetadata
                {
                    AgeRating = ageRating
                }
            }
        };
    }

    [Fact]
    public void RestrictBySocialPreferences_Rating_SocialLibs()
    {
        IList<AppUserPreferences> userPreferences =
        [
            CreateUserPreferences(1, [], AgeRating.NotApplicable, true, true, true),
            CreateUserPreferences(2, [1], AgeRating.NotApplicable, true, true, true),
            CreateUserPreferences(3, [], AgeRating.NotApplicable, true, false, true)
        ];

        IList<AppUserRating> ratings =
        [
            CreateRatingInLibraryWithAgeRating(1, 1, AgeRating.Unknown),
            CreateRatingInLibraryWithAgeRating(2, 1, AgeRating.Unknown),
            CreateRatingInLibraryWithAgeRating(2, 2, AgeRating.Unknown),
            CreateRatingInLibraryWithAgeRating(3, 1, AgeRating.Unknown),
            CreateRatingInLibraryWithAgeRating(3, 1, AgeRating.Unknown)
        ];

        // Own rating, and the other in lib 1
        Assert.Equal(2, ratings.AsQueryable().RestrictBySocialPreferences(1, userPreferences).Count());

        // Own ratings, and from user 1 in lib 1
        Assert.Equal(3, ratings.AsQueryable().RestrictBySocialPreferences(2, userPreferences).Count());

        // Own ratings, and user 1 in lib 1 and user 2 in lib 1
        Assert.Equal(4, ratings.AsQueryable().RestrictBySocialPreferences(3, userPreferences).Count());
    }

    [Theory]
    [InlineData(true, 4, 3)]
    [InlineData(false, 3, 2)]
    public void RestrictBySocialPreferences_Rating_AgeRating(bool includeUnknowns, int expected1, int expected2)
    {
        IList<AppUserPreferences> userPreferences =
        [
            CreateUserPreferences(1, [], AgeRating.NotApplicable, true, true, true),
            CreateUserPreferences(2, [], AgeRating.Mature, includeUnknowns, true, true)
        ];

        IList<AppUserRating> ratings =
        [
            CreateRatingInLibraryWithAgeRating(1, 1, AgeRating.AdultsOnly),
            CreateRatingInLibraryWithAgeRating(1, 1, AgeRating.Everyone),
            CreateRatingInLibraryWithAgeRating(1, 1, AgeRating.Unknown),
            CreateRatingInLibraryWithAgeRating(2, 1, AgeRating.Unknown)
        ];

        var f = ratings.AsQueryable().RestrictBySocialPreferences(1, userPreferences);
        Assert.Equal(expected1, ratings.AsQueryable().RestrictBySocialPreferences(1, userPreferences).Count());
        Assert.Equal(expected2, ratings.AsQueryable().RestrictBySocialPreferences(2, userPreferences).Count());
    }

    [Fact]
    public void RestrictBySocialPreferences_Rating_UserNotSharingReviews()
    {
        IList<AppUserPreferences> userPreferences =
        [
            CreateUserPreferences(1, [], AgeRating.NotApplicable, true, false, true),
            CreateUserPreferences(2, [], AgeRating.NotApplicable, true, true, true)
        ];

        IList<AppUserRating> ratings =
        [
            CreateRatingInLibraryWithAgeRating(1, 1, AgeRating.Everyone),
            CreateRatingInLibraryWithAgeRating(2, 1, AgeRating.Everyone)
        ];

        Assert.Equal(1, ratings.AsQueryable().RestrictBySocialPreferences(2, userPreferences).Count());
        Assert.Equal(2, ratings.AsQueryable().RestrictBySocialPreferences(1, userPreferences).Count());
    }

    [Fact]
    public void RestrictBySocialPreferences_Rating_RequestingUserLibraryFilter()
    {
        IList<AppUserPreferences> userPreferences =
        [
            CreateUserPreferences(1, [1], AgeRating.NotApplicable, true, true, true),
            CreateUserPreferences(2, [], AgeRating.NotApplicable, true, true, true)
        ];

        IList<AppUserRating> ratings =
        [
            CreateRatingInLibraryWithAgeRating(1, 1, AgeRating.Everyone),
            CreateRatingInLibraryWithAgeRating(1, 2, AgeRating.Everyone),
            CreateRatingInLibraryWithAgeRating(2, 1, AgeRating.Everyone),
            CreateRatingInLibraryWithAgeRating(2, 2, AgeRating.Everyone)
        ];

        Assert.Equal(3, ratings.AsQueryable().RestrictBySocialPreferences(1, userPreferences).Count());
    }

    [Fact]
    public void RestrictBySocialPreferences_Rating_RequestingUserAgeRatingFilter()
    {
        IList<AppUserPreferences> userPreferences =
        [
            CreateUserPreferences(1, [], AgeRating.Teen, false, true, true),
            CreateUserPreferences(2, [], AgeRating.NotApplicable, true, true, true)
        ];

        IList<AppUserRating> ratings =
        [
            CreateRatingInLibraryWithAgeRating(1, 1, AgeRating.AdultsOnly),
            CreateRatingInLibraryWithAgeRating(2, 1, AgeRating.AdultsOnly),
            CreateRatingInLibraryWithAgeRating(2, 1, AgeRating.Teen),
            CreateRatingInLibraryWithAgeRating(2, 1, AgeRating.Unknown)
        ];

        Assert.Equal(2, ratings.AsQueryable().RestrictBySocialPreferences(1, userPreferences).Count());
    }

    [Fact]
    public void RestrictBySocialPreferences_Rating_CombinedFilters()
    {
        IList<AppUserPreferences> userPreferences =
        [
            CreateUserPreferences(1, [], AgeRating.NotApplicable, true, true, true),
            CreateUserPreferences(2, [1], AgeRating.Teen, true, true, true)
        ];

        IList<AppUserRating> ratings =
        [
            CreateRatingInLibraryWithAgeRating(1, 1, AgeRating.Everyone),
            CreateRatingInLibraryWithAgeRating(1, 2, AgeRating.Everyone),
            CreateRatingInLibraryWithAgeRating(1, 1, AgeRating.AdultsOnly),
            CreateRatingInLibraryWithAgeRating(2, 1, AgeRating.Teen),
            CreateRatingInLibraryWithAgeRating(2, 2, AgeRating.Everyone)
        ];

        Assert.Equal(3, ratings.AsQueryable().RestrictBySocialPreferences(2, userPreferences).Count());
    }

    [Fact]
    public void RestrictBySocialPreferences_Rating_MultipleUsersWithDifferentLibraryRestrictions()
    {
        IList<AppUserPreferences> userPreferences =
        [
            CreateUserPreferences(1, [], AgeRating.NotApplicable, true, true, true),
            CreateUserPreferences(2, [1], AgeRating.NotApplicable, true, true, true),
            CreateUserPreferences(3, [2], AgeRating.NotApplicable, true, true, true)
        ];

        IList<AppUserRating> ratings =
        [
            CreateRatingInLibraryWithAgeRating(1, 1, AgeRating.Everyone),
            CreateRatingInLibraryWithAgeRating(2, 1, AgeRating.Everyone),
            CreateRatingInLibraryWithAgeRating(2, 2, AgeRating.Everyone),
            CreateRatingInLibraryWithAgeRating(3, 1, AgeRating.Everyone),
            CreateRatingInLibraryWithAgeRating(3, 2, AgeRating.Everyone)
        ];

        Assert.Equal(3, ratings.AsQueryable().RestrictBySocialPreferences(1, userPreferences).Count());
    }

    [Theory]
    [InlineData(AgeRating.Everyone, true, 3)]
    [InlineData(AgeRating.Everyone, false, 2)]
    [InlineData(AgeRating.Teen, true, 4)]
    [InlineData(AgeRating.Mature17Plus, false, 3)]
    public void RestrictBySocialPreferences_Rating_RequestingUserAgeRatingVariations(AgeRating maxRating,
        bool includeUnknowns, int expected)
    {
        IList<AppUserPreferences> userPreferences =
        [
            CreateUserPreferences(1, [], maxRating, includeUnknowns, true, true),
            CreateUserPreferences(2, [], AgeRating.NotApplicable, true, true, true)
        ];

        IList<AppUserRating> ratings =
        [
            CreateRatingInLibraryWithAgeRating(1, 1, AgeRating.AdultsOnly),
            CreateRatingInLibraryWithAgeRating(2, 1, AgeRating.Everyone),
            CreateRatingInLibraryWithAgeRating(2, 1, AgeRating.Teen),
            CreateRatingInLibraryWithAgeRating(2, 1, AgeRating.Mature),
            CreateRatingInLibraryWithAgeRating(2, 1, AgeRating.Unknown)
        ];

        Assert.Equal(expected, ratings.AsQueryable().RestrictBySocialPreferences(1, userPreferences).Count());
    }

    private static AppUserRating CreateRatingInLibraryWithAgeRating(int user, int lib, AgeRating ageRating)
    {
        return new AppUserRating
        {
            AppUserId = user,
            Series = new Series
            {
                Name = null,
                NormalizedName = null,
                NormalizedLocalizedName = null,
                SortName = null,
                LocalizedName = null,
                OriginalName = null,
                LibraryId = lib,
                Metadata = new SeriesMetadata
                {
                    AgeRating = ageRating
                }
            }
        };
    }
}
