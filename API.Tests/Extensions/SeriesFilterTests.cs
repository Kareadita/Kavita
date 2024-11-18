using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using API.DTOs.Filtering.v2;
using API.DTOs.Progress;
using API.Entities;
using API.Extensions.QueryExtensions.Filtering;
using API.Helpers.Builders;
using API.Services;
using API.Services.Plus;
using API.SignalR;
using Kavita.Common;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using NSubstitute;
using Xunit;

namespace API.Tests.Extensions;

public class SeriesFilterTests : AbstractDbTest
{
    protected override async Task ResetDb()
    {
        _context.Series.RemoveRange(_context.Series);
        _context.AppUser.RemoveRange(_context.AppUser);
        await _context.SaveChangesAsync();
    }

    #region HasProgress

    private async Task<AppUser> SetupHasProgress()
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

        _context.Users.Add(user);
        _context.Library.Add(library);
        await _context.SaveChangesAsync();


        // Create read progress on Partial and Full
        var readerService = new ReaderService(_unitOfWork, Substitute.For<ILogger<ReaderService>>(),
            Substitute.For<IEventHub>(), Substitute.For<IImageService>(),
            Substitute.For<IDirectoryService>(), Substitute.For<IScrobblingService>());

        // Select Partial and set pages read to 5 on first chapter
        var partialSeries = await _unitOfWork.SeriesRepository.GetSeriesByIdAsync(2);
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
        var fullSeries = await _unitOfWork.SeriesRepository.GetSeriesByIdAsync(3);
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
        var user = await SetupHasProgress();

        var queryResult = await _context.Series.HasReadingProgress(true, FilterComparison.LessThan, 50, user.Id)
            .ToListAsync();

        Assert.Single(queryResult);
        Assert.Equal("None", queryResult.First().Name);
    }

    [Fact]
    public async Task HasProgress_LessThanOrEqual50_ShouldReturnTwo()
    {
        var user = await SetupHasProgress();

        // Query series with progress <= 50%
        var queryResult = await _context.Series.HasReadingProgress(true, FilterComparison.LessThanEqual, 50, user.Id)
            .ToListAsync();

        Assert.Equal(2, queryResult.Count);
        Assert.Contains(queryResult, s => s.Name == "None");
        Assert.Contains(queryResult, s => s.Name == "Partial");
    }

    [Fact]
    public async Task HasProgress_GreaterThan50_ShouldReturnFull()
    {
        var user = await SetupHasProgress();

        // Query series with progress > 50%
        var queryResult = await _context.Series.HasReadingProgress(true, FilterComparison.GreaterThan, 50, user.Id)
            .ToListAsync();

        Assert.Single(queryResult);
        Assert.Equal("Full", queryResult.First().Name);
    }

    [Fact]
    public async Task HasProgress_Equal100_ShouldReturnFull()
    {
        var user = await SetupHasProgress();

        // Query series with progress == 100%
        var queryResult = await _context.Series.HasReadingProgress(true, FilterComparison.Equal, 100, user.Id)
            .ToListAsync();

        Assert.Single(queryResult);
        Assert.Equal("Full", queryResult.First().Name);
    }

    [Fact]
    public async Task HasProgress_LessThan100_ShouldReturnTwo()
    {
        var user = await SetupHasProgress();

        // Query series with progress < 100%
        var queryResult = await _context.Series.HasReadingProgress(true, FilterComparison.LessThan, 100, user.Id)
            .ToListAsync();

        Assert.Equal(2, queryResult.Count);
        Assert.Contains(queryResult, s => s.Name == "None");
        Assert.Contains(queryResult, s => s.Name == "Partial");
    }

    [Fact]
    public async Task HasProgress_LessThanOrEqual100_ShouldReturnAll()
    {
        var user = await SetupHasProgress();

        // Query series with progress <= 100%
        var queryResult = await _context.Series.HasReadingProgress(true, FilterComparison.LessThanEqual, 100, user.Id)
            .ToListAsync();

        Assert.Equal(3, queryResult.Count);
        Assert.Contains(queryResult, s => s.Name == "None");
        Assert.Contains(queryResult, s => s.Name == "Partial");
        Assert.Contains(queryResult, s => s.Name == "Full");
    }

    [Fact]
    public async Task HasProgress_LessThan100_WithProgress99_99_ShouldReturnSeries()
    {
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

        _context.Users.Add(user);
        _context.Library.Add(library);
        await _context.SaveChangesAsync();

        var readerService = new ReaderService(_unitOfWork, Substitute.For<ILogger<ReaderService>>(),
            Substitute.For<IEventHub>(), Substitute.For<IImageService>(),
            Substitute.For<IDirectoryService>(), Substitute.For<IScrobblingService>());

        // Set progress to 99.99% (99/100 pages read)
        var series = await _unitOfWork.SeriesRepository.GetSeriesByIdAsync(1);
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
        var queryResult = await _context.Series.HasReadingProgress(true, FilterComparison.LessThan, 100, user.Id)
            .ToListAsync();

        Assert.Single(queryResult);
        Assert.Equal("AlmostFull", queryResult.First().Name);
    }
    #endregion

    #region HasLanguage

    private async Task<AppUser> SetupHasLanguage()
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

        _context.Users.Add(user);
        _context.Library.Add(library);
        await _context.SaveChangesAsync();

        return user;
    }

    [Fact]
    public async Task HasLanguage_Equal_Works()
    {
        await SetupHasLanguage();

        var foundSeries = await _context.Series.HasLanguage(true, FilterComparison.Equal, ["en"]).ToListAsync();
        Assert.Single(foundSeries);
        Assert.Equal("en", foundSeries[0].Metadata.Language);
    }

    [Fact]
    public async Task HasLanguage_NotEqual_Works()
    {
        await SetupHasLanguage();

        var foundSeries = await _context.Series.HasLanguage(true, FilterComparison.NotEqual, ["en"]).ToListAsync();
        Assert.Equal(2, foundSeries.Count);
        Assert.DoesNotContain(foundSeries, s => s.Metadata.Language == "en");
    }

    [Fact]
    public async Task HasLanguage_Contains_Works()
    {
        await SetupHasLanguage();

        var foundSeries = await _context.Series.HasLanguage(true, FilterComparison.Contains, ["en", "fr"]).ToListAsync();
        Assert.Equal(2, foundSeries.Count);
        Assert.Contains(foundSeries, s => s.Metadata.Language == "en");
        Assert.Contains(foundSeries, s => s.Metadata.Language == "fr");
    }

    [Fact]
    public async Task HasLanguage_NotContains_Works()
    {
        await SetupHasLanguage();

        var foundSeries = await _context.Series.HasLanguage(true, FilterComparison.NotContains, ["en", "fr"]).ToListAsync();
        Assert.Single(foundSeries);
        Assert.Equal("es", foundSeries[0].Metadata.Language);
    }

    [Fact]
    public async Task HasLanguage_MustContains_Works()
    {
        await SetupHasLanguage();

        // Since "MustContains" matches all the provided languages, no series should match in this case.
        var foundSeries = await _context.Series.HasLanguage(true, FilterComparison.MustContains, ["en", "fr"]).ToListAsync();
        Assert.Empty(foundSeries);

        // Single language should work.
        foundSeries = await _context.Series.HasLanguage(true, FilterComparison.MustContains, ["en"]).ToListAsync();
        Assert.Single(foundSeries);
        Assert.Equal("en", foundSeries[0].Metadata.Language);
    }

    [Fact]
    public async Task HasLanguage_Matches_Works()
    {
        await SetupHasLanguage();

        var foundSeries = await _context.Series.HasLanguage(true, FilterComparison.Matches, ["e"]).ToListAsync();
        Assert.Equal(2, foundSeries.Count);
        Assert.Contains("en", foundSeries.Select(s => s.Metadata.Language));
        Assert.Contains("es", foundSeries.Select(s => s.Metadata.Language));
    }

    [Fact]
    public async Task HasLanguage_DisabledCondition_ReturnsAll()
    {
        await SetupHasLanguage();

        var foundSeries = await _context.Series.HasLanguage(false, FilterComparison.Equal, ["en"]).ToListAsync();
        Assert.Equal(3, foundSeries.Count);
    }

    [Fact]
    public async Task HasLanguage_EmptyLanguageList_ReturnsAll()
    {
        await SetupHasLanguage();

        var foundSeries = await _context.Series.HasLanguage(true, FilterComparison.Equal, new List<string>()).ToListAsync();
        Assert.Equal(3, foundSeries.Count);
    }

    [Fact]
    public async Task HasLanguage_UnsupportedComparison_ThrowsException()
    {
        await SetupHasLanguage();

        await Assert.ThrowsAsync<ArgumentOutOfRangeException>(async () =>
        {
            await _context.Series.HasLanguage(true, FilterComparison.GreaterThan, ["en"]).ToListAsync();
        });
    }

    #endregion

    #region HasAverageRating

    private async Task<AppUser> SetupHasAverageRating()
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

        _context.Users.Add(user);
        _context.Library.Add(library);
        await _context.SaveChangesAsync();

        return user;
    }

    [Fact]
    public async Task HasAverageRating_Equal_Works()
    {
        await SetupHasAverageRating();

        var series = await _context.Series.HasAverageRating(true, FilterComparison.Equal, 100).ToListAsync();
        Assert.Single(series);
        Assert.Equal("Full", series[0].Name);
    }

    [Fact]
    public async Task HasAverageRating_GreaterThan_Works()
    {
        await SetupHasAverageRating();

        var series = await _context.Series.HasAverageRating(true, FilterComparison.GreaterThan, 50).ToListAsync();
        Assert.Single(series);
        Assert.Equal("Full", series[0].Name);
    }

    [Fact]
    public async Task HasAverageRating_GreaterThanEqual_Works()
    {
        await SetupHasAverageRating();

        var series = await _context.Series.HasAverageRating(true, FilterComparison.GreaterThanEqual, 50).ToListAsync();
        Assert.Equal(2, series.Count);
        Assert.Contains(series, s => s.Name == "Partial");
        Assert.Contains(series, s => s.Name == "Full");
    }

    [Fact]
    public async Task HasAverageRating_LessThan_Works()
    {
        await SetupHasAverageRating();

        var series = await _context.Series.HasAverageRating(true, FilterComparison.LessThan, 50).ToListAsync();
        Assert.Single(series);
        Assert.Equal("None", series[0].Name);
    }

    [Fact]
    public async Task HasAverageRating_LessThanEqual_Works()
    {
        await SetupHasAverageRating();

        var series = await _context.Series.HasAverageRating(true, FilterComparison.LessThanEqual, 50).ToListAsync();
        Assert.Equal(2, series.Count);
        Assert.Contains(series, s => s.Name == "None");
        Assert.Contains(series, s => s.Name == "Partial");
    }

    [Fact]
    public async Task HasAverageRating_NotEqual_Works()
    {
        await SetupHasAverageRating();

        var series = await _context.Series.HasAverageRating(true, FilterComparison.NotEqual, 100).ToListAsync();
        Assert.Equal(2, series.Count);
        Assert.DoesNotContain(series, s => s.Name == "Full");
    }

    [Fact]
    public async Task HasAverageRating_ConditionFalse_ReturnsAll()
    {
        await SetupHasAverageRating();

        var series = await _context.Series.HasAverageRating(false, FilterComparison.Equal, 100).ToListAsync();
        Assert.Equal(3, series.Count);
    }

    [Fact]
    public async Task HasAverageRating_NotSet_IsHandled()
    {
        await SetupHasAverageRating();

        var series = await _context.Series.HasAverageRating(true, FilterComparison.Equal, -1).ToListAsync();
        Assert.Single(series);
        Assert.Equal("None", series[0].Name);
    }

    [Fact]
    public async Task HasAverageRating_ThrowsForInvalidComparison()
    {
        await SetupHasAverageRating();

        await Assert.ThrowsAsync<KavitaException>(async () =>
        {
            await _context.Series.HasAverageRating(true, FilterComparison.Contains, 50).ToListAsync();
        });
    }

    [Fact]
    public async Task HasAverageRating_ThrowsForOutOfRangeComparison()
    {
        await SetupHasAverageRating();

        await Assert.ThrowsAsync<ArgumentOutOfRangeException>(async () =>
        {
            await _context.Series.HasAverageRating(true, (FilterComparison)999, 50).ToListAsync();
        });
    }

    #endregion

}
