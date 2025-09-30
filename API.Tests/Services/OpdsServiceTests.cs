using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Abstractions;
using System.Linq;
using System.Threading.Tasks;
using API.Data;
using API.Data.Repositories;
using API.DTOs.OPDS.Requests;
using API.Entities;
using API.Entities.Enums;
using API.Helpers.Builders;
using API.Services;
using API.Services.Plus;
using API.SignalR;
using API.Tests.Helpers;
using AutoMapper;
using Hangfire;
using Hangfire.InMemory;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging;
using NSubstitute;
using Xunit;
using Xunit.Abstractions;

namespace API.Tests.Services;

public class OpdsServiceTests(ITestOutputHelper testOutputHelper) : AbstractDbTest(testOutputHelper)
{
    private readonly string _testFilePath = Path.Join(Path.Join(Directory.GetCurrentDirectory(), "../../../Services/Test Data/OpdsService"), "test.zip");
    #region Setup

    private Tuple<IOpdsService, IReaderService> SetupService(IUnitOfWork unitOfWork, IMapper mapper)
    {
        JobStorage.Current = new InMemoryStorage();

        var ds = new DirectoryService(Substitute.For<ILogger<DirectoryService>>(), new FileSystem());

        var readerService = new ReaderService(unitOfWork, Substitute.For<ILogger<ReaderService>>(),
            Substitute.For<IEventHub>(), Substitute.For<IImageService>(), ds, Substitute.For<IScrobblingService>());

        var localizationService =
            new LocalizationService(ds, new MockHostingEnvironment(), Substitute.For<IMemoryCache>(), unitOfWork);

        var seriesService = new SeriesService(unitOfWork, Substitute.For<IEventHub>(), Substitute.For<ITaskScheduler>(),
            Substitute.For<ILogger<SeriesService>>(), Substitute.For<IScrobblingService>(),
            localizationService, Substitute.For<IReadingListService>());

        var opdsService = new OpdsService(unitOfWork, localizationService,
            seriesService, Substitute.For<DownloadService>(),
            ds, readerService, mapper);

        return new Tuple<IOpdsService, IReaderService>(opdsService, readerService);
    }
    #endregion

    #region Continue Points

    [Fact]
    public async Task ContinuePoint_ShouldWorkWithProgress()
    {
        var (unitOfWork, context, mapper) = await CreateDatabase();
        var (opdsService, readerService) = SetupService(unitOfWork, mapper);

        // Setup Mock Data via Scanner for ease
        var series = new SeriesBuilder("Test")
            .WithVolume(new VolumeBuilder(API.Services.Tasks.Scanner.Parser.Parser.LooseLeafVolume)
                .WithChapter(new ChapterBuilder("1")
                    .WithPages(1)
                    .WithFile(new MangaFileBuilder(_testFilePath, MangaFormat.Archive, 1).Build())
                    .Build())
                .WithChapter(new ChapterBuilder("2")
                    .WithFile(new MangaFileBuilder(_testFilePath, MangaFormat.Archive, 1).Build())
                    .WithPages(1)
                    .Build())
                .Build())
            .Build();
        series.Library = new LibraryBuilder("Test Lib", LibraryType.Manga).Build();

        context.Series.Add(series);

        context.AppUser.Add(new AppUserBuilder("majora2007", "majora2007")
            .WithLibrary(series.Library)
            .WithLocale("en")
            .Build());

        await context.SaveChangesAsync();


        var user = await unitOfWork.UserRepository.GetUserByIdAsync(1, AppUserIncludes.Progress);
        Assert.NotNull(user);

        var firstChapter = await unitOfWork.ChapterRepository.GetChapterAsync(1);
        Assert.NotNull(firstChapter);

        // Mark Chapter 1 as read
        await readerService.MarkChaptersAsRead(user, 1, [firstChapter]);
        Assert.True(unitOfWork.HasChanges());
        await unitOfWork.CommitAsync();

        // Generate Series Feed and validate first element is a Continue From Chapter 2
        var feed = await opdsService.GetSeriesDetail(new OpdsItemsFromEntityIdRequest
        {
            ApiKey = user.ApiKey,
            Prefix = OpdsService.DefaultApiPrefix,
            BaseUrl = string.Empty,
            UserId = user.Id,
            EntityId = 1,
            PageNumber = 0
        });

        Assert.NotEmpty(feed.Entries);
        Assert.Equal(3, feed.Entries.Count);
        Assert.StartsWith("Continue Reading from", feed.Entries.First().Title);
    }

    [Fact]
    public async Task ContinuePoint_DoesntExist_WhenNoProgress()
    {
        var (unitOfWork, context, mapper) = await CreateDatabase();
        var (opdsService, readerService) = SetupService(unitOfWork, mapper);

        // Setup Mock Data via Scanner for ease
        var series = new SeriesBuilder("Test")
            .WithVolume(new VolumeBuilder(API.Services.Tasks.Scanner.Parser.Parser.LooseLeafVolume)
                .WithChapter(new ChapterBuilder("1")
                    .WithPages(1)
                    .WithFile(new MangaFileBuilder(_testFilePath, MangaFormat.Archive, 1).Build())
                    .Build())
                .WithChapter(new ChapterBuilder("2")
                    .WithFile(new MangaFileBuilder(_testFilePath, MangaFormat.Archive, 1).Build())
                    .WithPages(1)
                    .Build())
                .Build())
            .Build();
        series.Library = new LibraryBuilder("Test Lib", LibraryType.Manga).Build();

        context.Series.Add(series);

        context.AppUser.Add(new AppUserBuilder("majora2007", "majora2007")
            .WithLibrary(series.Library)
            .WithLocale("en")
            .Build());

        await context.SaveChangesAsync();


        var user = await unitOfWork.UserRepository.GetUserByIdAsync(1, AppUserIncludes.Progress);
        Assert.NotNull(user);

        var firstChapter = await unitOfWork.ChapterRepository.GetChapterAsync(1);
        Assert.NotNull(firstChapter);


        // Generate Series Feed and validate first element is a Continue From Chapter 2
        var feed = await opdsService.GetSeriesDetail(new OpdsItemsFromEntityIdRequest
        {
            ApiKey = user.ApiKey,
            Prefix = OpdsService.DefaultApiPrefix,
            BaseUrl = string.Empty,
            UserId = user.Id,
            EntityId = 1,
            PageNumber = 0
        });

        Assert.NotEmpty(feed.Entries);
        Assert.Equal(2, feed.Entries.Count);
    }
    #endregion

    #region Entity Feeds
    #endregion

    #region Detail Feeds
    #endregion

}
