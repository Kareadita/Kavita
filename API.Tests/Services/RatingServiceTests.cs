using System.Linq;
using System.Threading.Tasks;
using API.Data.Repositories;
using API.DTOs;
using API.Entities.Enums;
using API.Helpers.Builders;
using API.Services;
using API.Services.Plus;
using Hangfire;
using Hangfire.InMemory;
using Microsoft.Extensions.Logging;
using NSubstitute;
using Xunit;

namespace API.Tests.Services;

public class RatingServiceTests: AbstractDbTest
{
    private readonly RatingService _ratingService;

    public RatingServiceTests()
    {
        _ratingService = new RatingService(UnitOfWork, Substitute.For<IScrobblingService>(), Substitute.For<ILogger<RatingService>>());
    }

    [Fact]
    public async Task UpdateRating_ShouldSetRating()
    {
        await ResetDb();

        Context.Library.Add(new LibraryBuilder("Test LIb")
            .WithAppUser(new AppUserBuilder("majora2007", string.Empty).Build())
            .WithSeries(new SeriesBuilder("Test")

                .WithVolume(new VolumeBuilder("1")
                    .WithChapter(new ChapterBuilder("1").WithPages(1).Build())
                    .Build())
                .Build())
            .Build());


        await Context.SaveChangesAsync();


        var user = await UnitOfWork.UserRepository.GetUserByUsernameAsync("majora2007", AppUserIncludes.Ratings);

        JobStorage.Current = new InMemoryStorage();
        var result = await _ratingService.UpdateSeriesRating(user, new UpdateRatingDto
        {
            SeriesId = 1,
            UserRating = 3,
        });

        Assert.True(result);

        var ratings = (await UnitOfWork.UserRepository.GetUserByUsernameAsync("majora2007", AppUserIncludes.Ratings))!
            .Ratings;
        Assert.NotEmpty(ratings);
        Assert.Equal(3, ratings.First().Rating);
    }

    [Fact]
    public async Task UpdateRating_ShouldUpdateExistingRating()
    {
        await ResetDb();

        Context.Library.Add(new LibraryBuilder("Test LIb")
            .WithAppUser(new AppUserBuilder("majora2007", string.Empty).Build())
            .WithSeries(new SeriesBuilder("Test")

                .WithVolume(new VolumeBuilder("1")
                    .WithChapter(new ChapterBuilder("1").WithPages(1).Build())
                    .Build())
                .Build())
            .Build());


        await Context.SaveChangesAsync();

        var user = await UnitOfWork.UserRepository.GetUserByUsernameAsync("majora2007", AppUserIncludes.Ratings);

        var result = await _ratingService.UpdateSeriesRating(user, new UpdateRatingDto
        {
            SeriesId = 1,
            UserRating = 3,
        });

        Assert.True(result);

        JobStorage.Current = new InMemoryStorage();
        var ratings = (await UnitOfWork.UserRepository.GetUserByUsernameAsync("majora2007", AppUserIncludes.Ratings))
            .Ratings;
        Assert.NotEmpty(ratings);
        Assert.Equal(3, ratings.First().Rating);

        // Update the DB again

        var result2 = await _ratingService.UpdateSeriesRating(user, new UpdateRatingDto
        {
            SeriesId = 1,
            UserRating = 5,
        });

        Assert.True(result2);

        var ratings2 = (await UnitOfWork.UserRepository.GetUserByUsernameAsync("majora2007", AppUserIncludes.Ratings))
            .Ratings;
        Assert.NotEmpty(ratings2);
        Assert.True(ratings2.Count == 1);
        Assert.Equal(5, ratings2.First().Rating);
    }

    [Fact]
    public async Task UpdateRating_ShouldClampRatingAt5()
    {
        await ResetDb();

        Context.Library.Add(new LibraryBuilder("Test LIb")
            .WithAppUser(new AppUserBuilder("majora2007", string.Empty).Build())
            .WithSeries(new SeriesBuilder("Test")

                .WithVolume(new VolumeBuilder("1")
                    .WithChapter(new ChapterBuilder("1").WithPages(1).Build())
                    .Build())
                .Build())
            .Build());

        await Context.SaveChangesAsync();

        var user = await UnitOfWork.UserRepository.GetUserByUsernameAsync("majora2007", AppUserIncludes.Ratings);

        var result = await _ratingService.UpdateSeriesRating(user, new UpdateRatingDto
        {
            SeriesId = 1,
            UserRating = 10,
        });

        Assert.True(result);

        JobStorage.Current = new InMemoryStorage();
        var ratings = (await UnitOfWork.UserRepository.GetUserByUsernameAsync("majora2007",
                AppUserIncludes.Ratings)!)
            .Ratings;
        Assert.NotEmpty(ratings);
        Assert.Equal(5, ratings.First().Rating);
    }

    [Fact]
    public async Task UpdateRating_ShouldReturnFalseWhenSeriesDoesntExist()
    {
        await ResetDb();

        Context.Library.Add(new LibraryBuilder("Test LIb", LibraryType.Book)
            .WithAppUser(new AppUserBuilder("majora2007", string.Empty).Build())
            .WithSeries(new SeriesBuilder("Test")

                .WithVolume(new VolumeBuilder("1")
                    .WithChapter(new ChapterBuilder("1").WithPages(1).Build())
                    .Build())
                .Build())
            .Build());

        await Context.SaveChangesAsync();

        var user = await UnitOfWork.UserRepository.GetUserByUsernameAsync("majora2007", AppUserIncludes.Ratings);

        var result = await _ratingService.UpdateSeriesRating(user, new UpdateRatingDto
        {
            SeriesId = 2,
            UserRating = 5,
        });

        Assert.False(result);

        var ratings = user.Ratings;
        Assert.Empty(ratings);
    }
    protected override async Task ResetDb()
    {
        Context.Series.RemoveRange(Context.Series.ToList());
        Context.AppUserRating.RemoveRange(Context.AppUserRating.ToList());
        Context.Genre.RemoveRange(Context.Genre.ToList());
        Context.CollectionTag.RemoveRange(Context.CollectionTag.ToList());
        Context.Person.RemoveRange(Context.Person.ToList());
        Context.Library.RemoveRange(Context.Library.ToList());

        await Context.SaveChangesAsync();
    }
}
