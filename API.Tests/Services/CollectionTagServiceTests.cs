using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using API.Data;
using API.Data.Repositories;
using API.DTOs.Collection;
using API.Entities;
using API.Entities.Enums;
using API.Helpers.Builders;
using API.Services;
using API.Services.Plus;
using API.SignalR;
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
        _context.AppUserCollection.RemoveRange(_context.AppUserCollection.ToList());
        _context.Library.RemoveRange(_context.Library.ToList());

        await _unitOfWork.CommitAsync();
    }

    private async Task SeedSeries()
    {
        if (_context.AppUserCollection.Any()) return;

        var s1 = new SeriesBuilder("Series 1").WithMetadata(new SeriesMetadataBuilder().WithAgeRating(AgeRating.Mature).Build()).Build();
        var s2 = new SeriesBuilder("Series 2").WithMetadata(new SeriesMetadataBuilder().WithAgeRating(AgeRating.G).Build()).Build();
        _context.Library.Add(new LibraryBuilder("Library 2", LibraryType.Manga)
            .WithSeries(s1)
            .WithSeries(s2)
            .Build());

        var user = new AppUserBuilder("majora2007", "majora2007", Seed.DefaultThemes.First()).Build();
        user.Collections = new List<AppUserCollection>()
        {
            new AppUserCollectionBuilder("Tag 1").WithItems(new []{s1}).Build(),
            new AppUserCollectionBuilder("Tag 2").WithItems(new []{s1, s2}).WithIsPromoted(true).Build()
        };
        _unitOfWork.UserRepository.Add(user);

        await _unitOfWork.CommitAsync();
    }

    #region UpdateTag

    [Fact]
    public async Task UpdateTag_ShouldUpdateFields()
    {
        await SeedSeries();

        var user = await _unitOfWork.UserRepository.GetUserByIdAsync(1, AppUserIncludes.Collections);
        Assert.NotNull(user);

        user.Collections.Add(new AppUserCollectionBuilder("UpdateTag_ShouldUpdateFields").WithIsPromoted(true).Build());
        _unitOfWork.UserRepository.Update(user);
        await _unitOfWork.CommitAsync();

        await _service.UpdateTag(new AppUserCollectionDto()
        {
            Title = "UpdateTag_ShouldUpdateFields",
            Id = 3,
            Promoted = true,
            Summary = "Test Summary",
            AgeRating = AgeRating.Unknown
        }, 1);

        var tag = await _unitOfWork.CollectionTagRepository.GetCollectionAsync(3);
        Assert.NotNull(tag);
        Assert.True(tag.Promoted);
        Assert.False(string.IsNullOrEmpty(tag.Summary));
    }

    /// <summary>
    /// UpdateTag should not change any title if non-Kavita source
    /// </summary>
    [Fact]
    public async Task UpdateTag_ShouldNotChangeTitle_WhenNotKavitaSource()
    {
        await SeedSeries();

        var user = await _unitOfWork.UserRepository.GetUserByIdAsync(1, AppUserIncludes.Collections);
        Assert.NotNull(user);

        user.Collections.Add(new AppUserCollectionBuilder("UpdateTag_ShouldNotChangeTitle_WhenNotKavitaSource").WithSource(ScrobbleProvider.Mal).Build());
        _unitOfWork.UserRepository.Update(user);
        await _unitOfWork.CommitAsync();

        await _service.UpdateTag(new AppUserCollectionDto()
        {
            Title = "New Title",
            Id = 3,
            Promoted = true,
            Summary = "Test Summary",
            AgeRating = AgeRating.Unknown
        }, 1);

        var tag = await _unitOfWork.CollectionTagRepository.GetCollectionAsync(3);
        Assert.NotNull(tag);
        Assert.Equal("UpdateTag_ShouldNotChangeTitle_WhenNotKavitaSource", tag.Title);
        Assert.False(string.IsNullOrEmpty(tag.Summary));
    }
    #endregion


    #region RemoveTagFromSeries

    [Fact]
    public async Task RemoveTagFromSeries_RemoveSeriesFromTag()
    {
        await SeedSeries();

        var user = await _unitOfWork.UserRepository.GetUserByIdAsync(1, AppUserIncludes.Collections);
        Assert.NotNull(user);

        // Tag 2 has 2 series
        var tag = await _unitOfWork.CollectionTagRepository.GetCollectionAsync(2);
        Assert.NotNull(tag);

        await _service.RemoveTagFromSeries(tag, new[] {1});
        var userCollections = await _unitOfWork.UserRepository.GetUserByIdAsync(1, AppUserIncludes.Collections);
        Assert.Equal(2, userCollections!.Collections.Count);
        Assert.Equal(1, tag.Items.Count);
        Assert.Equal(2, tag.Items.First().Id);
    }

    /// <summary>
    /// Ensure the rating of the tag updates after a series change
    /// </summary>
    [Fact]
    public async Task RemoveTagFromSeries_RemoveSeriesFromTag_UpdatesRating()
    {
        await SeedSeries();

        var user = await _unitOfWork.UserRepository.GetUserByIdAsync(1, AppUserIncludes.Collections);
        Assert.NotNull(user);

        // Tag 2 has 2 series
        var tag = await _unitOfWork.CollectionTagRepository.GetCollectionAsync(2);
        Assert.NotNull(tag);

        await _service.RemoveTagFromSeries(tag, new[] {1});

        Assert.Equal(AgeRating.G, tag.AgeRating);
    }

    /// <summary>
    /// Should remove the tag when there are no items left on the tag
    /// </summary>
    [Fact]
    public async Task RemoveTagFromSeries_RemoveSeriesFromTag_DeleteTagWhenNoSeriesLeft()
    {
        await SeedSeries();

        var user = await _unitOfWork.UserRepository.GetUserByIdAsync(1, AppUserIncludes.Collections);
        Assert.NotNull(user);

        // Tag 1 has 1 series
        var tag = await _unitOfWork.CollectionTagRepository.GetCollectionAsync(1);
        Assert.NotNull(tag);

        await _service.RemoveTagFromSeries(tag, new[] {1});
        var tag2 = await _unitOfWork.CollectionTagRepository.GetCollectionAsync(1);
        Assert.Null(tag2);
    }

    #endregion

}
