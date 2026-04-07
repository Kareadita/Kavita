using Kavita.Common.Extensions;
using Kavita.Database.Tests;
using Kavita.Models.Builders;
using Kavita.Models.Entities;
using Kavita.Models.Entities.Enums;
using Kavita.Services.Builders;
using Kavita.Services.Helpers;
using Xunit.Abstractions;

namespace Kavita.Services.Tests.Helpers;

public class TagHelperTests(ITestOutputHelper outputHelper) : AbstractDbTest(outputHelper)
{
    #region UpdateEntityTags

    [Fact]
    public async Task UpdateEntityTags_AddsNewTags()
    {
        var (unitOfWork, context, _) = await CreateDatabase();

        var library = new LibraryBuilder("My Library").Build();
        unitOfWork.LibraryRepository.Add(library);
        await unitOfWork.CommitAsync();

        var chapter = new ChapterBuilder("1").Build();
        var series = new SeriesBuilder("Test")
            .WithLibraryId(library.Id)
            .WithFormat(MangaFormat.Archive)
            .WithVolume(new VolumeBuilder("1").WithChapter(chapter).Build())
            .Build();

        unitOfWork.SeriesRepository.Add(series);
        await unitOfWork.CommitAsync();

        await TagHelper.UpdateEntityTags(chapter.Tags, new[] { "Action", "Comedy" }, context.Tag, unitOfWork);

        Assert.Equal(2, chapter.Tags.Count);
        Assert.Contains(chapter.Tags, t => t.NormalizedTitle == "Action".ToNormalized());
        Assert.Contains(chapter.Tags, t => t.NormalizedTitle == "Comedy".ToNormalized());
    }

    [Fact]
    public async Task UpdateEntityTags_RemovesStaleTags()
    {
        var (unitOfWork, context, _) = await CreateDatabase();

        var library = new LibraryBuilder("My Library").Build();
        unitOfWork.LibraryRepository.Add(library);
        await unitOfWork.CommitAsync();

        var chapter = new ChapterBuilder("1").Build();
        var series = new SeriesBuilder("Test")
            .WithLibraryId(library.Id)
            .WithFormat(MangaFormat.Archive)
            .WithVolume(new VolumeBuilder("1").WithChapter(chapter).Build())
            .Build();

        unitOfWork.SeriesRepository.Add(series);
        await unitOfWork.CommitAsync();

        // First add two tags
        await TagHelper.UpdateEntityTags(chapter.Tags, new[] { "Action", "Comedy" }, context.Tag, unitOfWork);
        Assert.Equal(2, chapter.Tags.Count);

        // Now update to only keep "Action"
        await TagHelper.UpdateEntityTags(chapter.Tags, new[] { "Action" }, context.Tag, unitOfWork);

        Assert.Single(chapter.Tags);
        Assert.Contains(chapter.Tags, t => t.NormalizedTitle == "Action".ToNormalized());
    }

    [Fact]
    public async Task UpdateEntityTags_DeduplicatesByNormalizedTitle()
    {
        var (unitOfWork, context, _) = await CreateDatabase();

        var library = new LibraryBuilder("My Library").Build();
        unitOfWork.LibraryRepository.Add(library);
        await unitOfWork.CommitAsync();

        var chapter = new ChapterBuilder("1").Build();
        var series = new SeriesBuilder("Test")
            .WithLibraryId(library.Id)
            .WithFormat(MangaFormat.Archive)
            .WithVolume(new VolumeBuilder("1").WithChapter(chapter).Build())
            .Build();

        unitOfWork.SeriesRepository.Add(series);
        await unitOfWork.CommitAsync();

        await TagHelper.UpdateEntityTags(chapter.Tags, new[] { "DC Comics", "dc comics" }, context.Tag, unitOfWork);

        Assert.Single(chapter.Tags);
    }

    [Fact]
    public async Task UpdateEntityTags_ReusesExistingDbTags()
    {
        var (unitOfWork, context, _) = await CreateDatabase();

        var library = new LibraryBuilder("My Library").Build();
        unitOfWork.LibraryRepository.Add(library);
        await unitOfWork.CommitAsync();

        // Pre-create a tag in the database
        var existingTag = new TagBuilder("Action").Build();
        context.Tag.Add(existingTag);
        await unitOfWork.CommitAsync();
        var existingTagId = existingTag.Id;

        var chapter = new ChapterBuilder("1").Build();
        var series = new SeriesBuilder("Test")
            .WithLibraryId(library.Id)
            .WithFormat(MangaFormat.Archive)
            .WithVolume(new VolumeBuilder("1").WithChapter(chapter).Build())
            .Build();

        unitOfWork.SeriesRepository.Add(series);
        await unitOfWork.CommitAsync();

        await TagHelper.UpdateEntityTags(chapter.Tags, new[] { "Action" }, context.Tag, unitOfWork);

        Assert.Single(chapter.Tags);
        Assert.Equal(existingTagId, chapter.Tags.First().Id);
    }

    [Fact]
    public async Task UpdateEntityTags_HandlesEmptyInput()
    {
        var (unitOfWork, context, _) = await CreateDatabase();

        var library = new LibraryBuilder("My Library").Build();
        unitOfWork.LibraryRepository.Add(library);
        await unitOfWork.CommitAsync();

        var chapter = new ChapterBuilder("1").Build();
        var series = new SeriesBuilder("Test")
            .WithLibraryId(library.Id)
            .WithFormat(MangaFormat.Archive)
            .WithVolume(new VolumeBuilder("1").WithChapter(chapter).Build())
            .Build();

        unitOfWork.SeriesRepository.Add(series);
        await unitOfWork.CommitAsync();

        // Add tags first
        await TagHelper.UpdateEntityTags(chapter.Tags, new[] { "Action", "Comedy" }, context.Tag, unitOfWork);
        Assert.Equal(2, chapter.Tags.Count);

        // Empty input should remove all tags
        await TagHelper.UpdateEntityTags(chapter.Tags, Array.Empty<string>(), context.Tag, unitOfWork);

        Assert.Empty(chapter.Tags);
    }

    [Fact]
    public async Task UpdateEntityTags_WorksWithGenres()
    {
        var (unitOfWork, context, _) = await CreateDatabase();

        var library = new LibraryBuilder("My Library").Build();
        unitOfWork.LibraryRepository.Add(library);
        await unitOfWork.CommitAsync();

        var chapter = new ChapterBuilder("1").Build();
        var series = new SeriesBuilder("Test")
            .WithLibraryId(library.Id)
            .WithFormat(MangaFormat.Archive)
            .WithVolume(new VolumeBuilder("1").WithChapter(chapter).Build())
            .Build();

        unitOfWork.SeriesRepository.Add(series);
        await unitOfWork.CommitAsync();

        await TagHelper.UpdateEntityTags(chapter.Genres, new[] { "Fantasy", "Sci-Fi" }, context.Genre, unitOfWork);

        Assert.Equal(2, chapter.Genres.Count);
        Assert.Contains(chapter.Genres, g => g.NormalizedTitle == "Fantasy".ToNormalized());
        Assert.Contains(chapter.Genres, g => g.NormalizedTitle == "Sci-Fi".ToNormalized());
    }

    #endregion

    #region UpdateTagList

    [Fact]
    public void UpdateTagList_AddsNewTags()
    {
        var existingEntityTags = new List<Tag>();
        var allDbTags = new List<Tag>
        {
            new TagBuilder("Action").Build(),
            new TagBuilder("Comedy").Build()
        };
        var addedTags = new List<Tag>();

        TagHelper.UpdateTagList(
            new List<string> { "Action", "Comedy" },
            existingEntityTags,
            allDbTags,
            tag => addedTags.Add(tag),
            () => { });

        Assert.Equal(2, addedTags.Count);
    }

    [Fact]
    public void UpdateTagList_RemovesStaleTags()
    {
        var existingEntityTags = new List<Tag>
        {
            new TagBuilder("Action").Build(),
            new TagBuilder("Comedy").Build()
        };
        var allDbTags = new List<Tag> { new TagBuilder("Action").Build() };

        TagHelper.UpdateTagList(
            new List<string> { "Action" },
            existingEntityTags,
            allDbTags,
            tag => existingEntityTags.Add(tag),
            () => { });

        // Comedy should be removed
        Assert.Single(existingEntityTags);
        Assert.Contains(existingEntityTags, t => t.NormalizedTitle == "Action".ToNormalized());
    }

    [Fact]
    public void UpdateTagList_ReusesExistingFromAllDbTags()
    {
        var existingEntityTags = new List<Tag>();
        var dbTag = new TagBuilder("Action").Build();
        var allDbTags = new List<Tag> { dbTag };
        var addedTags = new List<Tag>();

        TagHelper.UpdateTagList(
            new List<string> { "Action" },
            existingEntityTags,
            allDbTags,
            tag => addedTags.Add(tag),
            () => { });

        Assert.Single(addedTags);
        Assert.Same(dbTag, addedTags[0]);
    }

    [Fact]
    public void UpdateTagList_CreatesNewWhenNotInDb()
    {
        var existingEntityTags = new List<Tag>();
        var allDbTags = new List<Tag>();
        var addedTags = new List<Tag>();

        TagHelper.UpdateTagList(
            new List<string> { "Brand New Tag" },
            existingEntityTags,
            allDbTags,
            tag => addedTags.Add(tag),
            () => { });

        Assert.Single(addedTags);
        Assert.Equal("Brand New Tag".ToNormalized(), addedTags[0].NormalizedTitle);
    }

    [Fact]
    public void UpdateTagList_NullInput_NoOp()
    {
        var existingEntityTags = new List<Tag>
        {
            new TagBuilder("Action").Build()
        };
        var modified = false;

        TagHelper.UpdateTagList<Tag>(
            null,
            existingEntityTags,
            new List<Tag>(),
            _ => { },
            () => modified = true);

        Assert.False(modified);
        Assert.Single(existingEntityTags);
    }

    [Fact]
    public void UpdateTagList_CallsOnModified_WhenChanged()
    {
        var existingEntityTags = new List<Tag>();
        var allDbTags = new List<Tag> { new TagBuilder("Action").Build() };
        var modified = false;

        TagHelper.UpdateTagList(
            new List<string> { "Action" },
            existingEntityTags,
            allDbTags,
            tag => existingEntityTags.Add(tag),
            () => modified = true);

        Assert.True(modified);
    }

    [Fact]
    public void UpdateTagList_NoCallOnModified_WhenUnchanged()
    {
        var existingTag = new TagBuilder("Action").Build();
        var existingEntityTags = new List<Tag> { existingTag };
        var allDbTags = new List<Tag> { existingTag };
        var modified = false;

        TagHelper.UpdateTagList(
            new List<string> { "Action" },
            existingEntityTags,
            allDbTags,
            tag => existingEntityTags.Add(tag),
            () => modified = true);

        Assert.False(modified);
    }

    #endregion
}
