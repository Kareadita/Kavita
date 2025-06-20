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
    private static readonly Tag SharedSeriesChaptersTag = new TagBuilder("Shared Series Chapter Tag").Build();
    private static readonly Tag SharedSeriesTag = new TagBuilder("Shared Series Tag").Build();
    private static readonly Tag SharedChaptersTag = new TagBuilder("Shared Chapters Tag").Build();
    private static readonly Tag Lib0SeriesChaptersTag = new TagBuilder("Lib0 Series Chapter Tag").Build();
    private static readonly Tag Lib0SeriesTag = new TagBuilder("Lib0 Series Tag").Build();
    private static readonly Tag Lib0ChaptersTag = new TagBuilder("Lib0 Chapters Tag").Build();
    private static readonly Tag Lib1SeriesChaptersTag = new TagBuilder("Lib1 Series Chapter Tag").Build();
    private static readonly Tag Lib1SeriesTag = new TagBuilder("Lib1 Series Tag").Build();
    private static readonly Tag Lib1ChaptersTag = new TagBuilder("Lib1 Chapters Tag").Build();
    private static readonly Tag Lib1ChapterAgeTag = new TagBuilder("Lib1 Chapter Age Tag").Build();

    private static readonly List<Tag> AllTags =
    [
        SharedSeriesChaptersTag, SharedSeriesTag, SharedChaptersTag,
        Lib0SeriesChaptersTag, Lib0SeriesTag, Lib0ChaptersTag,
        Lib1SeriesChaptersTag, Lib1SeriesTag, Lib1ChaptersTag, Lib1ChapterAgeTag
    ];


    /**
     * Access to lib0, lib1
     */
    private AppUser _fullAccess;
    /**
     * Access to lib1
     */
    private AppUser _restrictedAccess;
    /**
     * Access to lib1, and Teen
     */
    private AppUser _restrictedAgeAccess;

    protected override async Task ResetDb()
    {
        Context.Tag.RemoveRange(Context.Tag);
        Context.Library.RemoveRange(Context.Library);
        await Context.SaveChangesAsync();
    }

    private async Task SeedDb()
    {
        // Create users with different access levels
        _fullAccess = new AppUserBuilder("amelia", "amelia@example.com").Build();
        _restrictedAccess = new AppUserBuilder("mila", "mila@example.com").Build();
        _restrictedAgeAccess = new AppUserBuilder("eva", "eva@example.com").Build();
        _restrictedAgeAccess.AgeRestriction = AgeRating.Teen;
        _restrictedAgeAccess.AgeRestrictionIncludeUnknowns = true;

        Context.Users.Add(_fullAccess);
        Context.Users.Add(_restrictedAccess);
        Context.Users.Add(_restrictedAgeAccess);
        await Context.SaveChangesAsync();

        Context.Tag.Add(SharedSeriesChaptersTag);
        Context.Tag.Add(SharedSeriesTag);
        Context.Tag.Add(SharedChaptersTag);
        Context.Tag.Add(Lib0SeriesChaptersTag);
        Context.Tag.Add(Lib0SeriesTag);
        Context.Tag.Add(Lib0ChaptersTag);
        Context.Tag.Add(Lib1SeriesChaptersTag);
        Context.Tag.Add(Lib1SeriesTag);
        Context.Tag.Add(Lib1ChaptersTag);
        await Context.SaveChangesAsync();

        var lib0 = new LibraryBuilder("lib0")
            .WithSeries(new SeriesBuilder("lib0-s0")
                .WithMetadata(new SeriesMetadata
                {
                    Tags = [SharedSeriesChaptersTag, SharedSeriesTag, Lib0SeriesChaptersTag, Lib0SeriesTag]
                })
                .WithVolume(new VolumeBuilder("1")
                    .WithChapter(new ChapterBuilder("1")
                        .WithTags([SharedSeriesChaptersTag, SharedChaptersTag, Lib0SeriesChaptersTag, Lib0ChaptersTag])
                        .Build())
                    .WithChapter(new ChapterBuilder("2")
                        .WithTags([SharedSeriesChaptersTag, SharedChaptersTag, Lib1SeriesChaptersTag, Lib1ChaptersTag])
                        .Build())
                    .Build())
                .Build())
            .Build();
        var lib1 = new LibraryBuilder("lib1")
            .WithSeries(new SeriesBuilder("lib1-s0")
                .WithMetadata(new SeriesMetadata
                {
                    Tags = [SharedSeriesChaptersTag, SharedSeriesTag, Lib1SeriesChaptersTag, Lib1SeriesTag]
                })
                .WithVolume(new VolumeBuilder("1")
                    .WithChapter(new ChapterBuilder("1")
                        .WithTags([SharedSeriesChaptersTag, SharedChaptersTag, Lib1SeriesChaptersTag, Lib1ChaptersTag])
                        .Build())
                    .WithChapter(new ChapterBuilder("2")
                        .WithTags([SharedSeriesChaptersTag, SharedChaptersTag, Lib1SeriesChaptersTag, Lib1ChaptersTag, Lib1ChapterAgeTag])
                        .WithAgeRating(AgeRating.Mature17Plus)
                        .Build())
                    .Build())
                .Build())
            .Build();

        await Context.SaveChangesAsync();

        _fullAccess.Libraries.Add(lib0);
        _fullAccess.Libraries.Add(lib1);
        _restrictedAccess.Libraries.Add(lib1);
        _restrictedAgeAccess.Libraries.Add(lib1);

        await Context.SaveChangesAsync();
    }

    private Predicate<BrowseTagDto> ContainsTagCheck(Tag tag)
    {
        return t => t.Id == tag.Id;
    }

    [Fact]
    public async Task GetBrowseableTag()
    {
        await ResetDb();
        await SeedDb();

        var fullAccessTags = await UnitOfWork.TagRepository.GetBrowseableTag(_fullAccess.Id, new UserParams());
        Assert.Equal(AllTags.Count, fullAccessTags.TotalCount);
        foreach (var tag in AllTags)
        {
            Assert.Contains(fullAccessTags, ContainsTagCheck(tag));
        }

        Assert.Equal(2, fullAccessTags.First(dto => dto.Id == SharedSeriesChaptersTag.Id).SeriesCount);
        Assert.Equal(4, fullAccessTags.First(dto => dto.Id == SharedSeriesChaptersTag.Id).ChapterCount);
        Assert.Equal(1, fullAccessTags.First(dto => dto.Id == Lib0SeriesTag.Id).SeriesCount);


        var restrictedAccessTags = await UnitOfWork.TagRepository.GetBrowseableTag(_restrictedAccess.Id, new UserParams());

        // Should see: 3 shared + 4 library 1 specific = 7 tags
        Assert.Equal(7, restrictedAccessTags.TotalCount);

        // Verify Library 1 and shared tags are present
        Assert.Contains(restrictedAccessTags, ContainsTagCheck(SharedSeriesChaptersTag));
        Assert.Contains(restrictedAccessTags, ContainsTagCheck(SharedSeriesTag));
        Assert.Contains(restrictedAccessTags, ContainsTagCheck(SharedChaptersTag));
        Assert.Contains(restrictedAccessTags, ContainsTagCheck(Lib1SeriesChaptersTag));
        Assert.Contains(restrictedAccessTags, ContainsTagCheck(Lib1SeriesTag));
        Assert.Contains(restrictedAccessTags, ContainsTagCheck(Lib1ChaptersTag));
        Assert.Contains(restrictedAccessTags, ContainsTagCheck(Lib1ChapterAgeTag));

        // Verify Count is correctly limited
        Assert.Equal(1, restrictedAccessTags.First(dto => dto.Id == SharedSeriesChaptersTag.Id).SeriesCount);
        Assert.Equal(2, restrictedAccessTags.First(dto => dto.Id == SharedSeriesChaptersTag.Id).ChapterCount);
        Assert.Equal(1, restrictedAccessTags.First(dto => dto.Id == Lib1SeriesTag.Id).SeriesCount);


        var restrictedAgeAccessTags = await UnitOfWork.TagRepository.GetBrowseableTag(_restrictedAgeAccess.Id, new UserParams());

        // Should see: 3 shared + 3 library 1 specific = 6 tags
        Assert.Equal(6, restrictedAgeAccessTags.TotalCount);

        Assert.Contains(restrictedAccessTags, ContainsTagCheck(SharedSeriesChaptersTag));
        Assert.Contains(restrictedAccessTags, ContainsTagCheck(SharedSeriesTag));
        Assert.Contains(restrictedAccessTags, ContainsTagCheck(SharedChaptersTag));
        Assert.Contains(restrictedAccessTags, ContainsTagCheck(Lib1SeriesChaptersTag));
        Assert.Contains(restrictedAccessTags, ContainsTagCheck(Lib1SeriesTag));
        Assert.Contains(restrictedAccessTags, ContainsTagCheck(Lib1ChaptersTag));

        Assert.DoesNotContain(restrictedAgeAccessTags, ContainsTagCheck(Lib1ChapterAgeTag));

        // Verify Count is correctly limited
        Assert.Equal(1, restrictedAgeAccessTags.First(dto => dto.Id == SharedSeriesChaptersTag.Id).SeriesCount);
        Assert.Equal(1, restrictedAgeAccessTags.First(dto => dto.Id == SharedSeriesChaptersTag.Id).ChapterCount);
        Assert.Equal(1, restrictedAgeAccessTags.First(dto => dto.Id == Lib1SeriesTag.Id).SeriesCount);



    }
}
