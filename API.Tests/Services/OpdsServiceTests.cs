using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Abstractions;
using System.Linq;
using System.Threading.Tasks;
using API.Data;
using API.Data.Repositories;
using API.DTOs.OPDS;
using API.DTOs.OPDS.Requests;
using API.DTOs.Progress;
using API.Constants;
using API.Entities;
using API.Entities.Enums;
using API.Helpers;
using API.Helpers.Builders;
using API.Services;
using API.Services.Plus;
using API.Services.Reading;
using API.SignalR;
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

    private static Tuple<IOpdsService, IReaderService> SetupService(IUnitOfWork unitOfWork, IMapper mapper)
    {
        JobStorage.Current = new InMemoryStorage();

        var ds = new DirectoryService(Substitute.For<ILogger<DirectoryService>>(), new FileSystem());

        var readerService = new ReaderService(unitOfWork, Substitute.For<ILogger<ReaderService>>(),
            Substitute.For<IEventHub>(), Substitute.For<IImageService>(), ds,
            Substitute.For<IScrobblingService>(), Substitute.For<IReadingSessionService>(),
            Substitute.For<IClientInfoAccessor>(), Substitute.For<ISeriesService>(), Substitute.For<IEntityNamingService>(),
            Substitute.For<ILocalizationService>());

        var localizationService =
            new LocalizationService(ds, new MockHostingEnvironment(), Substitute.For<IMemoryCache>(), unitOfWork);

        var namingService = new EntityNamingService();

        var readingListService = new ReadingListService(unitOfWork, Substitute.For<ILogger<ReadingListService>>(),
            Substitute.For<IEventHub>(), Substitute.For<IImageService>(), Substitute.For<IDirectoryService>(),
            namingService);

        var seriesService = new SeriesService(unitOfWork, Substitute.For<IEventHub>(), Substitute.For<ITaskScheduler>(),
            Substitute.For<ILogger<SeriesService>>(),
            localizationService, Substitute.For<IReadingListService>(), namingService);

        var opdsService = new OpdsService(unitOfWork, localizationService,
            seriesService, Substitute.For<DownloadService>(),
            ds, readerService, namingService, readingListService);

        return new Tuple<IOpdsService, IReaderService>(opdsService, readerService);
    }

    private async Task<AppUser> SetupSeriesAndUser(DataContext context, IUnitOfWork unitOfWork, int numberOfSeries = 1)
    {
        var library = new LibraryBuilder("Test Lib").Build();

        unitOfWork.LibraryRepository.Add(library);
        await unitOfWork.CommitAsync();


        context.AppUser.Add(new AppUserBuilder("majora2007", "majora2007")
            .WithLibrary(library)
            .WithLocale("en")
            .WithRole(PolicyConstants.AdminRole)
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

        var user = await unitOfWork.UserRepository.GetUserByIdAsync(1, AppUserIncludes.Progress | AppUserIncludes.WantToRead | AppUserIncludes.Collections);
        Assert.NotNull(user);

        // Setup SideNav streams for library
        user.SideNavStreams = new List<AppUserSideNavStream>
        {
            new AppUserSideNavStream
            {
                Name = library.Name,
                IsProvided = true,
                Order = 0,
                StreamType = SideNavStreamType.Library,
                Visible = true,
                LibraryId = library.Id,
                AppUserId = user.Id
            }
        };
        await unitOfWork.CommitAsync();

        return user;
    }

    private static async Task<AppUserCollection> CreateCollection(IUnitOfWork unitOfWork, string title, int userId, params int[] seriesIds)
    {
        var collectionBuilder = new AppUserCollectionBuilder(title, promoted: true);

        foreach (var seriesId in seriesIds)
        {
            var series = await unitOfWork.SeriesRepository.GetSeriesByIdAsync(seriesId);
            if (series != null)
            {
                collectionBuilder.WithItem(series);
            }
        }

        var collection = collectionBuilder.Build();

        // Get the user and add collection
        var user = await unitOfWork.UserRepository.GetUserByIdAsync(userId, AppUserIncludes.Collections);
        if (user != null)
        {
            user.Collections.Add(collection);
            await unitOfWork.CommitAsync();
        }

        return collection;
    }

    private static async Task<ReadingList> CreateReadingList(DataContext context, IUnitOfWork unitOfWork, string title, int userId, List<(int seriesId, int volumeId, int chapterId)> items)
    {
        var readingList = new ReadingListBuilder(title)
            .WithAppUserId(userId)
            .Build();

        var order = 0;
        foreach (var (seriesId, volumeId, chapterId) in items)
        {
            var readingListItem = new ReadingListItem
            {
                SeriesId = seriesId,
                VolumeId = volumeId,
                ChapterId = chapterId,
                Order = order++
            };
            readingList.Items.Add(readingListItem);
        }

        context.ReadingList.Add(readingList);
        await unitOfWork.CommitAsync();

        return readingList;
    }

    private static async Task<AppUserSmartFilter> CreateSmartFilter(DataContext context, int userId, string name, string filter)
    {
        var smartFilter = new AppUserSmartFilter
        {
            Name = name,
            Filter = filter,
            AppUserId = userId
        };

        context.AppUserSmartFilter.Add(smartFilter);
        await context.SaveChangesAsync();

        return smartFilter;
    }

    private static void ValidatePaginationLinks(Feed feed, int pageNumber, bool expectNext, bool expectPrev)
    {
        var nextLink = feed.Links.FirstOrDefault(l => l.Rel == FeedLinkRelation.Next);
        var prevLink = feed.Links.FirstOrDefault(l => l.Rel == FeedLinkRelation.Prev);

        if (expectNext)
        {
            Assert.NotNull(nextLink);
        }
        else
        {
            Assert.Null(nextLink);
        }

        if (expectPrev)
        {
            Assert.NotNull(prevLink);
        }
        else
        {
            Assert.Null(prevLink);
        }
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
        await readerService.MarkChaptersAsRead(user, 1, [firstChapter]);
        await unitOfWork.CommitAsync();

        var feed = await opdsService.GetSeriesDetail(new OpdsItemsFromEntityIdRequest
        {
            ApiKey = user.GetOpdsAuthKey(),
            Prefix = OpdsService.DefaultApiPrefix,
            BaseUrl = string.Empty,
            UserId = user.Id,
            Preferences = await unitOfWork.UserRepository.GetOpdsPreferences(user.Id),
            EntityId = 1,
            PageNumber = 0
        });

        Assert.Equal(3, feed.Entries.Count);
        Assert.StartsWith("Continue Reading from", feed.Entries.First().Title);
    }

    [Fact]
    public async Task ContinuePoint_WithProgress_NotEnabled()
    {
        var (unitOfWork, context, mapper) = await CreateDatabase();
        var (opdsService, readerService) = SetupService(unitOfWork, mapper);
        var user = await SetupSeriesAndUser(context, unitOfWork);

        // Disable Continue Point
        var user2 = await unitOfWork.UserRepository.GetUserByIdAsync(user.Id, AppUserIncludes.UserPreferences);
        user2.UserPreferences.OpdsPreferences.IncludeContinueFrom = false;
        unitOfWork.UserRepository.Update(user2);
        await unitOfWork.CommitAsync();

        var firstChapter = await unitOfWork.ChapterRepository.GetChapterAsync(1);
        await readerService.MarkChaptersAsRead(user, 1, [firstChapter]);
        await unitOfWork.CommitAsync();

        var feed = await opdsService.GetSeriesDetail(new OpdsItemsFromEntityIdRequest
        {
            ApiKey = user.GetOpdsAuthKey(),
            Prefix = OpdsService.DefaultApiPrefix,
            BaseUrl = string.Empty,
            UserId = user.Id,
            Preferences = user2.UserPreferences.OpdsPreferences,
            EntityId = 1,
            PageNumber = 0
        });

        Assert.Equal(2, feed.Entries.Count);
        Assert.False(feed.Entries.First().Title.StartsWith("Continue Reading from"));
    }

    [Fact]
    public async Task ContinuePoint_DoesntExist_WhenNoProgress()
    {
        var (unitOfWork, context, mapper) = await CreateDatabase();
        var (opdsService, _) = SetupService(unitOfWork, mapper);
        var user = await SetupSeriesAndUser(context, unitOfWork);

        var feed = await opdsService.GetSeriesDetail(new OpdsItemsFromEntityIdRequest
        {
            ApiKey = user.GetOpdsAuthKey(),
            Prefix = OpdsService.DefaultApiPrefix,
            BaseUrl = string.Empty,
            Preferences = await unitOfWork.UserRepository.GetOpdsPreferences(user.Id),
            UserId = user.Id,
            EntityId = 1,
            PageNumber = 0
        });

        Assert.Equal(2, feed.Entries.Count);
    }
    #endregion

    #region Reading Progress Icons

    [Theory]
    [InlineData(0, "NoReadingProgressIcon", 0)] // No progress
    [InlineData(2, "QuarterReadingProgressIcon", 0)] // 2/10 pages = quarter
    [InlineData(5, "HalfReadingProgressIcon", 0)] // 5/10 pages = half
    [InlineData(7, "AboveHalfReadingProgressIcon", 0)] // 7/10 pages = above half
    [InlineData(10, "FullReadingProgressIcon", 1)] // 10/10 pages = full (shows in continue reading)
    public async Task ReadingProgressIconEncoding(int pageNum, string expectedIconField, int entryIndex)
    {
        var (unitOfWork, context, mapper) = await CreateDatabase();
        var (opdsService, readerService) = SetupService(unitOfWork, mapper);
        var user = await SetupSeriesAndUser(context, unitOfWork);

        var firstChapter = await unitOfWork.ChapterRepository.GetChapterAsync(1);
        Assert.NotNull(firstChapter);

        if (pageNum > 0)
        {
            await readerService.SaveReadingProgress(new ProgressDto
            {
                VolumeId = firstChapter.VolumeId,
                ChapterId = firstChapter.Id,
                PageNum = pageNum,
                SeriesId = 1,
                LibraryId = 1,
                BookScrollId = null,
                LastModifiedUtc = default
            }, user.Id);
        }

        var pref = await unitOfWork.UserRepository.GetOpdsPreferences(user.Id);
        pref.EmbedProgressIndicator = true;

        var feed = await opdsService.GetSeriesDetail(new OpdsItemsFromEntityIdRequest
        {
            ApiKey = user.GetOpdsAuthKey(),
            Prefix = OpdsService.DefaultApiPrefix,
            BaseUrl = string.Empty,
            UserId = user.Id,
            Preferences = pref,
            EntityId = 1,
            PageNumber = 0
        });

        Assert.NotEmpty(feed.Entries);
        var expectedIcon = typeof(OpdsService).GetField(expectedIconField)?.GetValue(null) as string;
        Assert.NotNull(expectedIcon);
        Assert.Contains(expectedIcon, feed.Entries[entryIndex].Title);
    }

    [Fact]
    public async Task ReadingIcon_NotEnabled()
    {
        var (unitOfWork, context, mapper) = await CreateDatabase();
        var (opdsService, readerService) = SetupService(unitOfWork, mapper);
        var user = await SetupSeriesAndUser(context, unitOfWork);

        // Disable Continue Point
        var user2 = await unitOfWork.UserRepository.GetUserByIdAsync(user.Id, AppUserIncludes.UserPreferences);
        user2.UserPreferences.OpdsPreferences.EmbedProgressIndicator = false;
        unitOfWork.UserRepository.Update(user2);
        await unitOfWork.CommitAsync();

        var firstChapter = await unitOfWork.ChapterRepository.GetChapterAsync(1);
        Assert.NotNull(firstChapter);

        await readerService.SaveReadingProgress(new ProgressDto
        {
            VolumeId = firstChapter.VolumeId,
            ChapterId = firstChapter.Id,
            PageNum = 2,
            SeriesId = 1,
            LibraryId = 1,
            BookScrollId = null,
            LastModifiedUtc = default
        }, user.Id);

        var feed = await opdsService.GetSeriesDetail(new OpdsItemsFromEntityIdRequest
        {
            ApiKey = user.GetOpdsAuthKey(),
            Prefix = OpdsService.DefaultApiPrefix,
            BaseUrl = string.Empty,
            UserId = user.Id,
            Preferences = user2.UserPreferences.OpdsPreferences,
            EntityId = 1,
            PageNumber = 0
        });

        List<string> icons = [OpdsService.NoReadingProgressIcon, OpdsService.QuarterReadingProgressIcon, OpdsService.HalfReadingProgressIcon, OpdsService.AboveHalfReadingProgressIcon, OpdsService.FullReadingProgressIcon];
        Assert.NotEmpty(feed.Entries);
        Assert.DoesNotContain(feed.Entries, e => icons.Any(icon => e.Title.Contains(icon)));
    }

    #endregion

    #region Misc

    [Fact]
    public async Task Search_EmptyQuery_ThrowsException()
    {
        var (unitOfWork, context, mapper) = await CreateDatabase();
        var (opdsService, _) = SetupService(unitOfWork, mapper);
        var user = await SetupSeriesAndUser(context, unitOfWork);

        await Assert.ThrowsAsync<Exceptions.OpdsException>(async () =>
        {
            await opdsService.Search(new OpdsSearchRequest
            {
                ApiKey = user.GetOpdsAuthKey(),
                Prefix = OpdsService.DefaultApiPrefix,
                BaseUrl = string.Empty,
                UserId = user.Id,
                Preferences = await unitOfWork.UserRepository.GetOpdsPreferences(user.Id),
                Query = string.Empty
            });
        });
    }

    [Fact]
    public async Task GetCatalogue_ContainsDashboardStreams()
    {
        var (unitOfWork, context, mapper) = await CreateDatabase();
        var (opdsService, _) = SetupService(unitOfWork, mapper);
        var user = await SetupSeriesAndUser(context, unitOfWork);

        // Setup dashboard streams
        user.DashboardStreams = new List<AppUserDashboardStream>
        {
            new AppUserDashboardStream
            {
                Name = "On Deck",
                IsProvided = true,
                Order = 0,
                StreamType = DashboardStreamType.OnDeck,
                Visible = true,
                AppUserId = user.Id
            },
            new AppUserDashboardStream
            {
                Name = "Recently Added",
                IsProvided = true,
                Order = 1,
                StreamType = DashboardStreamType.NewlyAdded,
                Visible = true,
                AppUserId = user.Id
            }
        };
        await unitOfWork.CommitAsync();

        var feed = await opdsService.GetCatalogue(new OpdsCatalogueRequest
        {
            ApiKey = user.GetOpdsAuthKey(),
            Prefix = OpdsService.DefaultApiPrefix,
            BaseUrl = string.Empty,
            UserId = user.Id
        });

        Assert.NotEmpty(feed.Entries);
        Assert.Contains(feed.Entries, e => e.Id == "onDeck");
        Assert.Contains(feed.Entries, e => e.Id == "recentlyAdded");
        Assert.Contains(feed.Entries, e => e.Id == "readingList");
        Assert.Contains(feed.Entries, e => e.Id == "wantToRead");
        Assert.Contains(feed.Entries, e => e.Id == "allLibraries");
        Assert.Contains(feed.Entries, e => e.Id == "allCollections");
    }

    #endregion

    #region Paginated Catalogue Tests

    [Fact]
    public async Task GetSmartFilters_WithPagination()
    {
        var (unitOfWork, context, mapper) = await CreateDatabase();
        var (opdsService, _) = SetupService(unitOfWork, mapper);
        var user = await SetupSeriesAndUser(context, unitOfWork, OpdsService.PageSize + 5);

        // Create smart filters (more than page size)
        for (var i = 0; i < OpdsService.PageSize + 5; i++)
        {
            await CreateSmartFilter(context, user.Id, $"Filter {i}", "combination=0");
        }

        // Test page 1
        var feed = await opdsService.GetSmartFilters(new OpdsPaginatedCatalogueRequest
        {
            ApiKey = user.GetOpdsAuthKey(),
            Prefix = OpdsService.DefaultApiPrefix,
            BaseUrl = string.Empty,
            UserId = user.Id,
            Preferences = await unitOfWork.UserRepository.GetOpdsPreferences(user.Id),
            PageNumber = OpdsService.FirstPageNumber
        });

        Assert.Equal(OpdsService.PageSize, feed.Entries.Count);
        Assert.Equal(OpdsService.PageSize + 5, feed.Total);
        ValidatePaginationLinks(feed, OpdsService.FirstPageNumber, expectNext: true, expectPrev: false);
    }

    [Fact]
    public async Task GetLibraries_ReturnsAllLibraries()
    {
        var (unitOfWork, context, mapper) = await CreateDatabase();
        var (opdsService, _) = SetupService(unitOfWork, mapper);
        var user = await SetupSeriesAndUser(context, unitOfWork);

        var feed = await opdsService.GetLibraries(new OpdsPaginatedCatalogueRequest
        {
            ApiKey = user.GetOpdsAuthKey(),
            Prefix = OpdsService.DefaultApiPrefix,
            BaseUrl = string.Empty,
            UserId = user.Id,
            Preferences = await unitOfWork.UserRepository.GetOpdsPreferences(user.Id),
            PageNumber = OpdsService.FirstPageNumber
        });

        Assert.Single(feed.Entries);
        Assert.Contains("Test Lib", feed.Entries.First().Title);
    }

    [Fact]
    public async Task GetWantToRead_WithPagination()
    {
        var (unitOfWork, context, mapper) = await CreateDatabase();
        var (opdsService, _) = SetupService(unitOfWork, mapper);
        var user = await SetupSeriesAndUser(context, unitOfWork, OpdsService.PageSize + 5);

        // Mark series as want to read
        for (var i = 1; i <= OpdsService.PageSize + 5; i++)
        {
            var series = await unitOfWork.SeriesRepository.GetSeriesByIdAsync(i);
            if (series != null)
            {
                user.WantToRead.Add(new AppUserWantToRead
                {
                    SeriesId = series.Id,
                    AppUserId = user.Id
                });
            }
        }
        await unitOfWork.CommitAsync();

        // Test page 1
        var feed = await opdsService.GetWantToRead(new OpdsPaginatedCatalogueRequest
        {
            ApiKey = user.GetOpdsAuthKey(),
            Prefix = OpdsService.DefaultApiPrefix,
            BaseUrl = string.Empty,
            UserId = user.Id,
            Preferences = await unitOfWork.UserRepository.GetOpdsPreferences(user.Id),
            PageNumber = OpdsService.FirstPageNumber
        });

        Assert.Equal(OpdsService.PageSize, feed.Entries.Count);
        ValidatePaginationLinks(feed, OpdsService.FirstPageNumber, expectNext: true, expectPrev: false);
    }

    [Fact]
    public async Task GetCollections_WithPagination()
    {
        var (unitOfWork, context, mapper) = await CreateDatabase();
        var (opdsService, _) = SetupService(unitOfWork, mapper);
        var user = await SetupSeriesAndUser(context, unitOfWork, OpdsService.PageSize + 5);

        // Create collections (more than page size)
        var firstSeries = await unitOfWork.SeriesRepository.GetSeriesByIdAsync(1);
        user.Collections ??= new List<AppUserCollection>();

        for (var i = 0; i < OpdsService.PageSize + 5; i++)
        {
            user.Collections.Add(new AppUserCollectionBuilder($"Collection {i}").WithItem(firstSeries).Build());
        }

        await unitOfWork.CommitAsync();

        // Test page 1
        var feed = await opdsService.GetCollections(new OpdsPaginatedCatalogueRequest
        {
            ApiKey = user.GetOpdsAuthKey(),
            Prefix = OpdsService.DefaultApiPrefix,
            BaseUrl = string.Empty,
            UserId = user.Id,
            Preferences = await unitOfWork.UserRepository.GetOpdsPreferences(user.Id),
            PageNumber = OpdsService.FirstPageNumber
        });

        // Collections should exist
        Assert.NotEmpty(feed.Entries);

        // If we have more than page size, verify pagination
        if (feed.Total > OpdsService.PageSize)
        {
            Assert.Equal(OpdsService.PageSize, feed.Entries.Count);
            ValidatePaginationLinks(feed, OpdsService.FirstPageNumber, expectNext: true, expectPrev: false);
        }
    }

    [Fact]
    public async Task GetReadingLists_WithPagination()
    {
        var (unitOfWork, context, mapper) = await CreateDatabase();
        var (opdsService, _) = SetupService(unitOfWork, mapper);
        var user = await SetupSeriesAndUser(context, unitOfWork, OpdsService.PageSize + 5);

        // Create reading lists (more than page size)
        for (var i = 0; i < OpdsService.PageSize + 5; i++)
        {
            await CreateReadingList(context, unitOfWork, $"Reading List {i}", user.Id,
                [(1, 1, 1)]);
        }

        // Test page 1
        var feed = await opdsService.GetReadingLists(new OpdsPaginatedCatalogueRequest
        {
            ApiKey = user.GetOpdsAuthKey(),
            Prefix = OpdsService.DefaultApiPrefix,
            BaseUrl = string.Empty,
            UserId = user.Id,
            Preferences = await unitOfWork.UserRepository.GetOpdsPreferences(user.Id),
            PageNumber = OpdsService.FirstPageNumber
        });

        Assert.Equal(OpdsService.PageSize, feed.Entries.Count);
        ValidatePaginationLinks(feed, OpdsService.FirstPageNumber, expectNext: true, expectPrev: false);
    }

    [Fact]
    public async Task GetRecentlyAdded_WithPagination()
    {
        var (unitOfWork, context, mapper) = await CreateDatabase();
        var (opdsService, _) = SetupService(unitOfWork, mapper);
        var user = await SetupSeriesAndUser(context, unitOfWork, OpdsService.PageSize + 5);

        // Test page 1
        var feed = await opdsService.GetRecentlyAdded(new OpdsPaginatedCatalogueRequest
        {
            ApiKey = user.GetOpdsAuthKey(),
            Prefix = OpdsService.DefaultApiPrefix,
            BaseUrl = string.Empty,
            UserId = user.Id,
            Preferences = await unitOfWork.UserRepository.GetOpdsPreferences(user.Id),
            PageNumber = OpdsService.FirstPageNumber
        });

        Assert.Equal(OpdsService.PageSize, feed.Entries.Count);
        ValidatePaginationLinks(feed, OpdsService.FirstPageNumber, expectNext: true, expectPrev: false);
    }

    [Fact]
    public async Task GetRecentlyUpdated_WithPagination()
    {
        var (unitOfWork, context, mapper) = await CreateDatabase();
        var (opdsService, readerService) = SetupService(unitOfWork, mapper);
        var user = await SetupSeriesAndUser(context, unitOfWork, OpdsService.PageSize + 5);

        // Mark some chapters as read to create updated series
        for (var i = 1; i <= OpdsService.PageSize + 5; i++)
        {
            var chapter = await unitOfWork.ChapterRepository.GetChapterAsync(i * 2 - 1);
            if (chapter != null)
            {
                await readerService.MarkChaptersAsRead(user, i, [chapter]);
            }
        }
        await unitOfWork.CommitAsync();

        // Test page 1
        var feed = await opdsService.GetRecentlyUpdated(new OpdsPaginatedCatalogueRequest
        {
            ApiKey = user.GetOpdsAuthKey(),
            Prefix = OpdsService.DefaultApiPrefix,
            BaseUrl = string.Empty,
            UserId = user.Id,
            Preferences = await unitOfWork.UserRepository.GetOpdsPreferences(user.Id),
            PageNumber = OpdsService.FirstPageNumber
        });

        Assert.NotEmpty(feed.Entries);
    }

    [Fact]
    public async Task GetOnDeck_WithPagination()
    {
        var (unitOfWork, context, mapper) = await CreateDatabase();
        var (opdsService, readerService) = SetupService(unitOfWork, mapper);
        var user = await SetupSeriesAndUser(context, unitOfWork, OpdsService.PageSize + 5);

        // Mark first chapter as read for each series to create on-deck items
        for (var i = 1; i <= OpdsService.PageSize + 5; i++)
        {
            var chapter = await unitOfWork.ChapterRepository.GetChapterAsync(i * 2 - 1);
            if (chapter != null)
            {
                await readerService.MarkChaptersAsRead(user, i, [chapter]);
            }
        }
        await unitOfWork.CommitAsync();

        // Test page 1
        var feed = await opdsService.GetOnDeck(new OpdsPaginatedCatalogueRequest
        {
            ApiKey = user.GetOpdsAuthKey(),
            Prefix = OpdsService.DefaultApiPrefix,
            BaseUrl = string.Empty,
            UserId = user.Id,
            Preferences = await unitOfWork.UserRepository.GetOpdsPreferences(user.Id),
            PageNumber = OpdsService.FirstPageNumber
        });

        Assert.NotEmpty(feed.Entries);
    }

    #endregion

    #region Entity Feeds

    [Fact]
    public async Task PaginationWorks()
    {
        var (unitOfWork, context, mapper) = await CreateDatabase();
        var (opdsService, _) = SetupService(unitOfWork, mapper);
        var user = await SetupSeriesAndUser(context, unitOfWork, OpdsService.PageSize * 2);

        var libs = (await unitOfWork.LibraryRepository.GetLibrariesForUserIdAsync(1)).ToList();

        // Test page 1
        var feed = await opdsService.GetSeriesFromLibrary(new OpdsItemsFromEntityIdRequest()
        {
            ApiKey = user.GetOpdsAuthKey(),
            Prefix = OpdsService.DefaultApiPrefix,
            BaseUrl = string.Empty,
            UserId = user.Id,
            Preferences = await unitOfWork.UserRepository.GetOpdsPreferences(user.Id),
            EntityId = libs.First().Id,
            PageNumber = OpdsService.FirstPageNumber
        });

        Assert.Equal(OpdsService.PageSize, feed.Entries.Count);
        Assert.Equal(OpdsService.PageSize * 2, feed.Total);
        ValidatePaginationLinks(feed, OpdsService.FirstPageNumber, expectNext: true, expectPrev: false);

        // Test page 2
        var feed2 = await opdsService.GetSeriesFromLibrary(new OpdsItemsFromEntityIdRequest()
        {
            ApiKey = user.GetOpdsAuthKey(),
            Prefix = OpdsService.DefaultApiPrefix,
            BaseUrl = string.Empty,
            UserId = user.Id,
            Preferences = await unitOfWork.UserRepository.GetOpdsPreferences(user.Id),
            EntityId = libs.First().Id,
            PageNumber = OpdsService.FirstPageNumber + 1
        });
        Assert.Equal(OpdsService.PageSize, feed2.Entries.Count);

        // Ensure there is no overlap between page 1 and page 2
        var page1Ids = feed.Entries.Select(e => e.Id).ToList();
        var page2Ids = feed2.Entries.Select(e => e.Id).ToList();
        Assert.Empty(page1Ids.Intersect(page2Ids));

        // Validate page 2 pagination - should have prev link but no next link (last page)
        ValidatePaginationLinks(feed2, OpdsService.FirstPageNumber + 1, expectNext: false, expectPrev: true);
    }

    [Fact]
    public async Task GetMoreInGenre_WithPagination()
    {
        var (unitOfWork, context, mapper) = await CreateDatabase();
        var (opdsService, _) = SetupService(unitOfWork, mapper);
        var user = await SetupSeriesAndUser(context, unitOfWork, OpdsService.PageSize + 5);

        // Add genre to all series
        var genre = new GenreBuilder("Action").Build();
        context.Genre.Add(genre);
        await context.SaveChangesAsync();

        for (var i = 1; i <= OpdsService.PageSize + 5; i++)
        {
            var series = await unitOfWork.SeriesRepository.GetSeriesByIdAsync(i);
            if (series?.Metadata != null)
            {
                series.Metadata.Genres.Add(genre);
            }
        }
        await unitOfWork.CommitAsync();

        // Test page 1
        var feed = await opdsService.GetMoreInGenre(new OpdsItemsFromEntityIdRequest
        {
            ApiKey = user.GetOpdsAuthKey(),
            Prefix = OpdsService.DefaultApiPrefix,
            BaseUrl = string.Empty,
            UserId = user.Id,
            Preferences = await unitOfWork.UserRepository.GetOpdsPreferences(user.Id),
            EntityId = genre.Id,
            PageNumber = OpdsService.FirstPageNumber
        });

        Assert.Equal(OpdsService.PageSize, feed.Entries.Count);
        ValidatePaginationLinks(feed, OpdsService.FirstPageNumber, expectNext: true, expectPrev: false);
    }

    #endregion

    #region Detail Feeds

    [Fact]
    public async Task GetSeriesFromSmartFilter_WithPagination()
    {
        var (unitOfWork, context, mapper) = await CreateDatabase();
        var (opdsService, _) = SetupService(unitOfWork, mapper);
        var user = await SetupSeriesAndUser(context, unitOfWork, OpdsService.PageSize + 5);

        // Create a smart filter that matches series containing "Test" (all test series)
        var smartFilter = await CreateSmartFilter(context, user.Id, "Test Filter", "combination=0");

        // Test page 1
        var feed = await opdsService.GetSeriesFromSmartFilter(new OpdsItemsFromEntityIdRequest
        {
            ApiKey = user.GetOpdsAuthKey(),
            Prefix = OpdsService.DefaultApiPrefix,
            BaseUrl = string.Empty,
            UserId = user.Id,
            Preferences = await unitOfWork.UserRepository.GetOpdsPreferences(user.Id),
            EntityId = smartFilter.Id,
            PageNumber = OpdsService.FirstPageNumber
        });

        Assert.Equal(OpdsService.PageSize, feed.Entries.Count);
        ValidatePaginationLinks(feed, OpdsService.FirstPageNumber, expectNext: true, expectPrev: false);
    }

    [Fact]
    public async Task GetSeriesFromCollection_WithPagination()
    {
        var (unitOfWork, context, mapper) = await CreateDatabase();
        var (opdsService, _) = SetupService(unitOfWork, mapper);
        var user = await SetupSeriesAndUser(context, unitOfWork, OpdsService.PageSize + 5);

        // Create a collection with PageSize + 1 series
        var seriesIds = Enumerable.Range(1, OpdsService.PageSize + 1).ToArray();
        var collection = await CreateCollection(unitOfWork, "Test Collection", user.Id, seriesIds);

        // Test page 1
        var feed = await opdsService.GetSeriesFromCollection(new OpdsItemsFromEntityIdRequest
        {
            ApiKey = user.GetOpdsAuthKey(),
            Prefix = OpdsService.DefaultApiPrefix,
            BaseUrl = string.Empty,
            UserId = user.Id,
            Preferences = await unitOfWork.UserRepository.GetOpdsPreferences(user.Id),
            EntityId = collection.Id,
            PageNumber = OpdsService.FirstPageNumber
        });

        Assert.NotEmpty(feed.Entries);

        // If we have pagination, verify it
        if (feed.Total > OpdsService.PageSize)
        {
            Assert.Equal(OpdsService.PageSize, feed.Entries.Count);
            ValidatePaginationLinks(feed, OpdsService.FirstPageNumber, expectNext: true, expectPrev: false);
        }

        // Validate all entries are from the collection
        foreach (var entry in feed.Entries)
        {
            var seriesId = int.Parse(entry.Id);
            Assert.True(seriesId <= OpdsService.PageSize + 5);
        }
    }

    [Fact]
    public async Task GetSeriesFromLibrary_WithPagination()
    {
        var (unitOfWork, context, mapper) = await CreateDatabase();
        var (opdsService, _) = SetupService(unitOfWork, mapper);
        var user = await SetupSeriesAndUser(context, unitOfWork, OpdsService.PageSize + 5);

        var libraries = await unitOfWork.LibraryRepository.GetLibrariesForUserIdAsync(user.Id);
        var library = libraries.First();

        // Test page 1
        var feed = await opdsService.GetSeriesFromLibrary(new OpdsItemsFromEntityIdRequest
        {
            ApiKey = user.GetOpdsAuthKey(),
            Prefix = OpdsService.DefaultApiPrefix,
            BaseUrl = string.Empty,
            UserId = user.Id,
            Preferences = await unitOfWork.UserRepository.GetOpdsPreferences(user.Id),
            EntityId = library.Id,
            PageNumber = OpdsService.FirstPageNumber
        });

        Assert.Equal(OpdsService.PageSize, feed.Entries.Count);
        ValidatePaginationLinks(feed, OpdsService.FirstPageNumber, expectNext: true, expectPrev: false);
    }

    [Fact]
    public async Task GetReadingListItems_WithPagination()
    {
        var (unitOfWork, context, mapper) = await CreateDatabase();
        var (opdsService, _) = SetupService(unitOfWork, mapper);
        var user = await SetupSeriesAndUser(context, unitOfWork, OpdsService.PageSize + 5);

        // Create reading list with items from all series (chapter 1 only)
        var items = new List<(int, int, int)>();
        for (var i = 1; i <= OpdsService.PageSize + 5; i++)
        {
            items.Add((i, 1, i * 2 - 1)); // seriesId, volumeId, chapterId
        }
        var readingList = await CreateReadingList(context, unitOfWork, "Test Reading List", user.Id, items);

        // Test page 1
        var feed = await opdsService.GetReadingListItems(new OpdsItemsFromEntityIdRequest
        {
            ApiKey = user.GetOpdsAuthKey(),
            Prefix = OpdsService.DefaultApiPrefix,
            BaseUrl = string.Empty,
            UserId = user.Id,
            Preferences = await unitOfWork.UserRepository.GetOpdsPreferences(user.Id),
            EntityId = readingList.Id,
            PageNumber = OpdsService.FirstPageNumber
        });


        Assert.True(feed.Entries.Count == UserParams.Default.PageSize);
        Assert.True(feed.Total >= OpdsService.PageSize);

        // Validate pagination - reading lists have complex pagination due to continue reading items
        // First page should never have prev link
        var prevLink = feed.Links.FirstOrDefault(l => l.Rel == FeedLinkRelation.Prev);
        Assert.Null(prevLink);
    }

    /// <summary>
    /// Reading lists have unique pagination implementation thus need explicit testing
    /// </summary>
    [Fact]
    public async Task GetReadingListItems_ContinueFromItem_WhenFirst2PagesFullyRead()
    {
        var (unitOfWork, context, mapper) = await CreateDatabase();
        var (opdsService, readerService) = SetupService(unitOfWork, mapper);

        // Create enough series for 2+ pages (UserParams.Default.PageSize * 2 + 5)
        var totalItems = UserParams.Default.PageSize * 2 + 5;
        var user = await SetupSeriesAndUser(context, unitOfWork, totalItems);

        // Create reading list with items from all series (chapter 1 only)
        var items = new List<(int, int, int)>();
        for (var i = 1; i <= totalItems; i++)
        {
            items.Add((i, 1, i * 2 - 1)); // seriesId, volumeId, chapterId
        }
        var readingList = await CreateReadingList(context, unitOfWork, "Test Reading List", user.Id, items);

        // Mark all chapters in first 2 pages as fully read
        var itemsInFirst2Pages = UserParams.Default.PageSize * 2;
        for (var i = 1; i <= itemsInFirst2Pages; i++)
        {
            var chapterId = i * 2 - 1;
            var chapter = await unitOfWork.ChapterRepository.GetChapterAsync(chapterId);
            if (chapter != null)
            {
                await readerService.MarkChaptersAsRead(user, i, [chapter]);
            }
        }
        await unitOfWork.CommitAsync();

        // Test page 1 - should include continue reading item at the top
        var feed = await opdsService.GetReadingListItems(new OpdsItemsFromEntityIdRequest
        {
            ApiKey = user.GetOpdsAuthKey(),
            Prefix = OpdsService.DefaultApiPrefix,
            BaseUrl = string.Empty,
            UserId = user.Id,
            Preferences = await unitOfWork.UserRepository.GetOpdsPreferences(user.Id),
            EntityId = readingList.Id,
            PageNumber = OpdsService.FirstPageNumber
        });

        Assert.NotEmpty(feed.Entries);

        // Verify continue reading item is inserted at the top
        var firstEntry = feed.Entries.First();
        Assert.Contains("Continue Reading from", firstEntry.Title);

        // The continue reading should point to the first unread item (page 3, first item)
        var expectedNextUnreadItemIndex = itemsInFirst2Pages + 1; // First item of page 3
        Assert.Contains($"Test {expectedNextUnreadItemIndex}", firstEntry.Title);
    }

    [Fact]
    public async Task GetSeriesDetail_ReturnsChapters()
    {
        var (unitOfWork, context, mapper) = await CreateDatabase();
        var (opdsService, _) = SetupService(unitOfWork, mapper);
        var user = await SetupSeriesAndUser(context, unitOfWork);

        var feed = await opdsService.GetSeriesDetail(new OpdsItemsFromEntityIdRequest
        {
            ApiKey = user.GetOpdsAuthKey(),
            Prefix = OpdsService.DefaultApiPrefix,
            BaseUrl = string.Empty,
            UserId = user.Id,
            Preferences = await unitOfWork.UserRepository.GetOpdsPreferences(user.Id),
            EntityId = 1,
            PageNumber = OpdsService.FirstPageNumber
        });

        Assert.NotEmpty(feed.Entries);
        Assert.Equal(2, feed.Entries.Count); // 2 chapters
    }

    [Fact]
    public async Task GetItemsFromVolume_ReturnsChapters()
    {
        var (unitOfWork, context, mapper) = await CreateDatabase();
        var (opdsService, _) = SetupService(unitOfWork, mapper);
        var user = await SetupSeriesAndUser(context, unitOfWork);

        var feed = await opdsService.GetItemsFromVolume(new OpdsItemsFromCompoundEntityIdsRequest
        {
            ApiKey = user.GetOpdsAuthKey(),
            Prefix = OpdsService.DefaultApiPrefix,
            BaseUrl = string.Empty,
            UserId = user.Id,
            Preferences = await unitOfWork.UserRepository.GetOpdsPreferences(user.Id),
            SeriesId = 1,
            VolumeId = 1,
            PageNumber = OpdsService.FirstPageNumber
        });

        Assert.NotEmpty(feed.Entries);
        // May include continue reading item if there's progress
        Assert.True(feed.Entries.Count >= 2);
    }

    [Fact]
    public async Task GetItemsFromChapter_ReturnsFiles()
    {
        var (unitOfWork, context, mapper) = await CreateDatabase();
        var (opdsService, _) = SetupService(unitOfWork, mapper);
        var user = await SetupSeriesAndUser(context, unitOfWork);

        var feed = await opdsService.GetItemsFromChapter(new OpdsItemsFromCompoundEntityIdsRequest
        {
            ApiKey = user.GetOpdsAuthKey(),
            Prefix = OpdsService.DefaultApiPrefix,
            BaseUrl = string.Empty,
            UserId = user.Id,
            Preferences = await unitOfWork.UserRepository.GetOpdsPreferences(user.Id),
            SeriesId = 1,
            VolumeId = 1,
            ChapterId = 1,
            PageNumber = OpdsService.FirstPageNumber
        });

        Assert.NotEmpty(feed.Entries);
        Assert.Single(feed.Entries); // Each chapter has 1 file
    }

    #endregion

    #region XML Serialization

    [Fact]
    public async Task SerializeXml_ProducesValidXml()
    {
        var (unitOfWork, context, mapper) = await CreateDatabase();
        var (opdsService, _) = SetupService(unitOfWork, mapper);
        var user = await SetupSeriesAndUser(context, unitOfWork);

        var feed = await opdsService.GetSeriesDetail(new OpdsItemsFromEntityIdRequest
        {
            ApiKey = user.GetOpdsAuthKey(),
            Prefix = OpdsService.DefaultApiPrefix,
            BaseUrl = string.Empty,
            UserId = user.Id,
            Preferences = await unitOfWork.UserRepository.GetOpdsPreferences(user.Id),
            EntityId = 1,
            PageNumber = OpdsService.FirstPageNumber
        });

        var xml = opdsService.SerializeXml(feed);

        Assert.NotEmpty(xml);
        Assert.Contains("<?xml version=\"1.0\" encoding=\"utf-8\"?>", xml);
        Assert.Contains("utf-8", xml);
        Assert.DoesNotContain("utf-16", xml);
    }

    [Fact]
    public async Task SerializeXml_HandlesNullFeed()
    {
        var (unitOfWork, _, mapper) = await CreateDatabase();
        var (opdsService, _) = SetupService(unitOfWork, mapper);

        var xml = opdsService.SerializeXml(null);

        Assert.Empty(xml);
    }

    #endregion

}
