using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using API.DTOs.Metadata.Browse;
using API.Entities;
using API.Entities.Enums;
using API.Entities.Metadata;
using API.Helpers;
using API.Helpers.Builders;
using Xunit;

namespace API.Tests.Repository;

public class TagRepositoryTests : AbstractDbTest
{
    private AppUser _fullAccess;
    private AppUser _restrictedAccess;
    private AppUser _restrictedAgeAccess;

    protected override async Task ResetDb()
    {
        Context.Tag.RemoveRange(Context.Tag);
        Context.Library.RemoveRange(Context.Library);
        await Context.SaveChangesAsync();
    }

    private TestTagSet CreateTestTags()
    {
        return new TestTagSet
        {
            SharedSeriesChaptersTag = new TagBuilder("Shared Series Chapter Tag").Build(),
            SharedSeriesTag = new TagBuilder("Shared Series Tag").Build(),
            SharedChaptersTag = new TagBuilder("Shared Chapters Tag").Build(),
            Lib0SeriesChaptersTag = new TagBuilder("Lib0 Series Chapter Tag").Build(),
            Lib0SeriesTag = new TagBuilder("Lib0 Series Tag").Build(),
            Lib0ChaptersTag = new TagBuilder("Lib0 Chapters Tag").Build(),
            Lib1SeriesChaptersTag = new TagBuilder("Lib1 Series Chapter Tag").Build(),
            Lib1SeriesTag = new TagBuilder("Lib1 Series Tag").Build(),
            Lib1ChaptersTag = new TagBuilder("Lib1 Chapters Tag").Build(),
            Lib1ChapterAgeTag = new TagBuilder("Lib1 Chapter Age Tag").Build()
        };
    }

    private async Task SeedDbWithTags(TestTagSet tags)
    {
        await CreateTestUsers();
        await AddTagsToContext(tags);
        await CreateLibrariesWithTags(tags);
        await AssignLibrariesToUsers();
    }

    private async Task CreateTestUsers()
    {
        _fullAccess = new AppUserBuilder("amelia", "amelia@example.com").Build();
        _restrictedAccess = new AppUserBuilder("mila", "mila@example.com").Build();
        _restrictedAgeAccess = new AppUserBuilder("eva", "eva@example.com").Build();
        _restrictedAgeAccess.AgeRestriction = AgeRating.Teen;
        _restrictedAgeAccess.AgeRestrictionIncludeUnknowns = true;

        Context.Users.Add(_fullAccess);
        Context.Users.Add(_restrictedAccess);
        Context.Users.Add(_restrictedAgeAccess);
        await Context.SaveChangesAsync();
    }

    private async Task AddTagsToContext(TestTagSet tags)
    {
        var allTags = tags.GetAllTags();
        Context.Tag.AddRange(allTags);
        await Context.SaveChangesAsync();
    }

    private async Task CreateLibrariesWithTags(TestTagSet tags)
    {
        var lib0 = new LibraryBuilder("lib0")
            .WithSeries(new SeriesBuilder("lib0-s0")
                .WithMetadata(new SeriesMetadata
                {
                    Tags = [tags.SharedSeriesChaptersTag, tags.SharedSeriesTag, tags.Lib0SeriesChaptersTag, tags.Lib0SeriesTag]
                })
                .WithVolume(new VolumeBuilder("1")
                    .WithChapter(new ChapterBuilder("1")
                        .WithTags([tags.SharedSeriesChaptersTag, tags.SharedChaptersTag, tags.Lib0SeriesChaptersTag, tags.Lib0ChaptersTag])
                        .Build())
                    .WithChapter(new ChapterBuilder("2")
                        .WithTags([tags.SharedSeriesChaptersTag, tags.SharedChaptersTag, tags.Lib1SeriesChaptersTag, tags.Lib1ChaptersTag])
                        .Build())
                    .Build())
                .Build())
            .Build();

        var lib1 = new LibraryBuilder("lib1")
            .WithSeries(new SeriesBuilder("lib1-s0")
                .WithMetadata(new SeriesMetadataBuilder()
                    .WithTags([tags.SharedSeriesChaptersTag, tags.SharedSeriesTag, tags.Lib1SeriesChaptersTag, tags.Lib1SeriesTag])
                    .WithAgeRating(AgeRating.Mature17Plus)
                    .Build())
                .WithVolume(new VolumeBuilder("1")
                    .WithChapter(new ChapterBuilder("1")
                        .WithTags([tags.SharedSeriesChaptersTag, tags.SharedChaptersTag, tags.Lib1SeriesChaptersTag, tags.Lib1ChaptersTag])
                        .Build())
                    .WithChapter(new ChapterBuilder("2")
                        .WithTags([tags.SharedSeriesChaptersTag, tags.SharedChaptersTag, tags.Lib1SeriesChaptersTag, tags.Lib1ChaptersTag, tags.Lib1ChapterAgeTag])
                        .WithAgeRating(AgeRating.Mature17Plus)
                        .Build())
                    .Build())
                .Build())
            .WithSeries(new SeriesBuilder("lib1-s1")
                .WithMetadata(new SeriesMetadataBuilder()
                    .WithTags([tags.SharedSeriesChaptersTag, tags.SharedSeriesTag, tags.Lib1SeriesChaptersTag, tags.Lib1SeriesTag])
                    .Build())
                .WithVolume(new VolumeBuilder("1")
                    .WithChapter(new ChapterBuilder("1")
                        .WithTags([tags.SharedSeriesChaptersTag, tags.SharedChaptersTag, tags.Lib1SeriesChaptersTag, tags.Lib1ChaptersTag])
                        .Build())
                    .WithChapter(new ChapterBuilder("2")
                        .WithTags([tags.SharedSeriesChaptersTag, tags.SharedChaptersTag, tags.Lib1SeriesChaptersTag, tags.Lib1ChaptersTag])
                        .WithAgeRating(AgeRating.Mature17Plus)
                        .Build())
                    .Build())
                .Build())
            .Build();

        Context.Library.Add(lib0);
        Context.Library.Add(lib1);
        await Context.SaveChangesAsync();
    }

    private async Task AssignLibrariesToUsers()
    {
        var lib0 = Context.Library.First(l => l.Name == "lib0");
        var lib1 = Context.Library.First(l => l.Name == "lib1");

        _fullAccess.Libraries.Add(lib0);
        _fullAccess.Libraries.Add(lib1);
        _restrictedAccess.Libraries.Add(lib1);
        _restrictedAgeAccess.Libraries.Add(lib1);

        await Context.SaveChangesAsync();
    }

    private static Predicate<BrowseTagDto> ContainsTagCheck(Tag tag)
    {
        return t => t.Id == tag.Id;
    }

    private static void AssertTagPresent(IEnumerable<BrowseTagDto> tags, Tag expectedTag)
    {
        Assert.Contains(tags, ContainsTagCheck(expectedTag));
    }

    private static void AssertTagNotPresent(IEnumerable<BrowseTagDto> tags, Tag expectedTag)
    {
        Assert.DoesNotContain(tags, ContainsTagCheck(expectedTag));
    }

    private static BrowseTagDto GetTagDto(IEnumerable<BrowseTagDto> tags, Tag tag)
    {
        return tags.First(dto => dto.Id == tag.Id);
    }

    [Fact]
    public async Task GetBrowseableTag_FullAccess_ReturnsAllTagsWithCorrectCounts()
    {
        // Arrange
        await ResetDb();
        var tags = CreateTestTags();
        await SeedDbWithTags(tags);

        // Act
        var fullAccessTags = await UnitOfWork.TagRepository.GetBrowseableTag(_fullAccess.Id, new UserParams());

        // Assert
        Assert.Equal(tags.GetAllTags().Count, fullAccessTags.TotalCount);

        foreach (var tag in tags.GetAllTags())
        {
            AssertTagPresent(fullAccessTags, tag);
        }

        // Verify counts - 1 series lib0, 2 series lib1 = 3 total series
        Assert.Equal(3, GetTagDto(fullAccessTags, tags.SharedSeriesChaptersTag).SeriesCount);
        Assert.Equal(6, GetTagDto(fullAccessTags, tags.SharedSeriesChaptersTag).ChapterCount);
        Assert.Equal(1, GetTagDto(fullAccessTags, tags.Lib0SeriesTag).SeriesCount);
    }

    [Fact]
    public async Task GetBrowseableTag_RestrictedAccess_ReturnsOnlyAccessibleTags()
    {
        // Arrange
        await ResetDb();
        var tags = CreateTestTags();
        await SeedDbWithTags(tags);

        // Act
        var restrictedAccessTags = await UnitOfWork.TagRepository.GetBrowseableTag(_restrictedAccess.Id, new UserParams());

        // Assert - Should see: 3 shared + 4 library 1 specific = 7 tags
        Assert.Equal(7, restrictedAccessTags.TotalCount);

        // Verify shared and Library 1 tags are present
        AssertTagPresent(restrictedAccessTags, tags.SharedSeriesChaptersTag);
        AssertTagPresent(restrictedAccessTags, tags.SharedSeriesTag);
        AssertTagPresent(restrictedAccessTags, tags.SharedChaptersTag);
        AssertTagPresent(restrictedAccessTags, tags.Lib1SeriesChaptersTag);
        AssertTagPresent(restrictedAccessTags, tags.Lib1SeriesTag);
        AssertTagPresent(restrictedAccessTags, tags.Lib1ChaptersTag);
        AssertTagPresent(restrictedAccessTags, tags.Lib1ChapterAgeTag);

        // Verify Library 0 specific tags are not present
        AssertTagNotPresent(restrictedAccessTags, tags.Lib0SeriesChaptersTag);
        AssertTagNotPresent(restrictedAccessTags, tags.Lib0SeriesTag);
        AssertTagNotPresent(restrictedAccessTags, tags.Lib0ChaptersTag);

        // Verify counts - 2 series lib1
        Assert.Equal(2, GetTagDto(restrictedAccessTags, tags.SharedSeriesChaptersTag).SeriesCount);
        Assert.Equal(4, GetTagDto(restrictedAccessTags, tags.SharedSeriesChaptersTag).ChapterCount);
        Assert.Equal(2, GetTagDto(restrictedAccessTags, tags.Lib1SeriesTag).SeriesCount);
        Assert.Equal(4, GetTagDto(restrictedAccessTags, tags.Lib1ChaptersTag).ChapterCount);
    }

    [Fact]
    public async Task GetBrowseableTag_RestrictedAgeAccess_FiltersAgeRestrictedContent()
    {
        // Arrange
        await ResetDb();
        var tags = CreateTestTags();
        await SeedDbWithTags(tags);

        // Act
        var restrictedAgeAccessTags = await UnitOfWork.TagRepository.GetBrowseableTag(_restrictedAgeAccess.Id, new UserParams());

        // Assert - Should see: 3 shared + 3 lib1 specific = 6 tags (age-restricted tag filtered out)
        Assert.Equal(6, restrictedAgeAccessTags.TotalCount);

        // Verify accessible tags are present
        AssertTagPresent(restrictedAgeAccessTags, tags.SharedSeriesChaptersTag);
        AssertTagPresent(restrictedAgeAccessTags, tags.SharedSeriesTag);
        AssertTagPresent(restrictedAgeAccessTags, tags.SharedChaptersTag);
        AssertTagPresent(restrictedAgeAccessTags, tags.Lib1SeriesChaptersTag);
        AssertTagPresent(restrictedAgeAccessTags, tags.Lib1SeriesTag);
        AssertTagPresent(restrictedAgeAccessTags, tags.Lib1ChaptersTag);

        // Verify age-restricted tag is filtered out
        AssertTagNotPresent(restrictedAgeAccessTags, tags.Lib1ChapterAgeTag);

        // Verify counts - 1 series lib1 (age-restricted series filtered out)
        Assert.Equal(1, GetTagDto(restrictedAgeAccessTags, tags.SharedSeriesChaptersTag).SeriesCount);
        Assert.Equal(2, GetTagDto(restrictedAgeAccessTags, tags.SharedSeriesChaptersTag).ChapterCount);
        Assert.Equal(1, GetTagDto(restrictedAgeAccessTags, tags.Lib1SeriesTag).SeriesCount);
        Assert.Equal(2, GetTagDto(restrictedAgeAccessTags, tags.Lib1ChaptersTag).ChapterCount);
    }

    private class TestTagSet
    {
        public Tag SharedSeriesChaptersTag { get; set; }
        public Tag SharedSeriesTag { get; set; }
        public Tag SharedChaptersTag { get; set; }
        public Tag Lib0SeriesChaptersTag { get; set; }
        public Tag Lib0SeriesTag { get; set; }
        public Tag Lib0ChaptersTag { get; set; }
        public Tag Lib1SeriesChaptersTag { get; set; }
        public Tag Lib1SeriesTag { get; set; }
        public Tag Lib1ChaptersTag { get; set; }
        public Tag Lib1ChapterAgeTag { get; set; }

        public List<Tag> GetAllTags()
        {
            return
            [
                SharedSeriesChaptersTag, SharedSeriesTag, SharedChaptersTag,
                Lib0SeriesChaptersTag, Lib0SeriesTag, Lib0ChaptersTag,
                Lib1SeriesChaptersTag, Lib1SeriesTag, Lib1ChaptersTag, Lib1ChapterAgeTag
            ];
        }
    }
}
