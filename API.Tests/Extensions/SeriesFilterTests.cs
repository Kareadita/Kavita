using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using API.Data;
using API.DTOs;
using API.DTOs.Filtering.v2;
using API.DTOs.Progress;
using API.Entities;
using API.Entities.Enums;
using API.Extensions.QueryExtensions.Filtering;
using API.Helpers.Builders;
using API.Services;
using API.Services.Plus;
using API.Services.Reading;
using API.SignalR;
using Kavita.Common;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using NSubstitute;
using Xunit;
using Xunit.Abstractions;

namespace API.Tests.Extensions;

public class SeriesFilterTests(ITestOutputHelper outputHelper): AbstractDbTest(outputHelper)
{

    #region HasProgress

    private async Task<AppUser> SetupHasProgress(IUnitOfWork unitOfWork, DataContext context)
    {
        var library = new LibraryBuilder("Manga")
            .WithSeries(new SeriesBuilder("None").WithPages(10)
                .WithVolume(new VolumeBuilder("1")
                    .WithChapter(new ChapterBuilder("1").WithPages(10).Build())
                    .Build())
                .Build())
            .WithSeries(new SeriesBuilder("Partial").WithPages(10)
                .WithVolume(new VolumeBuilder("1")
                    .WithChapter(new ChapterBuilder("1").WithPages(10).Build())
                    .Build())
                .Build())
            .WithSeries(new SeriesBuilder("Full").WithPages(10)
                .WithVolume(new VolumeBuilder("1")
                    .WithChapter(new ChapterBuilder("1").WithPages(10).Build())
                    .Build())
                .Build())
            .Build();
        var user = new AppUserBuilder("user", "user@gmail.com")
            .WithLibrary(library)
            .Build();

        context.Users.Add(user);
        await context.SaveChangesAsync();


        // Create read progress on Partial and Full
        var readerService = new ReaderService(unitOfWork, Substitute.For<ILogger<ReaderService>>(),
            Substitute.For<IEventHub>(), Substitute.For<IImageService>(),
            Substitute.For<IDirectoryService>(), Substitute.For<IScrobblingService>(),
            Substitute.For<IReadingSessionService>(), Substitute.For<IClientInfoAccessor>(),
            Substitute.For<ISeriesService>(), Substitute.For<IEntityNamingService>(),
            Substitute.For<ILocalizationService>());

        // Select Partial and set pages read to 5 on first chapter
        var partialSeries = await unitOfWork.SeriesRepository.GetSeriesByIdAsync(2);
        var partialChapter = partialSeries.Volumes.First().Chapters.First();

        Assert.True(await readerService.SaveReadingProgress(new ProgressDto()
        {
            ChapterId = partialChapter.Id,
            LibraryId = 1,
            SeriesId = partialSeries.Id,
            PageNum = 5,
            VolumeId = partialChapter.VolumeId
        }, user.Id));

        // Select Full and set pages read to 10 on first chapter
        var fullSeries = await unitOfWork.SeriesRepository.GetSeriesByIdAsync(3);
        var fullChapter = fullSeries.Volumes.First().Chapters.First();

        Assert.True(await readerService.SaveReadingProgress(new ProgressDto()
        {
            ChapterId = fullChapter.Id,
            LibraryId = 1,
            SeriesId = fullSeries.Id,
            PageNum = 10,
            VolumeId = fullChapter.VolumeId
        }, user.Id));

        return user;
    }

    [Fact]
    public async Task HasProgress_LessThan50_ShouldReturnSingle()
    {
        var (unitOfWork, context, mapper) = await CreateDatabase();
        var user = await SetupHasProgress(unitOfWork, context);

        var queryResult = await context.Series.HasReadingProgress(true, FilterComparison.LessThan, 50, user.Id)
            .ToListAsync();

        Assert.Single(queryResult);
        Assert.Equal("None", queryResult.First().Name);
    }

    [Fact]
    public async Task HasProgress_LessThanOrEqual50_ShouldReturnTwo()
    {
        var (unitOfWork, context, mapper) = await CreateDatabase();
        var user = await SetupHasProgress(unitOfWork, context);

        // Query series with progress <= 50%
        var queryResult = await context.Series.HasReadingProgress(true, FilterComparison.LessThanEqual, 50, user.Id)
            .ToListAsync();

        Assert.Equal(2, queryResult.Count);
        Assert.Contains(queryResult, s => s.Name == "None");
        Assert.Contains(queryResult, s => s.Name == "Partial");
    }

    [Fact]
    public async Task HasProgress_GreaterThan50_ShouldReturnFull()
    {
        var (unitOfWork, context, mapper) = await CreateDatabase();
        var user = await SetupHasProgress(unitOfWork, context);

        // Query series with progress > 50%
        var queryResult = await context.Series.HasReadingProgress(true, FilterComparison.GreaterThan, 50, user.Id)
            .ToListAsync();

        Assert.Single(queryResult);
        Assert.Equal("Full", queryResult.First().Name);
    }

    [Fact]
    public async Task HasProgress_Equal100_ShouldReturnFull()
    {
        var (unitOfWork, context, mapper) = await CreateDatabase();
        var user = await SetupHasProgress(unitOfWork, context);

        // Query series with progress == 100%
        var queryResult = await context.Series.HasReadingProgress(true, FilterComparison.Equal, 100, user.Id)
            .ToListAsync();

        Assert.Single(queryResult);
        Assert.Equal("Full", queryResult.First().Name);
    }

    [Fact]
    public async Task HasProgress_LessThan100_ShouldReturnTwo()
    {
        var (unitOfWork, context, mapper) = await CreateDatabase();
        var user = await SetupHasProgress(unitOfWork, context);

        // Query series with progress < 100%
        var queryResult = await context.Series.HasReadingProgress(true, FilterComparison.LessThan, 100, user.Id)
            .ToListAsync();

        Assert.Equal(2, queryResult.Count);
        Assert.Contains(queryResult, s => s.Name == "None");
        Assert.Contains(queryResult, s => s.Name == "Partial");
    }

    [Fact]
    public async Task HasProgress_LessThanOrEqual100_ShouldReturnAll()
    {
        var (unitOfWork, context, mapper) = await CreateDatabase();
        var user = await SetupHasProgress(unitOfWork, context);

        // Query series with progress <= 100%
        var queryResult = await context.Series.HasReadingProgress(true, FilterComparison.LessThanEqual, 100, user.Id)
            .ToListAsync();

        Assert.Equal(3, queryResult.Count);
        Assert.Contains(queryResult, s => s.Name == "None");
        Assert.Contains(queryResult, s => s.Name == "Partial");
        Assert.Contains(queryResult, s => s.Name == "Full");
    }

    [Fact]
    public async Task HasProgress_LessThan100_WithProgress99_99_ShouldReturnSeries()
    {
        var (unitOfWork, context, mapper) = await CreateDatabase();

        var library = new LibraryBuilder("Manga")
            .WithSeries(new SeriesBuilder("AlmostFull").WithPages(100)
                .WithVolume(new VolumeBuilder("1")
                    .WithChapter(new ChapterBuilder("1").WithPages(100).Build())
                    .Build())
                .Build())
            .Build();
        var user = new AppUserBuilder("user", "user@gmail.com")
            .WithLibrary(library)
            .Build();

        context.Users.Add(user);
        await context.SaveChangesAsync();

        var readerService = new ReaderService(unitOfWork, Substitute.For<ILogger<ReaderService>>(),
            Substitute.For<IEventHub>(), Substitute.For<IImageService>(),
            Substitute.For<IDirectoryService>(),
            Substitute.For<IScrobblingService>(),
            Substitute.For<IReadingSessionService>(), Substitute.For<IClientInfoAccessor>(),
            Substitute.For<ISeriesService>(), Substitute.For<IEntityNamingService>(),
            Substitute.For<ILocalizationService>());

        // Set progress to 99.99% (99/100 pages read)
        var series = await unitOfWork.SeriesRepository.GetSeriesByIdAsync(1);
        var chapter = series.Volumes.First().Chapters.First();

        Assert.True(await readerService.SaveReadingProgress(new ProgressDto()
        {
            ChapterId = chapter.Id,
            LibraryId = 1,
            SeriesId = series.Id,
            PageNum = 99,
            VolumeId = chapter.VolumeId
        }, user.Id));

        // Query series with progress < 100%
        var queryResult = await context.Series.HasReadingProgress(true, FilterComparison.LessThan, 100, user.Id)
            .ToListAsync();

        Assert.Single(queryResult);
        Assert.Equal("AlmostFull", queryResult.First().Name);
    }
    #endregion

    #region HasLanguage

    private async Task<AppUser> SetupHasLanguage(DataContext context)
    {
        var library = new LibraryBuilder("Manga")
            .WithSeries(new SeriesBuilder("English").WithPages(10)
                .WithMetadata(new SeriesMetadataBuilder().WithLanguage("en").Build())
                .WithVolume(new VolumeBuilder("1")
                    .WithChapter(new ChapterBuilder("1").WithPages(10).Build())
                    .Build())
                .Build())
            .WithSeries(new SeriesBuilder("French").WithPages(10)
                .WithMetadata(new SeriesMetadataBuilder().WithLanguage("fr").Build())
                .WithVolume(new VolumeBuilder("1")
                    .WithChapter(new ChapterBuilder("1").WithPages(10).Build())
                    .Build())
                .Build())
            .WithSeries(new SeriesBuilder("Spanish").WithPages(10)
                .WithMetadata(new SeriesMetadataBuilder().WithLanguage("es").Build())
                .WithVolume(new VolumeBuilder("1")
                    .WithChapter(new ChapterBuilder("1").WithPages(10).Build())
                    .Build())
                .Build())
            .Build();
        var user = new AppUserBuilder("user", "user@gmail.com")
            .WithLibrary(library)
            .Build();

        context.Users.Add(user);
        await context.SaveChangesAsync();

        return user;
    }

    [Fact]
    public async Task HasLanguage_Equal_Works()
    {
        var (unitOfWork, context, mapper) = await CreateDatabase();
        await SetupHasLanguage(context);

        var foundSeries = await context.Series.HasLanguage(true, FilterComparison.Equal, ["en"]).ToListAsync();
        Assert.Single(foundSeries);
        Assert.Equal("en", foundSeries[0].Metadata.Language);
    }

    [Fact]
    public async Task HasLanguage_NotEqual_Works()
    {
        var (unitOfWork, context, mapper) = await CreateDatabase();
        await SetupHasLanguage(context);

        var foundSeries = await context.Series.HasLanguage(true, FilterComparison.NotEqual, ["en"]).ToListAsync();
        Assert.Equal(2, foundSeries.Count);
        Assert.DoesNotContain(foundSeries, s => s.Metadata.Language == "en");
    }

    [Fact]
    public async Task HasLanguage_Contains_Works()
    {
        var (unitOfWork, context, mapper) = await CreateDatabase();
        await SetupHasLanguage(context);

        var foundSeries = await context.Series.HasLanguage(true, FilterComparison.Contains, ["en", "fr"]).ToListAsync();
        Assert.Equal(2, foundSeries.Count);
        Assert.Contains(foundSeries, s => s.Metadata.Language == "en");
        Assert.Contains(foundSeries, s => s.Metadata.Language == "fr");
    }

    [Fact]
    public async Task HasLanguage_NotContains_Works()
    {
        var (unitOfWork, context, mapper) = await CreateDatabase();
        await SetupHasLanguage(context);

        var foundSeries = await context.Series.HasLanguage(true, FilterComparison.NotContains, ["en", "fr"]).ToListAsync();
        Assert.Single(foundSeries);
        Assert.Equal("es", foundSeries[0].Metadata.Language);
    }

    [Fact]
    public async Task HasLanguage_MustContains_Works()
    {
        var (unitOfWork, context, mapper) = await CreateDatabase();
        await SetupHasLanguage(context);

        // Since "MustContains" matches all the provided languages, no series should match in this case.
        var foundSeries = await context.Series.HasLanguage(true, FilterComparison.MustContains, ["en", "fr"]).ToListAsync();
        Assert.Empty(foundSeries);

        // Single language should work.
        foundSeries = await context.Series.HasLanguage(true, FilterComparison.MustContains, ["en"]).ToListAsync();
        Assert.Single(foundSeries);
        Assert.Equal("en", foundSeries[0].Metadata.Language);
    }

    [Fact]
    public async Task HasLanguage_Matches_Works()
    {
        var (unitOfWork, context, mapper) = await CreateDatabase();
        await SetupHasLanguage(context);

        var foundSeries = await context.Series.HasLanguage(true, FilterComparison.Matches, ["e"]).ToListAsync();
        Assert.Equal(2, foundSeries.Count);
        Assert.Contains("en", foundSeries.Select(s => s.Metadata.Language));
        Assert.Contains("es", foundSeries.Select(s => s.Metadata.Language));
    }

    [Fact]
    public async Task HasLanguage_DisabledCondition_ReturnsAll()
    {
        var (unitOfWork, context, mapper) = await CreateDatabase();
        await SetupHasLanguage(context);

        var foundSeries = await context.Series.HasLanguage(false, FilterComparison.Equal, ["en"]).ToListAsync();
        Assert.Equal(3, foundSeries.Count);
    }

    [Fact]
    public async Task HasLanguage_EmptyLanguageList_ReturnsAll()
    {
        var (unitOfWork, context, mapper) = await CreateDatabase();
        await SetupHasLanguage(context);

        var foundSeries = await context.Series.HasLanguage(true, FilterComparison.Equal, new List<string>()).ToListAsync();
        Assert.Equal(3, foundSeries.Count);
    }

    [Fact]
    public async Task HasLanguage_UnsupportedComparison_ThrowsException()
    {
        var (unitOfWork, context, mapper) = await CreateDatabase();
        await SetupHasLanguage(context);

        await Assert.ThrowsAsync<ArgumentOutOfRangeException>(async () =>
        {
            await context.Series.HasLanguage(true, FilterComparison.GreaterThan, ["en"]).ToListAsync();
        });
    }

    #endregion

    #region HasAverageRating

    private async Task<AppUser> SetupHasAverageRating(DataContext context)
    {
        var library = new LibraryBuilder("Manga")
            .WithSeries(new SeriesBuilder("None").WithPages(10)
                .WithExternalMetadata(new ExternalSeriesMetadataBuilder().WithAverageExternalRating(-1).Build())
                .WithVolume(new VolumeBuilder("1")
                    .WithChapter(new ChapterBuilder("1").WithPages(10).Build())
                    .Build())
                .Build())
            .WithSeries(new SeriesBuilder("Partial").WithPages(10)
                .WithExternalMetadata(new ExternalSeriesMetadataBuilder().WithAverageExternalRating(50).Build())
                .WithVolume(new VolumeBuilder("1")
                    .WithChapter(new ChapterBuilder("1").WithPages(10).Build())
                    .Build())
                .Build())
            .WithSeries(new SeriesBuilder("Full").WithPages(10)
                .WithExternalMetadata(new ExternalSeriesMetadataBuilder().WithAverageExternalRating(100).Build())
                .WithVolume(new VolumeBuilder("1")
                    .WithChapter(new ChapterBuilder("1").WithPages(10).Build())
                    .Build())
                .Build())
            .Build();
        var user = new AppUserBuilder("user", "user@gmail.com")
            .WithLibrary(library)
            .Build();

        context.Users.Add(user);
        await context.SaveChangesAsync();

        return user;
    }

    [Fact]
    public async Task HasAverageRating_Equal_Works()
    {
        var (unitOfWork, context, mapper) = await CreateDatabase();
        await SetupHasAverageRating(context);

        var series = await context.Series.HasAverageRating(true, FilterComparison.Equal, 100).ToListAsync();
        Assert.Single(series);
        Assert.Equal("Full", series[0].Name);
    }

    [Fact]
    public async Task HasAverageRating_GreaterThan_Works()
    {
        var (unitOfWork, context, mapper) = await CreateDatabase();
        await SetupHasAverageRating(context);

        var series = await context.Series.HasAverageRating(true, FilterComparison.GreaterThan, 50).ToListAsync();
        Assert.Single(series);
        Assert.Equal("Full", series[0].Name);
    }

    [Fact]
    public async Task HasAverageRating_GreaterThanEqual_Works()
    {
        var (unitOfWork, context, mapper) = await CreateDatabase();
        await SetupHasAverageRating(context);

        var series = await context.Series.HasAverageRating(true, FilterComparison.GreaterThanEqual, 50).ToListAsync();
        Assert.Equal(2, series.Count);
        Assert.Contains(series, s => s.Name == "Partial");
        Assert.Contains(series, s => s.Name == "Full");
    }

    [Fact]
    public async Task HasAverageRating_LessThan_Works()
    {
        var (unitOfWork, context, mapper) = await CreateDatabase();
        await SetupHasAverageRating(context);

        var series = await context.Series.HasAverageRating(true, FilterComparison.LessThan, 50).ToListAsync();
        Assert.Single(series);
        Assert.Equal("None", series[0].Name);
    }

    [Fact]
    public async Task HasAverageRating_LessThanEqual_Works()
    {
        var (unitOfWork, context, mapper) = await CreateDatabase();
        await SetupHasAverageRating(context);

        var series = await context.Series.HasAverageRating(true, FilterComparison.LessThanEqual, 50).ToListAsync();
        Assert.Equal(2, series.Count);
        Assert.Contains(series, s => s.Name == "None");
        Assert.Contains(series, s => s.Name == "Partial");
    }

    [Fact]
    public async Task HasAverageRating_NotEqual_Works()
    {
        var (unitOfWork, context, mapper) = await CreateDatabase();
        await SetupHasAverageRating(context);

        var series = await context.Series.HasAverageRating(true, FilterComparison.NotEqual, 100).ToListAsync();
        Assert.Equal(2, series.Count);
        Assert.DoesNotContain(series, s => s.Name == "Full");
    }

    [Fact]
    public async Task HasAverageRating_ConditionFalse_ReturnsAll()
    {
        var (unitOfWork, context, mapper) = await CreateDatabase();
        await SetupHasAverageRating(context);

        var series = await context.Series.HasAverageRating(false, FilterComparison.Equal, 100).ToListAsync();
        Assert.Equal(3, series.Count);
    }

    [Fact]
    public async Task HasAverageRating_NotSet_IsHandled()
    {
        var (unitOfWork, context, mapper) = await CreateDatabase();
        await SetupHasAverageRating(context);

        var series = await context.Series.HasAverageRating(true, FilterComparison.Equal, -1).ToListAsync();
        Assert.Single(series);
        Assert.Equal("None", series[0].Name);
    }

    [Fact]
    public async Task HasAverageRating_ThrowsForInvalidComparison()
    {
        var (unitOfWork, context, mapper) = await CreateDatabase();
        await SetupHasAverageRating(context);

        await Assert.ThrowsAsync<KavitaException>(async () =>
        {
            await context.Series.HasAverageRating(true, FilterComparison.Contains, 50).ToListAsync();
        });
    }

    [Fact]
    public async Task HasAverageRating_ThrowsForOutOfRangeComparison()
    {
        var (unitOfWork, context, mapper) = await CreateDatabase();
        await SetupHasAverageRating(context);

        await Assert.ThrowsAsync<ArgumentOutOfRangeException>(async () =>
        {
            await context.Series.HasAverageRating(true, (FilterComparison)999, 50).ToListAsync();
        });
    }

    #endregion

    # region HasPublicationStatus

    private async Task<AppUser> SetupHasPublicationStatus(DataContext context)
    {
        var library = new LibraryBuilder("Manga")
            .WithSeries(new SeriesBuilder("Cancelled").WithPages(10)
                .WithMetadata(new SeriesMetadataBuilder().WithPublicationStatus(PublicationStatus.Cancelled).Build())
                .WithVolume(new VolumeBuilder("1")
                    .WithChapter(new ChapterBuilder("1").WithPages(10).Build())
                    .Build())
                .Build())
            .WithSeries(new SeriesBuilder("OnGoing").WithPages(10)
                .WithMetadata(new SeriesMetadataBuilder().WithPublicationStatus(PublicationStatus.OnGoing).Build())
                .WithVolume(new VolumeBuilder("1")
                    .WithChapter(new ChapterBuilder("1").WithPages(10).Build())
                    .Build())
                .Build())
            .WithSeries(new SeriesBuilder("Completed").WithPages(10)
                .WithMetadata(new SeriesMetadataBuilder().WithPublicationStatus(PublicationStatus.Completed).Build())
                .WithVolume(new VolumeBuilder("1")
                    .WithChapter(new ChapterBuilder("1").WithPages(10).Build())
                    .Build())
                .Build())
            .Build();
        var user = new AppUserBuilder("user", "user@gmail.com")
            .WithLibrary(library)
            .Build();

        context.Users.Add(user);
        await context.SaveChangesAsync();

        return user;
    }

    [Fact]
    public async Task HasPublicationStatus_Equal_Works()
    {
        var (unitOfWork, context, mapper) = await CreateDatabase();
        await SetupHasPublicationStatus(context);

        var foundSeries = await context.Series.HasPublicationStatus(true, FilterComparison.Equal, new List<PublicationStatus> { PublicationStatus.Cancelled }).ToListAsync();
        Assert.Single(foundSeries);
        Assert.Equal("Cancelled", foundSeries[0].Name);
    }

    [Fact]
    public async Task HasPublicationStatus_Contains_Works()
    {
        var (unitOfWork, context, mapper) = await CreateDatabase();
        await SetupHasPublicationStatus(context);

        var foundSeries = await context.Series.HasPublicationStatus(true, FilterComparison.Contains, new List<PublicationStatus> { PublicationStatus.Cancelled, PublicationStatus.Completed }).ToListAsync();
        Assert.Equal(2, foundSeries.Count);
        Assert.Contains(foundSeries, s => s.Name == "Cancelled");
        Assert.Contains(foundSeries, s => s.Name == "Completed");
    }

    [Fact]
    public async Task HasPublicationStatus_NotContains_Works()
    {
        var (unitOfWork, context, mapper) = await CreateDatabase();
        await SetupHasPublicationStatus(context);

        var foundSeries = await context.Series.HasPublicationStatus(true, FilterComparison.NotContains, new List<PublicationStatus> { PublicationStatus.Cancelled }).ToListAsync();
        Assert.Equal(2, foundSeries.Count);
        Assert.Contains(foundSeries, s => s.Name == "OnGoing");
        Assert.Contains(foundSeries, s => s.Name == "Completed");
    }

    [Fact]
    public async Task HasPublicationStatus_NotEqual_Works()
    {
        var (unitOfWork, context, mapper) = await CreateDatabase();
        await SetupHasPublicationStatus(context);

        var foundSeries = await context.Series.HasPublicationStatus(true, FilterComparison.NotEqual, new List<PublicationStatus> { PublicationStatus.OnGoing }).ToListAsync();
        Assert.Equal(2, foundSeries.Count);
        Assert.Contains(foundSeries, s => s.Name == "Cancelled");
        Assert.Contains(foundSeries, s => s.Name == "Completed");
    }

    [Fact]
    public async Task HasPublicationStatus_ConditionFalse_ReturnsAll()
    {
        var (unitOfWork, context, mapper) = await CreateDatabase();
        await SetupHasPublicationStatus(context);

        var foundSeries = await context.Series.HasPublicationStatus(false, FilterComparison.Equal, new List<PublicationStatus> { PublicationStatus.Cancelled }).ToListAsync();
        Assert.Equal(3, foundSeries.Count);
    }

    [Fact]
    public async Task HasPublicationStatus_EmptyPubStatuses_ReturnsAll()
    {
        var (unitOfWork, context, mapper) = await CreateDatabase();
        await SetupHasPublicationStatus(context);

        var foundSeries = await context.Series.HasPublicationStatus(true, FilterComparison.Equal, new List<PublicationStatus>()).ToListAsync();
        Assert.Equal(3, foundSeries.Count);
    }

    [Fact]
    public async Task HasPublicationStatus_ThrowsForInvalidComparison()
    {
        var (unitOfWork, context, mapper) = await CreateDatabase();
        await SetupHasPublicationStatus(context);

        await Assert.ThrowsAsync<KavitaException>(async () =>
        {
            await context.Series.HasPublicationStatus(true, FilterComparison.BeginsWith, new List<PublicationStatus> { PublicationStatus.Cancelled }).ToListAsync();
        });
    }

    [Fact]
    public async Task HasPublicationStatus_ThrowsForOutOfRangeComparison()
    {
        var (unitOfWork, context, mapper) = await CreateDatabase();
        await SetupHasPublicationStatus(context);

        await Assert.ThrowsAsync<ArgumentOutOfRangeException>(async () =>
        {
            await context.Series.HasPublicationStatus(true, (FilterComparison)999, new List<PublicationStatus> { PublicationStatus.Cancelled }).ToListAsync();
        });
    }
    #endregion

    #region HasAgeRating
    private async Task<AppUser> SetupHasAgeRating(DataContext context)
    {
        var library = new LibraryBuilder("Manga")
            .WithSeries(new SeriesBuilder("Unknown").WithPages(10)
                .WithMetadata(new SeriesMetadataBuilder().WithAgeRating(AgeRating.Unknown).Build())
                .WithVolume(new VolumeBuilder("1")
                    .WithChapter(new ChapterBuilder("1").WithPages(10).Build())
                    .Build())
                .Build())
            .WithSeries(new SeriesBuilder("G").WithPages(10)
                .WithMetadata(new SeriesMetadataBuilder().WithAgeRating(AgeRating.G).Build())
                .WithVolume(new VolumeBuilder("1")
                    .WithChapter(new ChapterBuilder("1").WithPages(10).Build())
                    .Build())
                .Build())
            .WithSeries(new SeriesBuilder("Mature").WithPages(10)
                .WithMetadata(new SeriesMetadataBuilder().WithAgeRating(AgeRating.Mature).Build())
                .WithVolume(new VolumeBuilder("1")
                    .WithChapter(new ChapterBuilder("1").WithPages(10).Build())
                    .Build())
                .Build())
            .Build();
        var user = new AppUserBuilder("user", "user@gmail.com")
            .WithLibrary(library)
            .Build();

        context.Users.Add(user);
        await context.SaveChangesAsync();

        return user;
    }

    [Fact]
    public async Task HasAgeRating_Equal_Works()
    {
        var (unitOfWork, context, mapper) = await CreateDatabase();
        await SetupHasAgeRating(context);

        var foundSeries = await context.Series.HasAgeRating(true, FilterComparison.Equal, [AgeRating.G]).ToListAsync();
        Assert.Single(foundSeries);
        Assert.Equal("G", foundSeries[0].Name);
    }

    [Fact]
    public async Task HasAgeRating_Contains_Works()
    {
        var (unitOfWork, context, mapper) = await CreateDatabase();
        await SetupHasAgeRating(context);

        var foundSeries = await context.Series.HasAgeRating(true, FilterComparison.Contains, new List<AgeRating> { AgeRating.G, AgeRating.Mature }).ToListAsync();
        Assert.Equal(2, foundSeries.Count);
        Assert.Contains(foundSeries, s => s.Name == "G");
        Assert.Contains(foundSeries, s => s.Name == "Mature");
    }

    [Fact]
    public async Task HasAgeRating_NotContains_Works()
    {
        var (unitOfWork, context, mapper) = await CreateDatabase();
        await SetupHasAgeRating(context);

        var foundSeries = await context.Series.HasAgeRating(true, FilterComparison.NotContains, new List<AgeRating> { AgeRating.Unknown }).ToListAsync();
        Assert.Equal(2, foundSeries.Count);
        Assert.Contains(foundSeries, s => s.Name == "G");
        Assert.Contains(foundSeries, s => s.Name == "Mature");
    }

    [Fact]
    public async Task HasAgeRating_NotEqual_Works()
    {
        var (unitOfWork, context, mapper) = await CreateDatabase();
        await SetupHasAgeRating(context);

        var foundSeries = await context.Series.HasAgeRating(true, FilterComparison.NotEqual, new List<AgeRating> { AgeRating.G }).ToListAsync();
        Assert.Equal(2, foundSeries.Count);
        Assert.Contains(foundSeries, s => s.Name == "Unknown");
        Assert.Contains(foundSeries, s => s.Name == "Mature");
    }

    [Fact]
    public async Task HasAgeRating_GreaterThan_Works()
    {
        var (unitOfWork, context, mapper) = await CreateDatabase();
        await SetupHasAgeRating(context);

        var foundSeries = await context.Series.HasAgeRating(true, FilterComparison.GreaterThan, new List<AgeRating> { AgeRating.Unknown }).ToListAsync();
        Assert.Equal(2, foundSeries.Count);
        Assert.Contains(foundSeries, s => s.Name == "G");
        Assert.Contains(foundSeries, s => s.Name == "Mature");
    }

    [Fact]
    public async Task HasAgeRating_GreaterThanEqual_Works()
    {
        var (unitOfWork, context, mapper) = await CreateDatabase();
        await SetupHasAgeRating(context);

        var foundSeries = await context.Series.HasAgeRating(true, FilterComparison.GreaterThanEqual, new List<AgeRating> { AgeRating.G }).ToListAsync();
        Assert.Equal(2, foundSeries.Count);
        Assert.Contains(foundSeries, s => s.Name == "G");
        Assert.Contains(foundSeries, s => s.Name == "Mature");
    }

    [Fact]
    public async Task HasAgeRating_LessThan_Works()
    {
        var (unitOfWork, context, mapper) = await CreateDatabase();
        await SetupHasAgeRating(context);

        var foundSeries = await context.Series.HasAgeRating(true, FilterComparison.LessThan, new List<AgeRating> { AgeRating.Mature }).ToListAsync();
        Assert.Equal(2, foundSeries.Count);
        Assert.Contains(foundSeries, s => s.Name == "Unknown");
        Assert.Contains(foundSeries, s => s.Name == "G");
    }

    [Fact]
    public async Task HasAgeRating_LessThanEqual_Works()
    {
        var (unitOfWork, context, mapper) = await CreateDatabase();
        await SetupHasAgeRating(context);

        var foundSeries = await context.Series.HasAgeRating(true, FilterComparison.LessThanEqual, new List<AgeRating> { AgeRating.G }).ToListAsync();
        Assert.Equal(2, foundSeries.Count);
        Assert.Contains(foundSeries, s => s.Name == "Unknown");
        Assert.Contains(foundSeries, s => s.Name == "G");
    }

    [Fact]
    public async Task HasAgeRating_ConditionFalse_ReturnsAll()
    {
        var (unitOfWork, context, mapper) = await CreateDatabase();
        await SetupHasAgeRating(context);

        var foundSeries = await context.Series.HasAgeRating(false, FilterComparison.Equal, new List<AgeRating> { AgeRating.G }).ToListAsync();
        Assert.Equal(3, foundSeries.Count);
    }

    [Fact]
    public async Task HasAgeRating_EmptyRatings_ReturnsAll()
    {
        var (unitOfWork, context, mapper) = await CreateDatabase();
        await SetupHasAgeRating(context);

        var foundSeries = await context.Series.HasAgeRating(true, FilterComparison.Equal, new List<AgeRating>()).ToListAsync();
        Assert.Equal(3, foundSeries.Count);
    }

    [Fact]
    public async Task HasAgeRating_ThrowsForInvalidComparison()
    {
        var (unitOfWork, context, mapper) = await CreateDatabase();
        await SetupHasAgeRating(context);

        await Assert.ThrowsAsync<KavitaException>(async () =>
        {
            await context.Series.HasAgeRating(true, FilterComparison.BeginsWith, new List<AgeRating> { AgeRating.G }).ToListAsync();
        });
    }

    [Fact]
    public async Task HasAgeRating_ThrowsForOutOfRangeComparison()
    {
        var (unitOfWork, context, mapper) = await CreateDatabase();
        await SetupHasAgeRating(context);

        await Assert.ThrowsAsync<ArgumentOutOfRangeException>(async () =>
        {
            await context.Series.HasAgeRating(true, (FilterComparison)999, new List<AgeRating> { AgeRating.G }).ToListAsync();
        });
    }

    #endregion

    #region HasReleaseYear

    private async Task<AppUser> SetupHasReleaseYear(DataContext context)
    {
        var library = new LibraryBuilder("Manga")
            .WithSeries(new SeriesBuilder("2000").WithPages(10)
                .WithMetadata(new SeriesMetadataBuilder().WithReleaseYear(2000).Build())
                .WithVolume(new VolumeBuilder("1")
                    .WithChapter(new ChapterBuilder("1").WithPages(10).Build())
                    .Build())
                .Build())
            .WithSeries(new SeriesBuilder("2020").WithPages(10)
                .WithMetadata(new SeriesMetadataBuilder().WithReleaseYear(2020).Build())
                .WithVolume(new VolumeBuilder("1")
                    .WithChapter(new ChapterBuilder("1").WithPages(10).Build())
                    .Build())
                .Build())
            .WithSeries(new SeriesBuilder("2025").WithPages(10)
                .WithMetadata(new SeriesMetadataBuilder().WithReleaseYear(2025).Build())
                .WithVolume(new VolumeBuilder("1")
                    .WithChapter(new ChapterBuilder("1").WithPages(10).Build())
                    .Build())
                .Build())
            .Build();
        var user = new AppUserBuilder("user", "user@gmail.com")
            .WithLibrary(library)
            .Build();

        context.Users.Add(user);
        await context.SaveChangesAsync();

        return user;
    }

    [Fact]
    public async Task HasReleaseYear_Equal_Works()
    {
        var (unitOfWork, context, mapper) = await CreateDatabase();
        await SetupHasReleaseYear(context);

        var foundSeries = await context.Series.HasReleaseYear(true, FilterComparison.Equal, 2020).ToListAsync();
        Assert.Single(foundSeries);
        Assert.Equal("2020", foundSeries[0].Name);
    }

    [Fact]
    public async Task HasReleaseYear_GreaterThan_Works()
    {
        var (unitOfWork, context, mapper) = await CreateDatabase();
        await SetupHasReleaseYear(context);

        var foundSeries = await context.Series.HasReleaseYear(true, FilterComparison.GreaterThan, 2000).ToListAsync();
        Assert.Equal(2, foundSeries.Count);
        Assert.Contains(foundSeries, s => s.Name == "2020");
        Assert.Contains(foundSeries, s => s.Name == "2025");
    }

    [Fact]
    public async Task HasReleaseYear_LessThan_Works()
    {
        var (unitOfWork, context, mapper) = await CreateDatabase();
        await SetupHasReleaseYear(context);

        var foundSeries = await context.Series.HasReleaseYear(true, FilterComparison.LessThan, 2025).ToListAsync();
        Assert.Equal(2, foundSeries.Count);
        Assert.Contains(foundSeries, s => s.Name == "2000");
        Assert.Contains(foundSeries, s => s.Name == "2020");
    }

    [Fact]
    public async Task HasReleaseYear_IsInLast_Works()
    {
        var (unitOfWork, context, mapper) = await CreateDatabase();
        await SetupHasReleaseYear(context);

        var years = DateTime.UtcNow.Year - 2020;

        var foundSeries = await context.Series.HasReleaseYear(true, FilterComparison.IsInLast, years).ToListAsync();
        Assert.Equal(2, foundSeries.Count);
    }

    [Fact]
    public async Task HasReleaseYear_IsNotInLast_Works()
    {
        var (unitOfWork, context, mapper) = await CreateDatabase();
        await SetupHasReleaseYear(context);

        var years = DateTime.UtcNow.Year - 2020;

        var foundSeries = await context.Series.HasReleaseYear(true, FilterComparison.IsNotInLast, years).ToListAsync();
        Assert.Single(foundSeries);
        Assert.Contains(foundSeries, s => s.Name == "2000");
    }

    [Fact]
    public async Task HasReleaseYear_ConditionFalse_ReturnsAll()
    {
        var (unitOfWork, context, mapper) = await CreateDatabase();
        await SetupHasReleaseYear(context);

        var foundSeries = await context.Series.HasReleaseYear(false, FilterComparison.Equal, 2020).ToListAsync();
        Assert.Equal(3, foundSeries.Count);
    }

    [Fact]
    public async Task HasReleaseYear_ReleaseYearNull_ReturnsAll()
    {
        var (unitOfWork, context, mapper) = await CreateDatabase();
        await SetupHasReleaseYear(context);

        var foundSeries = await context.Series.HasReleaseYear(true, FilterComparison.Equal, null).ToListAsync();
        Assert.Equal(3, foundSeries.Count);
    }

    [Fact]
    public async Task HasReleaseYear_IsEmpty_Works()
    {
        var (unitOfWork, context, mapper) = await CreateDatabase();

        var library = new LibraryBuilder("Manga")
            .WithSeries(new SeriesBuilder("EmptyYear").WithPages(10)
                .WithMetadata(new SeriesMetadataBuilder().WithReleaseYear(0).Build())
                .WithVolume(new VolumeBuilder("1")
                    .WithChapter(new ChapterBuilder("1").WithPages(10).Build())
                    .Build())
                .Build())
            .Build();

        context.Library.Add(library);
        await context.SaveChangesAsync();

        var foundSeries = await context.Series.HasReleaseYear(true, FilterComparison.IsEmpty, 0).ToListAsync();
        Assert.Single(foundSeries);
        Assert.Equal("EmptyYear", foundSeries[0].Name);
    }


    #endregion

    #region HasRating

    private async Task<AppUser> SetupHasRating(IUnitOfWork unitOfWork, DataContext context)
    {
        var library = new LibraryBuilder("Manga")
            .WithSeries(new SeriesBuilder("No Rating").WithPages(10)
                .WithVolume(new VolumeBuilder("1")
                    .WithChapter(new ChapterBuilder("1").WithPages(10).Build())
                    .Build())
                .Build())
            .WithSeries(new SeriesBuilder("0 Rating").WithPages(10)
                .WithVolume(new VolumeBuilder("1")
                    .WithChapter(new ChapterBuilder("1").WithPages(10).Build())
                    .Build())
                .Build())
            .WithSeries(new SeriesBuilder("4.5 Rating").WithPages(10)
                .WithVolume(new VolumeBuilder("1")
                    .WithChapter(new ChapterBuilder("1").WithPages(10).Build())
                    .Build())
                .Build())
            .Build();
        var user = new AppUserBuilder("user", "user@gmail.com")
            .WithLibrary(library)
            .Build();

        context.Users.Add(user);
        await context.SaveChangesAsync();

        var ratingService = new RatingService(unitOfWork, Substitute.For<IScrobblingService>(), Substitute.For<ILogger<RatingService>>());

        // Select 0 Rating
        var zeroRating = await unitOfWork.SeriesRepository.GetSeriesByIdAsync(2);
        Assert.NotNull(zeroRating);

        Assert.True(await ratingService.UpdateSeriesRating(user, new UpdateRatingDto()
        {
            SeriesId = zeroRating.Id,
            UserRating = 0
        }));

        // Select 4.5 Rating
        var partialRating = await unitOfWork.SeriesRepository.GetSeriesByIdAsync(3);

        Assert.True(await ratingService.UpdateSeriesRating(user, new UpdateRatingDto()
        {
            SeriesId = partialRating.Id,
            UserRating = 4.5f
        }));

        return user;
    }

    [Fact]
    public async Task HasRating_Equal_Works()
    {
        var (unitOfWork, context, mapper) = await CreateDatabase();
        var user = await SetupHasRating(unitOfWork, context);

        var foundSeries = await context.Series
            .HasRating(true, FilterComparison.Equal, 4.5f, user.Id)
            .ToListAsync();

        Assert.Single(foundSeries);
        Assert.Equal("4.5 Rating", foundSeries[0].Name);
    }

    [Fact]
    public async Task HasRating_GreaterThan_Works()
    {
        var (unitOfWork, context, mapper) = await CreateDatabase();
        var user = await SetupHasRating(unitOfWork, context);

        var foundSeries = await context.Series
            .HasRating(true, FilterComparison.GreaterThan, 0, user.Id)
            .ToListAsync();

        Assert.Single(foundSeries);
        Assert.Equal("4.5 Rating", foundSeries[0].Name);
    }

    [Fact]
    public async Task HasRating_LessThan_Works()
    {
        var (unitOfWork, context, mapper) = await CreateDatabase();
        var user = await SetupHasRating(unitOfWork, context);

        var foundSeries = await context.Series
            .HasRating(true, FilterComparison.LessThan, 4.5f, user.Id)
            .ToListAsync();

        Assert.Single(foundSeries);
        Assert.Equal("0 Rating", foundSeries[0].Name);
    }

    [Fact]
    public async Task HasRating_IsEmpty_Works()
    {
        var (unitOfWork, context, mapper) = await CreateDatabase();
        var user = await SetupHasRating(unitOfWork, context);

        var foundSeries = await context.Series
            .HasRating(true, FilterComparison.IsEmpty, 0, user.Id)
            .ToListAsync();

        Assert.Single(foundSeries);
        Assert.Equal("No Rating", foundSeries[0].Name);
    }

    [Fact]
    public async Task HasRating_GreaterThanEqual_Works()
    {
        var (unitOfWork, context, mapper) = await CreateDatabase();
        var user = await SetupHasRating(unitOfWork, context);

        var foundSeries = await context.Series
            .HasRating(true, FilterComparison.GreaterThanEqual, 4.5f, user.Id)
            .ToListAsync();

        Assert.Single(foundSeries);
        Assert.Equal("4.5 Rating", foundSeries[0].Name);
    }

    [Fact]
    public async Task HasRating_LessThanEqual_Works()
    {
        var (unitOfWork, context, mapper) = await CreateDatabase();
        var user = await SetupHasRating(unitOfWork, context);

        var foundSeries = await context.Series
            .HasRating(true, FilterComparison.LessThanEqual, 0, user.Id)
            .ToListAsync();

        Assert.Single(foundSeries);
        Assert.Equal("0 Rating", foundSeries[0].Name);
    }

    #endregion

    #region HasAverageReadTime



    #endregion

    #region HasReadLast



    #endregion

    #region HasReadingDate



    #endregion

    #region HasTags



    #endregion

    #region HasPeople



    #endregion

    #region HasGenre



    #endregion

    #region HasFormat



    #endregion

    #region HasCollectionTags



    #endregion

    #region HasName

    private async Task<AppUser> SetupHasName(DataContext context)
    {
        var library = new LibraryBuilder("Manga")
            .WithSeries(new SeriesBuilder("Don't Toy With Me, Miss Nagatoro").WithLocalizedName("Ijiranaide, Nagatoro-san").WithPages(10)
                .WithVolume(new VolumeBuilder("1")
                    .WithChapter(new ChapterBuilder("1").WithPages(10).Build())
                    .Build())
                .Build())
            .WithSeries(new SeriesBuilder("My Dress-Up Darling").WithLocalizedName("Sono Bisque Doll wa Koi wo Suru").WithPages(10)
                .WithVolume(new VolumeBuilder("1")
                    .WithChapter(new ChapterBuilder("1").WithPages(10).Build())
                    .Build())
                .Build())
            .Build();
        var user = new AppUserBuilder("user", "user@gmail.com")
            .WithLibrary(library)
            .Build();

        context.Users.Add(user);
        await context.SaveChangesAsync();

        return user;
    }

    [Fact]
    public async Task HasName_Equal_Works()
    {
        var (unitOfWork, context, mapper) = await CreateDatabase();
        await SetupHasName(context);

        var foundSeries = await context.Series
            .HasName(true, FilterComparison.Equal, "My Dress-Up Darling")
            .ToListAsync();

        Assert.Single(foundSeries);
        Assert.Equal("My Dress-Up Darling", foundSeries[0].Name);
    }

    [Fact]
    public async Task HasName_Equal_LocalizedName_Works()
    {
        var (unitOfWork, context, mapper) = await CreateDatabase();
        await SetupHasName(context);

        var foundSeries = await context.Series
            .HasName(true, FilterComparison.Equal, "Ijiranaide, Nagatoro-san")
            .ToListAsync();

        Assert.Single(foundSeries);
        Assert.Equal("Don't Toy With Me, Miss Nagatoro", foundSeries[0].Name);
    }

    [Fact]
    public async Task HasName_BeginsWith_Works()
    {
        var (unitOfWork, context, mapper) = await CreateDatabase();
        await SetupHasName(context);

        var foundSeries = await context.Series
            .HasName(true, FilterComparison.BeginsWith, "My Dress")
            .ToListAsync();

        Assert.Single(foundSeries);
        Assert.Equal("My Dress-Up Darling", foundSeries[0].Name);
    }

    [Fact]
    public async Task HasName_BeginsWith_LocalizedName_Works()
    {
        var (unitOfWork, context, mapper) = await CreateDatabase();
        await SetupHasName(context);

        var foundSeries = await context.Series
            .HasName(true, FilterComparison.BeginsWith, "Sono Bisque")
            .ToListAsync();

        Assert.Single(foundSeries);
        Assert.Equal("My Dress-Up Darling", foundSeries[0].Name);
    }

    [Fact]
    public async Task HasName_EndsWith_Works()
    {
        var (unitOfWork, context, mapper) = await CreateDatabase();
        await SetupHasName(context);

        var foundSeries = await context.Series
            .HasName(true, FilterComparison.EndsWith, "Nagatoro")
            .ToListAsync();

        Assert.Single(foundSeries);
        Assert.Equal("Don't Toy With Me, Miss Nagatoro", foundSeries[0].Name);
    }

    [Fact]
    public async Task HasName_Matches_Works()
    {
        var (unitOfWork, context, mapper) = await CreateDatabase();
        await SetupHasName(context);

        var foundSeries = await context.Series
            .HasName(true, FilterComparison.Matches, "Toy With Me")
            .ToListAsync();

        Assert.Single(foundSeries);
        Assert.Equal("Don't Toy With Me, Miss Nagatoro", foundSeries[0].Name);
    }

    [Fact]
    public async Task HasName_NotEqual_Works()
    {
        var (unitOfWork, context, mapper) = await CreateDatabase();
        await SetupHasName(context);

        var foundSeries = await context.Series
            .HasName(true, FilterComparison.NotEqual, "My Dress-Up Darling")
            .ToListAsync();

        Assert.Equal(2, foundSeries.Count);
        Assert.Equal("Don't Toy With Me, Miss Nagatoro", foundSeries[0].Name);
    }


    #endregion

    #region HasSummary

    private async Task<AppUser> SetupHasSummary(DataContext context)
    {
        var library = new LibraryBuilder("Manga")
            .WithSeries(new SeriesBuilder("Hippos").WithPages(10)
                .WithMetadata(new SeriesMetadataBuilder().WithSummary("I like hippos").Build())
                .WithVolume(new VolumeBuilder("1")
                    .WithChapter(new ChapterBuilder("1").WithPages(10).Build())
                    .Build())
                .Build())
            .WithSeries(new SeriesBuilder("Apples").WithPages(10)
                .WithMetadata(new SeriesMetadataBuilder().WithSummary("I like apples").Build())
                .WithVolume(new VolumeBuilder("1")
                    .WithChapter(new ChapterBuilder("1").WithPages(10).Build())
                    .Build())
                .Build())
            .WithSeries(new SeriesBuilder("Ducks").WithPages(10)
                .WithMetadata(new SeriesMetadataBuilder().WithSummary("I like ducks").Build())
                .WithVolume(new VolumeBuilder("1")
                    .WithChapter(new ChapterBuilder("1").WithPages(10).Build())
                    .Build())
                .Build())
            .WithSeries(new SeriesBuilder("No Summary").WithPages(10)
                .WithVolume(new VolumeBuilder("1")
                    .WithChapter(new ChapterBuilder("1").WithPages(10).Build())
                    .Build())
                .Build())
            .Build();
        var user = new AppUserBuilder("user", "user@gmail.com")
            .WithLibrary(library)
            .Build();

        context.Users.Add(user);
        await context.SaveChangesAsync();

        return user;
    }

    [Fact]
    public async Task HasSummary_Equal_Works()
    {
        var (unitOfWork, context, mapper) = await CreateDatabase();
        await SetupHasSummary(context);

        var foundSeries = await context.Series
            .HasSummary(true, FilterComparison.Equal, "I like hippos")
            .ToListAsync();

        Assert.Single(foundSeries);
        Assert.Equal("Hippos", foundSeries[0].Name);
    }

    [Fact]
    public async Task HasSummary_BeginsWith_Works()
    {
        var (unitOfWork, context, mapper) = await CreateDatabase();
        await SetupHasSummary(context);

        var foundSeries = await context.Series
            .HasSummary(true, FilterComparison.BeginsWith, "I like h")
            .ToListAsync();

        Assert.Single(foundSeries);
        Assert.Equal("Hippos", foundSeries[0].Name);
    }

    [Fact]
    public async Task HasSummary_EndsWith_Works()
    {
        var (unitOfWork, context, mapper) = await CreateDatabase();
        await SetupHasSummary(context);

        var foundSeries = await context.Series
            .HasSummary(true, FilterComparison.EndsWith, "apples")
            .ToListAsync();

        Assert.Single(foundSeries);
        Assert.Equal("Apples", foundSeries[0].Name);
    }

    [Fact]
    public async Task HasSummary_Matches_Works()
    {
        var (unitOfWork, context, mapper) = await CreateDatabase();
        await SetupHasSummary(context);

        var foundSeries = await context.Series
            .HasSummary(true, FilterComparison.Matches, "like ducks")
            .ToListAsync();

        Assert.Single(foundSeries);
        Assert.Equal("Ducks", foundSeries[0].Name);
    }

    [Fact]
    public async Task HasSummary_NotEqual_Works()
    {
        var (unitOfWork, context, mapper) = await CreateDatabase();
        await SetupHasSummary(context);

        var foundSeries = await context.Series
            .HasSummary(true, FilterComparison.NotEqual, "I like ducks")
            .ToListAsync();

        Assert.Equal(3, foundSeries.Count);
        Assert.DoesNotContain(foundSeries, s => s.Name == "Ducks");
    }

    [Fact]
    public async Task HasSummary_IsEmpty_Works()
    {
        var (unitOfWork, context, mapper) = await CreateDatabase();
        await SetupHasSummary(context);

        var foundSeries = await context.Series
            .HasSummary(true, FilterComparison.IsEmpty, string.Empty)
            .ToListAsync();

        Assert.Single(foundSeries);
        Assert.Equal("No Summary", foundSeries[0].Name);
    }

    #endregion


    #region HasPath



    #endregion


    #region HasFilePath



    #endregion
}
