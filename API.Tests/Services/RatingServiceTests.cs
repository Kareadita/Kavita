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
using Xunit.Abstractions;

namespace API.Tests.Services;

public class RatingServiceTests(ITestOutputHelper outputHelper): AbstractDbTest(outputHelper)
{


    [Fact]
    public async Task UpdateRating_ShouldSetRating()
    {
        var (unitOfWork, context, _) = await CreateDatabase();
        var ratingService = new RatingService(unitOfWork, Substitute.For<IScrobblingService>(), Substitute.For<ILogger<RatingService>>());

        context.Library.Add(new LibraryBuilder("Test LIb")
            .WithAppUser(new AppUserBuilder("majora2007", string.Empty).Build())
            .WithSeries(new SeriesBuilder("Test")

                .WithVolume(new VolumeBuilder("1")
                    .WithChapter(new ChapterBuilder("1").WithPages(1).Build())
                    .Build())
                .Build())
            .Build());


        await context.SaveChangesAsync();


        var user = await unitOfWork.UserRepository.GetUserByUsernameAsync("majora2007", AppUserIncludes.Ratings);

        JobStorage.Current = new InMemoryStorage();
        var result = await ratingService.UpdateSeriesRating(user, new UpdateRatingDto
        {
            SeriesId = 1,
            UserRating = 3,
        });

        Assert.True(result);

        var ratings = (await unitOfWork.UserRepository.GetUserByUsernameAsync("majora2007", AppUserIncludes.Ratings))!
            .Ratings;
        Assert.NotEmpty(ratings);
        Assert.Equal(3, ratings.First().Rating);
    }

    [Fact]
    public async Task UpdateRating_ShouldUpdateExistingRating()
    {
        var (unitOfWork, context, _) = await CreateDatabase();
        var ratingService = new RatingService(unitOfWork, Substitute.For<IScrobblingService>(), Substitute.For<ILogger<RatingService>>());

        context.Library.Add(new LibraryBuilder("Test LIb")
            .WithAppUser(new AppUserBuilder("majora2007", string.Empty).Build())
            .WithSeries(new SeriesBuilder("Test")

                .WithVolume(new VolumeBuilder("1")
                    .WithChapter(new ChapterBuilder("1").WithPages(1).Build())
                    .Build())
                .Build())
            .Build());


        await context.SaveChangesAsync();

        var user = await unitOfWork.UserRepository.GetUserByUsernameAsync("majora2007", AppUserIncludes.Ratings);

        var result = await ratingService.UpdateSeriesRating(user, new UpdateRatingDto
        {
            SeriesId = 1,
            UserRating = 3,
        });

        Assert.True(result);

        JobStorage.Current = new InMemoryStorage();
        var ratings = (await unitOfWork.UserRepository.GetUserByUsernameAsync("majora2007", AppUserIncludes.Ratings))
            .Ratings;
        Assert.NotEmpty(ratings);
        Assert.Equal(3, ratings.First().Rating);

        // Update the DB again

        var result2 = await ratingService.UpdateSeriesRating(user, new UpdateRatingDto
        {
            SeriesId = 1,
            UserRating = 5,
        });

        Assert.True(result2);

        var ratings2 = (await unitOfWork.UserRepository.GetUserByUsernameAsync("majora2007", AppUserIncludes.Ratings))
            .Ratings;
        Assert.NotEmpty(ratings2);
        Assert.True(ratings2.Count == 1);
        Assert.Equal(5, ratings2.First().Rating);
    }

    [Fact]
    public async Task UpdateRating_ShouldClampRatingAt5()
    {
        var (unitOfWork, context, _) = await CreateDatabase();
        var ratingService = new RatingService(unitOfWork, Substitute.For<IScrobblingService>(), Substitute.For<ILogger<RatingService>>());

        context.Library.Add(new LibraryBuilder("Test LIb")
            .WithAppUser(new AppUserBuilder("majora2007", string.Empty).Build())
            .WithSeries(new SeriesBuilder("Test")

                .WithVolume(new VolumeBuilder("1")
                    .WithChapter(new ChapterBuilder("1").WithPages(1).Build())
                    .Build())
                .Build())
            .Build());

        await context.SaveChangesAsync();

        var user = await unitOfWork.UserRepository.GetUserByUsernameAsync("majora2007", AppUserIncludes.Ratings);

        var result = await ratingService.UpdateSeriesRating(user, new UpdateRatingDto
        {
            SeriesId = 1,
            UserRating = 10,
        });

        Assert.True(result);

        JobStorage.Current = new InMemoryStorage();
        var ratings = (await unitOfWork.UserRepository.GetUserByUsernameAsync("majora2007",
                AppUserIncludes.Ratings)!)
            .Ratings;
        Assert.NotEmpty(ratings);
        Assert.Equal(5, ratings.First().Rating);
    }

    [Fact]
    public async Task UpdateRating_ShouldReturnFalseWhenSeriesDoesntExist()
    {
        var (unitOfWork, context, _) = await CreateDatabase();
        var ratingService = new RatingService(unitOfWork, Substitute.For<IScrobblingService>(), Substitute.For<ILogger<RatingService>>());

        context.Library.Add(new LibraryBuilder("Test LIb", LibraryType.Book)
            .WithAppUser(new AppUserBuilder("majora2007", string.Empty).Build())
            .WithSeries(new SeriesBuilder("Test")

                .WithVolume(new VolumeBuilder("1")
                    .WithChapter(new ChapterBuilder("1").WithPages(1).Build())
                    .Build())
                .Build())
            .Build());

        await context.SaveChangesAsync();

        var user = await unitOfWork.UserRepository.GetUserByUsernameAsync("majora2007", AppUserIncludes.Ratings);

        var result = await ratingService.UpdateSeriesRating(user, new UpdateRatingDto
        {
            SeriesId = 2,
            UserRating = 5,
        });

        Assert.False(result);

        var ratings = user.Ratings;
        Assert.Empty(ratings);
    }
}
