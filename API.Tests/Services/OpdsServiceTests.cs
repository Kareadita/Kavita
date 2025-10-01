using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Abstractions;
using System.Linq;
using System.Threading.Tasks;
using API.Data;
using API.Data.Repositories;
using API.DTOs.OPDS.Requests;
using API.DTOs.Progress;
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

    private async Task<AppUser> SetupSeriesAndUser(DataContext context, IUnitOfWork unitOfWork, int numberOfSeries = 1)
    {
        var library = new LibraryBuilder("Test Lib", LibraryType.Manga).Build();

        unitOfWork.LibraryRepository.Add(library);
        await unitOfWork.CommitAsync();


        context.AppUser.Add(new AppUserBuilder("majora2007", "majora2007")
            .WithLibrary(library)
            .WithLocale("en")
            .Build());

        await context.SaveChangesAsync();

        Assert.NotEmpty(await unitOfWork.LibraryRepository.GetLibrariesForUserIdAsync(1));

        var counter = 0;
        foreach (var i in Enumerable.Range(0, numberOfSeries))
        {
            var series = new SeriesBuilder("Test " + (i + 1))
                .WithVolume(new VolumeBuilder(API.Services.Tasks.Scanner.Parser.Parser.LooseLeafVolume)
                    .WithChapter(new ChapterBuilder("1")
                        .WithSortOrder(counter)
                        .WithPages(10)
                        .WithFile(new MangaFileBuilder(_testFilePath, MangaFormat.Archive, 10).Build())
                        .Build())
                    .WithChapter(new ChapterBuilder("2")
                        .WithFile(new MangaFileBuilder(_testFilePath, MangaFormat.Archive, 10).Build())
                        .WithSortOrder(counter + 1)
                        .WithPages(10)
                        .Build())
                    .Build())
                .Build();
            series.Library = library;

            context.Series.Add(series);
            counter += 2;
        }

        await unitOfWork.CommitAsync();



        var user = await unitOfWork.UserRepository.GetUserByIdAsync(1, AppUserIncludes.Progress);
        Assert.NotNull(user);

        // // Build a reading list
        //
        // var readingList = new ReadingListBuilder("Test RL").WithAppUserId(user.Id).WithItem(new ReadingListItem
        // {
        //     SeriesId = 1,
        //     VolumeId = 1,
        //     ChapterId = 0,
        //     Order = 0,
        //     Series = null,
        //     Volume = null,
        //     Chapter = null
        // })


        return user;
    }

    #endregion

    #region Continue Points

    [Fact]
    public async Task ContinuePoint_ShouldWorkWithProgress()
    {
        var (unitOfWork, context, mapper) = await CreateDatabase();
        var (opdsService, readerService) = SetupService(unitOfWork, mapper);


        var user = await SetupSeriesAndUser(context, unitOfWork);

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


        var user = await SetupSeriesAndUser(context, unitOfWork);

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

    #region Misc

    [Fact]
    public async Task NoProgressEncoding()
    {
        var (unitOfWork, context, mapper) = await CreateDatabase();
        var (opdsService, readerService) = SetupService(unitOfWork, mapper);


        var user = await SetupSeriesAndUser(context, unitOfWork);

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
        Assert.Contains(OpdsService.NoReadingProgressIcon, feed.Entries.First().Title);
    }

    [Fact]
    public async Task QuarterProgressEncoding()
    {
        var (unitOfWork, context, mapper) = await CreateDatabase();
        var (opdsService, readerService) = SetupService(unitOfWork, mapper);


        var user = await SetupSeriesAndUser(context, unitOfWork);

        var firstChapter = await unitOfWork.ChapterRepository.GetChapterAsync(1);
        Assert.NotNull(firstChapter);

        // Mark Chapter 1 as read
        await readerService.SaveReadingProgress(new ProgressDto
        {
            VolumeId = firstChapter.VolumeId,
            ChapterId = firstChapter.Id,
            PageNum = 2, // 10 total pages
            SeriesId = 1,
            LibraryId = 1,
            BookScrollId = null,
            LastModifiedUtc = default
        }, user.Id);

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
        Assert.Contains(OpdsService.QuarterReadingProgressIcon, feed.Entries.First().Title);
    }


    [Fact]
    public async Task HalfProgressEncoding()
    {
        var (unitOfWork, context, mapper) = await CreateDatabase();
        var (opdsService, readerService) = SetupService(unitOfWork, mapper);


        var user = await SetupSeriesAndUser(context, unitOfWork);

        var firstChapter = await unitOfWork.ChapterRepository.GetChapterAsync(1);
        Assert.NotNull(firstChapter);

        // Mark Chapter 1 as read
        await readerService.SaveReadingProgress(new ProgressDto
        {
            VolumeId = firstChapter.VolumeId,
            ChapterId = firstChapter.Id,
            PageNum = 5, // 10 total pages
            SeriesId = 1,
            LibraryId = 1,
            BookScrollId = null,
            LastModifiedUtc = default
        }, user.Id);

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
        Assert.Contains(OpdsService.HalfReadingProgressIcon, feed.Entries.First().Title);
    }

    [Fact]
    public async Task AboveHalfProgressEncoding()
    {
        var (unitOfWork, context, mapper) = await CreateDatabase();
        var (opdsService, readerService) = SetupService(unitOfWork, mapper);


        var user = await SetupSeriesAndUser(context, unitOfWork);

        var firstChapter = await unitOfWork.ChapterRepository.GetChapterAsync(1);
        Assert.NotNull(firstChapter);

        // Mark Chapter 1 as read
        await readerService.SaveReadingProgress(new ProgressDto
        {
            VolumeId = firstChapter.VolumeId,
            ChapterId = firstChapter.Id,
            PageNum = 7, // 10 total pages
            SeriesId = 1,
            LibraryId = 1,
            BookScrollId = null,
            LastModifiedUtc = default
        }, user.Id);

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
        Assert.Contains(OpdsService.AboveHalfReadingProgressIcon, feed.Entries.First().Title);
    }

    [Fact]
    public async Task FullProgressEncoding()
    {
        var (unitOfWork, context, mapper) = await CreateDatabase();
        var (opdsService, readerService) = SetupService(unitOfWork, mapper);


        var user = await SetupSeriesAndUser(context, unitOfWork);

        var firstChapter = await unitOfWork.ChapterRepository.GetChapterAsync(1);
        Assert.NotNull(firstChapter);

        // Mark Chapter 1 as read
        await readerService.SaveReadingProgress(new ProgressDto
        {
            VolumeId = firstChapter.VolumeId,
            ChapterId = firstChapter.Id,
            PageNum = 10, // 10 total pages
            SeriesId = 1,
            LibraryId = 1,
            BookScrollId = null,
            LastModifiedUtc = default
        }, user.Id);

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
        Assert.Contains(OpdsService.FullReadingProgressIcon, feed.Entries[1].Title); // The continue from will show the 2nd chapter
    }

    #endregion

    #region Entity Feeds

    [Fact]
    public async Task PaginationWorks()
    {
        var (unitOfWork, context, mapper) = await CreateDatabase();
        var (opdsService, readerService) = SetupService(unitOfWork, mapper);
        var user = await SetupSeriesAndUser(context, unitOfWork, 100);

        var libs = await unitOfWork.LibraryRepository.GetLibrariesForUserIdAsync(1);
        var feed = await opdsService.GetSeriesFromLibrary(new OpdsItemsFromEntityIdRequest()
        {
            ApiKey = user.ApiKey,
            Prefix = OpdsService.DefaultApiPrefix,
            BaseUrl = string.Empty,
            UserId = user.Id,
            EntityId = libs.First().Id,
            PageNumber = OpdsService.FirstPageNumber
        });

        Assert.Equal(OpdsService.PageSize, feed.Entries.Count);
        var feed2 = await opdsService.GetSeriesFromLibrary(new OpdsItemsFromEntityIdRequest()
        {
            ApiKey = user.ApiKey,
            Prefix = OpdsService.DefaultApiPrefix,
            BaseUrl = string.Empty,
            UserId = user.Id,
            EntityId = libs.First().Id,
            PageNumber = OpdsService.FirstPageNumber
        });
        Assert.Equal(OpdsService.PageSize, feed.Entries.Count);

        // Ensure there is no overlap
        Assert.NotSame(feed.Entries.Select(e => e.Id),  feed2.Entries.Select(e => e.Id));




    }
    #endregion

    #region Detail Feeds
    #endregion

}
