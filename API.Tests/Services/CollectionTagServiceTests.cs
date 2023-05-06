using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using API.Data;
using API.Data.Repositories;
using API.DTOs.CollectionTags;
using API.Entities;
using API.Entities.Enums;
using API.Helpers.Builders;
using API.Services;
using API.SignalR;
using API.Tests.Helpers;
using NSubstitute;
using Xunit;

namespace API.Tests.Services;

public class CollectionTagServiceTests : AbstractDbTest
{
    private readonly ICollectionTagService _service;
    public CollectionTagServiceTests()
    {
        _service = new CollectionTagService(_unitOfWork, Substitute.For<IEventHub>());
    }

    protected override async Task ResetDb()
    {
        _context.CollectionTag.RemoveRange(_context.CollectionTag.ToList());
        _context.Library.RemoveRange(_context.Library.ToList());

        await _unitOfWork.CommitAsync();
    }

    private async Task SeedSeries()
    {
        if (_context.CollectionTag.Any()) return;

        _context.Library.Add(new LibraryBuilder("Library 2", LibraryType.Manga)
            .WithSeries(new SeriesBuilder("Series 1").Build())
            .WithSeries(new SeriesBuilder("Series 2").Build())
            .Build());

        _context.CollectionTag.Add(new CollectionTagBuilder("Tag 1").Build());
        _context.CollectionTag.Add(new CollectionTagBuilder("Tag 2").WithIsPromoted(true).Build());
        await _unitOfWork.CommitAsync();
    }


    [Fact]
    public async Task TagExistsByName_ShouldFindTag()
    {
        await SeedSeries();
        Assert.True(await _service.TagExistsByName("Tag 1"));
        Assert.True(await _service.TagExistsByName("tag 1"));
        Assert.False(await _service.TagExistsByName("tag5"));
    }

    [Fact]
    public async Task UpdateTag_ShouldUpdateFields()
    {
        await SeedSeries();

        _context.CollectionTag.Add(new CollectionTagBuilder("UpdateTag_ShouldUpdateFields").WithId(3).WithIsPromoted(true).Build());
        await _unitOfWork.CommitAsync();

        await _service.UpdateTag(new CollectionTagDto()
        {
            Title = "UpdateTag_ShouldUpdateFields",
            Id = 3,
            Promoted = true,
            Summary = "Test Summary",
        });

        var tag = await _unitOfWork.CollectionTagRepository.GetTagAsync(3);
        Assert.NotNull(tag);
        Assert.True(tag.Promoted);
        Assert.True(!string.IsNullOrEmpty(tag.Summary));
    }

    [Fact]
    public async Task AddTagToSeries_ShouldAddTagToAllSeries()
    {
        await SeedSeries();
        var ids = new[] {1, 2};
        await _service.AddTagToSeries(await _unitOfWork.CollectionTagRepository.GetTagAsync(1, CollectionTagIncludes.SeriesMetadata), ids);

        var metadatas = await _unitOfWork.SeriesRepository.GetSeriesMetadataForIdsAsync(ids);
        Assert.True(metadatas.ElementAt(0).CollectionTags.Any(t => t.Title.Equals("Tag 1")));
        Assert.True(metadatas.ElementAt(1).CollectionTags.Any(t => t.Title.Equals("Tag 1")));
    }

    [Fact]
    public async Task RemoveTagFromSeries_ShouldRemoveMultiple()
    {
        await SeedSeries();
        var ids = new[] {1, 2};
        var tag = await _unitOfWork.CollectionTagRepository.GetTagAsync(2, CollectionTagIncludes.SeriesMetadata);
        await _service.AddTagToSeries(tag, ids);

        await _service.RemoveTagFromSeries(tag, new[] {1});

        var metadatas = await _unitOfWork.SeriesRepository.GetSeriesMetadataForIdsAsync(new[] {1});

        Assert.Single(metadatas);
        Assert.Empty(metadatas.First().CollectionTags);
        Assert.NotEmpty(await _unitOfWork.SeriesRepository.GetSeriesMetadataForIdsAsync(new[] {2}));
    }

    [Fact]
    public async Task GetTagOrCreate_ShouldReturnNewTag()
    {
        await SeedSeries();
        var tag = await _service.GetTagOrCreate(0, "GetTagOrCreate_ShouldReturnNewTag");
        Assert.NotNull(tag);
        Assert.NotSame(0, tag.Id);
    }

    [Fact]
    public async Task GetTagOrCreate_ShouldReturnExistingTag()
    {
        await SeedSeries();
        var tag = await _service.GetTagOrCreate(1, string.Empty);
        Assert.NotNull(tag);
        Assert.NotSame(1, tag.Id);
    }

    [Fact]
    public async Task RemoveTagsWithoutSeries_ShouldRemoveAbandonedEntries()
    {
        await SeedSeries();
        // Setup a tag with one series
        var tag = await _service.GetTagOrCreate(0, "Tag with a series");
        await _unitOfWork.CommitAsync();

        var metadatas = await _unitOfWork.SeriesRepository.GetSeriesMetadataForIdsAsync(new[] {1});
        tag.SeriesMetadatas.Add(metadatas.First());
        var tagId = tag.Id;
        await _unitOfWork.CommitAsync();

        // Validate it doesn't remove tags it shouldn't
        await _service.RemoveTagsWithoutSeries();
        Assert.NotNull(await _unitOfWork.CollectionTagRepository.GetTagAsync(tagId));

        await _service.RemoveTagFromSeries(tag, new[] {1});

        // Validate it does remove tags it should
        await _service.RemoveTagsWithoutSeries();
        Assert.Null(await _unitOfWork.CollectionTagRepository.GetTagAsync(tagId));
    }
}
