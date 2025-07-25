using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using API.Constants;
using API.Data;
using API.Data.Repositories;
using API.DTOs.Collection;
using API.Entities;
using API.Entities.Enums;
using API.Helpers.Builders;
using API.Services;
using API.Services.Plus;
using API.SignalR;
using Kavita.Common;
using NSubstitute;
using Xunit;

namespace API.Tests.Services;

public class CollectionTagServiceTests : AbstractDbTest
{
    private readonly ICollectionTagService _service;
    public CollectionTagServiceTests()
    {
        _service = new CollectionTagService(UnitOfWork, Substitute.For<IEventHub>());
    }

    protected override async Task ResetDb()
    {
        Context.AppUserCollection.RemoveRange(Context.AppUserCollection.ToList());
        Context.Library.RemoveRange(Context.Library.ToList());

        await UnitOfWork.CommitAsync();
    }

    private async Task SeedSeries()
    {
        if (Context.AppUserCollection.Any()) return;

        var s1 = new SeriesBuilder("Series 1").WithMetadata(new SeriesMetadataBuilder().WithAgeRating(AgeRating.Mature).Build()).Build();
        var s2 = new SeriesBuilder("Series 2").WithMetadata(new SeriesMetadataBuilder().WithAgeRating(AgeRating.G).Build()).Build();
        Context.Library.Add(new LibraryBuilder("Library 2", LibraryType.Manga)
            .WithSeries(s1)
            .WithSeries(s2)
            .Build());

        var user = new AppUserBuilder("majora2007", "majora2007", Seed.DefaultThemes.First()).Build();
        user.Collections = new List<AppUserCollection>()
        {
            new AppUserCollectionBuilder("Tag 1").WithItems(new []{s1}).Build(),
            new AppUserCollectionBuilder("Tag 2").WithItems(new []{s1, s2}).WithIsPromoted(true).Build()
        };
        UnitOfWork.UserRepository.Add(user);

        await UnitOfWork.CommitAsync();
    }

    #region DeleteTag

    [Fact]
    public async Task DeleteTag_ShouldDeleteTag_WhenTagExists()
    {
        // Arrange
        await SeedSeries();

        var user = await UnitOfWork.UserRepository.GetUserByIdAsync(1, AppUserIncludes.Collections);
        Assert.NotNull(user);

        // Act
        var result = await _service.DeleteTag(1, user);

        // Assert
        Assert.True(result);
        var deletedTag = await UnitOfWork.CollectionTagRepository.GetCollectionAsync(1);
        Assert.Null(deletedTag);
        Assert.Single(user.Collections);  // Only one collection should remain
    }

    [Fact]
    public async Task DeleteTag_ShouldReturnTrue_WhenTagDoesNotExist()
    {
        // Arrange
        await SeedSeries();
        var user = await UnitOfWork.UserRepository.GetUserByIdAsync(1, AppUserIncludes.Collections);
        Assert.NotNull(user);

        // Act - Try to delete a non-existent tag
        var result = await _service.DeleteTag(999, user);

        // Assert
        Assert.True(result);  // Should return true because the tag is already "deleted"
        Assert.Equal(2, user.Collections.Count);  // Both collections should remain
    }

    [Fact]
    public async Task DeleteTag_ShouldNotAffectOtherTags()
    {
        // Arrange
        await SeedSeries();
        var user = await UnitOfWork.UserRepository.GetUserByIdAsync(1, AppUserIncludes.Collections);
        Assert.NotNull(user);

        // Act
        var result = await _service.DeleteTag(1, user);

        // Assert
        Assert.True(result);
        var remainingTag = await UnitOfWork.CollectionTagRepository.GetCollectionAsync(2);
        Assert.NotNull(remainingTag);
        Assert.Equal("Tag 2", remainingTag.Title);
        Assert.True(remainingTag.Promoted);
    }

    #endregion

    #region UpdateTag

    [Fact]
    public async Task UpdateTag_ShouldUpdateFields()
    {
        await SeedSeries();

        var user = await UnitOfWork.UserRepository.GetUserByIdAsync(1, AppUserIncludes.Collections);
        Assert.NotNull(user);

        user.Collections.Add(new AppUserCollectionBuilder("UpdateTag_ShouldUpdateFields").WithIsPromoted(true).Build());
        UnitOfWork.UserRepository.Update(user);
        await UnitOfWork.CommitAsync();

        await _service.UpdateTag(new AppUserCollectionDto()
        {
            Title = "UpdateTag_ShouldUpdateFields",
            Id = 3,
            Promoted = true,
            Summary = "Test Summary",
            AgeRating = AgeRating.Unknown
        }, 1);

        var tag = await UnitOfWork.CollectionTagRepository.GetCollectionAsync(3);
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

        var user = await UnitOfWork.UserRepository.GetUserByIdAsync(1, AppUserIncludes.Collections);
        Assert.NotNull(user);

        user.Collections.Add(new AppUserCollectionBuilder("UpdateTag_ShouldNotChangeTitle_WhenNotKavitaSource").WithSource(ScrobbleProvider.Mal).Build());
        UnitOfWork.UserRepository.Update(user);
        await UnitOfWork.CommitAsync();

        await _service.UpdateTag(new AppUserCollectionDto()
        {
            Title = "New Title",
            Id = 3,
            Promoted = true,
            Summary = "Test Summary",
            AgeRating = AgeRating.Unknown
        }, 1);

        var tag = await UnitOfWork.CollectionTagRepository.GetCollectionAsync(3);
        Assert.NotNull(tag);
        Assert.Equal("UpdateTag_ShouldNotChangeTitle_WhenNotKavitaSource", tag.Title);
        Assert.False(string.IsNullOrEmpty(tag.Summary));
    }

    [Fact]
    public async Task UpdateTag_ShouldThrowException_WhenTagDoesNotExist()
    {
        // Arrange
        await SeedSeries();

        // Act & Assert
        var exception = await Assert.ThrowsAsync<KavitaException>(() => _service.UpdateTag(new AppUserCollectionDto()
        {
            Title = "Non-existent Tag",
            Id = 999, // Non-existent ID
            Promoted = false
        }, 1));

        Assert.Equal("collection-doesnt-exist", exception.Message);
    }

    [Fact]
    public async Task UpdateTag_ShouldThrowException_WhenUserDoesNotOwnTag()
    {
        // Arrange
        await SeedSeries();

        // Create a second user
        var user2 = new AppUserBuilder("user2", "user2", Seed.DefaultThemes.First()).Build();
        UnitOfWork.UserRepository.Add(user2);
        await UnitOfWork.CommitAsync();

        // Act & Assert
        var exception = await Assert.ThrowsAsync<KavitaException>(() => _service.UpdateTag(new AppUserCollectionDto()
        {
            Title = "Tag 1",
            Id = 1, // This belongs to user1
            Promoted = false
        }, 2)); // User with ID 2

        Assert.Equal("access-denied", exception.Message);
    }

    [Fact]
    public async Task UpdateTag_ShouldThrowException_WhenTitleIsEmpty()
    {
        // Arrange
        await SeedSeries();

        // Act & Assert
        var exception = await Assert.ThrowsAsync<KavitaException>(() => _service.UpdateTag(new AppUserCollectionDto()
        {
            Title = "   ", // Empty after trimming
            Id = 1,
            Promoted = false
        }, 1));

        Assert.Equal("collection-tag-title-required", exception.Message);
    }

    [Fact]
    public async Task UpdateTag_ShouldThrowException_WhenTitleAlreadyExists()
    {
        // Arrange
        await SeedSeries();

        // Act & Assert
        var exception = await Assert.ThrowsAsync<KavitaException>(() => _service.UpdateTag(new AppUserCollectionDto()
        {
            Title = "Tag 2", // Already exists
            Id = 1, // Trying to rename Tag 1 to Tag 2
            Promoted = false
        }, 1));

        Assert.Equal("collection-tag-duplicate", exception.Message);
    }

    [Fact]
    public async Task UpdateTag_ShouldUpdateCoverImageSettings()
    {
        // Arrange
        await SeedSeries();

        // Act
        await _service.UpdateTag(new AppUserCollectionDto()
        {
            Title = "Tag 1",
            Id = 1,
            CoverImageLocked = true
        }, 1);

        // Assert
        var tag = await UnitOfWork.CollectionTagRepository.GetCollectionAsync(1);
        Assert.NotNull(tag);
        Assert.True(tag.CoverImageLocked);

        // Now test unlocking the cover image
        await _service.UpdateTag(new AppUserCollectionDto()
        {
            Title = "Tag 1",
            Id = 1,
            CoverImageLocked = false
        }, 1);

        tag = await UnitOfWork.CollectionTagRepository.GetCollectionAsync(1);
        Assert.NotNull(tag);
        Assert.False(tag.CoverImageLocked);
        Assert.Equal(string.Empty, tag.CoverImage);
    }

    [Fact]
    public async Task UpdateTag_ShouldAllowPromoteForAdminRole()
    {
        // Arrange
        await SeedSeries();

        // Setup a user with admin role
        var user = await UnitOfWork.UserRepository.GetUserByIdAsync(1, AppUserIncludes.Collections);
        Assert.NotNull(user);
        await AddUserWithRole(user.Id, PolicyConstants.AdminRole);


        // Act - Try to promote a tag that wasn't previously promoted
        await _service.UpdateTag(new AppUserCollectionDto()
        {
            Title = "Tag 1",
            Id = 1,
            Promoted = true
        }, 1);

        // Assert
        var tag = await UnitOfWork.CollectionTagRepository.GetCollectionAsync(1);
        Assert.NotNull(tag);
        Assert.True(tag.Promoted);
    }

    [Fact]
    public async Task UpdateTag_ShouldAllowPromoteForPromoteRole()
    {
        // Arrange
        await SeedSeries();

        // Setup a user with promote role
        var user = await UnitOfWork.UserRepository.GetUserByIdAsync(1, AppUserIncludes.Collections);
        Assert.NotNull(user);

        // Mock to return promote role for the user
        await AddUserWithRole(user.Id, PolicyConstants.PromoteRole);

        // Act - Try to promote a tag that wasn't previously promoted
        await _service.UpdateTag(new AppUserCollectionDto()
        {
            Title = "Tag 1",
            Id = 1,
            Promoted = true
        }, 1);

        // Assert
        var tag = await UnitOfWork.CollectionTagRepository.GetCollectionAsync(1);
        Assert.NotNull(tag);
        Assert.True(tag.Promoted);
    }

    [Fact]
    public async Task UpdateTag_ShouldNotChangePromotion_WhenUserHasNoPermission()
    {
        // Arrange
        await SeedSeries();

        // Setup a user with no special roles
        var user = await UnitOfWork.UserRepository.GetUserByIdAsync(1, AppUserIncludes.Collections);
        Assert.NotNull(user);

        // Act - Try to promote a tag without proper role
        await _service.UpdateTag(new AppUserCollectionDto()
        {
            Title = "Tag 1",
            Id = 1,
            Promoted = true
        }, 1);

        // Assert
        var tag = await UnitOfWork.CollectionTagRepository.GetCollectionAsync(1);
        Assert.NotNull(tag);
        Assert.False(tag.Promoted); // Should remain unpromoted
    }
    #endregion


    #region RemoveTagFromSeries

    [Fact]
    public async Task RemoveTagFromSeries_RemoveSeriesFromTag()
    {
        await SeedSeries();

        var user = await UnitOfWork.UserRepository.GetUserByIdAsync(1, AppUserIncludes.Collections);
        Assert.NotNull(user);

        // Tag 2 has 2 series
        var tag = await UnitOfWork.CollectionTagRepository.GetCollectionAsync(2);
        Assert.NotNull(tag);

        await _service.RemoveTagFromSeries(tag, new[] {1});
        var userCollections = await UnitOfWork.UserRepository.GetUserByIdAsync(1, AppUserIncludes.Collections);
        Assert.Equal(2, userCollections!.Collections.Count);
        Assert.Single(tag.Items);
        Assert.Equal(2, tag.Items.First().Id);
    }

    /// <summary>
    /// Ensure the rating of the tag updates after a series change
    /// </summary>
    [Fact]
    public async Task RemoveTagFromSeries_RemoveSeriesFromTag_UpdatesRating()
    {
        await SeedSeries();

        var user = await UnitOfWork.UserRepository.GetUserByIdAsync(1, AppUserIncludes.Collections);
        Assert.NotNull(user);

        // Tag 2 has 2 series
        var tag = await UnitOfWork.CollectionTagRepository.GetCollectionAsync(2);
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

        var user = await UnitOfWork.UserRepository.GetUserByIdAsync(1, AppUserIncludes.Collections);
        Assert.NotNull(user);

        // Tag 1 has 1 series
        var tag = await UnitOfWork.CollectionTagRepository.GetCollectionAsync(1);
        Assert.NotNull(tag);

        await _service.RemoveTagFromSeries(tag, new[] {1});
        var tag2 = await UnitOfWork.CollectionTagRepository.GetCollectionAsync(1);
        Assert.Null(tag2);
    }

    [Fact]
    public async Task RemoveTagFromSeries_ShouldReturnFalse_WhenTagIsNull()
    {
        // Act
        var result = await _service.RemoveTagFromSeries(null, [1]);

        // Assert
        Assert.False(result);
    }

    [Fact]
    public async Task RemoveTagFromSeries_ShouldHandleEmptySeriesIdsList()
    {
        // Arrange
        await SeedSeries();

        var tag = await UnitOfWork.CollectionTagRepository.GetCollectionAsync(1);
        Assert.NotNull(tag);
        var initialItemCount = tag.Items.Count;

        // Act
        var result = await _service.RemoveTagFromSeries(tag, Array.Empty<int>());

        // Assert
        Assert.True(result);
        tag = await UnitOfWork.CollectionTagRepository.GetCollectionAsync(1);
        Assert.NotNull(tag);
        Assert.Equal(initialItemCount, tag.Items.Count); // No items should be removed
    }

    [Fact]
    public async Task RemoveTagFromSeries_ShouldHandleNonExistentSeriesIds()
    {
        // Arrange
        await SeedSeries();

        var tag = await UnitOfWork.CollectionTagRepository.GetCollectionAsync(1);
        Assert.NotNull(tag);
        var initialItemCount = tag.Items.Count;

        // Act - Try to remove a series that doesn't exist in the tag
        var result = await _service.RemoveTagFromSeries(tag, [999]);

        // Assert
        Assert.True(result);
        tag = await UnitOfWork.CollectionTagRepository.GetCollectionAsync(1);
        Assert.NotNull(tag);
        Assert.Equal(initialItemCount, tag.Items.Count); // No items should be removed
    }

    [Fact]
    public async Task RemoveTagFromSeries_ShouldHandleNullItemsList()
    {
        // Arrange
        await SeedSeries();

        var tag = await UnitOfWork.CollectionTagRepository.GetCollectionAsync(1);
        Assert.NotNull(tag);

        // Force null items list
        tag.Items = null;
        UnitOfWork.CollectionTagRepository.Update(tag);
        await UnitOfWork.CommitAsync();

        // Act
        var result = await _service.RemoveTagFromSeries(tag, [1]);

        // Assert
        Assert.True(result);
        // The tag should not be removed since the items list was null, not empty
        var tagAfter = await UnitOfWork.CollectionTagRepository.GetCollectionAsync(1);
        Assert.Null(tagAfter);
    }

    [Fact]
    public async Task RemoveTagFromSeries_ShouldUpdateAgeRating_WhenMultipleSeriesRemain()
    {
        // Arrange
        await SeedSeries();

        // Add a third series with a different age rating
        var s3 = new SeriesBuilder("Series 3").WithMetadata(new SeriesMetadataBuilder().WithAgeRating(AgeRating.PG).Build()).Build();
        Context.Library.First().Series.Add(s3);
        await UnitOfWork.CommitAsync();

        // Add series 3 to tag 2
        var tag = await UnitOfWork.CollectionTagRepository.GetCollectionAsync(2);
        Assert.NotNull(tag);
        tag.Items.Add(s3);
        UnitOfWork.CollectionTagRepository.Update(tag);
        await UnitOfWork.CommitAsync();

        // Act - Remove the series with Mature rating
        await _service.RemoveTagFromSeries(tag, new[] {1});

        // Assert
        tag = await UnitOfWork.CollectionTagRepository.GetCollectionAsync(2);
        Assert.NotNull(tag);
        Assert.Equal(2, tag.Items.Count);

        // The age rating should be updated to the highest remaining rating (PG)
        Assert.Equal(AgeRating.PG, tag.AgeRating);
    }


    #endregion

}
