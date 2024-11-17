using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using API.DTOs.Filtering.v2;
using API.DTOs.Progress;
using API.Entities;
using API.Entities.Enums;
using API.Extensions.QueryExtensions.Filtering;
using API.Helpers.Builders;
using API.Services;
using API.Services.Plus;
using API.SignalR;
using AutoMapper;
using Hangfire;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using NSubstitute;
using Xunit;

namespace API.Tests.Extensions;

public class SeriesFilterTests : AbstractDbTest
{
    protected override Task ResetDb()
    {
        return Task.CompletedTask;
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
    #endregion

    #region HasLanguage

    [Fact]
    public async Task HasLanguage_Works()
    {
        var foundSeries = await _context.Series.HasLanguage(true, FilterComparison.Contains, new List<string>() { }).ToListAsync();

    }

    #endregion
}
