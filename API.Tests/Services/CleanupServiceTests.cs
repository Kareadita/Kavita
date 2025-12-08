using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Abstractions.TestingHelpers;
using System.Linq;
using System.Threading.Tasks;
using API.Data;
using API.Data.Repositories;
using API.DTOs.Filtering;
using API.Entities;
using API.Entities.Enums;
using API.Entities.Progress;
using API.Extensions;
using API.Helpers;
using API.Helpers.Builders;
using API.Services;
using API.Services.Plus;
using API.Services.Reading;
using API.Services.Tasks;
using API.SignalR;
using Microsoft.Extensions.Logging;
using NSubstitute;
using Xunit;
using Xunit.Abstractions;

namespace API.Tests.Services;

public class CleanupServiceTests(ITestOutputHelper outputHelper): AbstractDbTest(outputHelper)
{

    #region Setup

    private Task<(ILogger<CleanupService>, IEventHub, IReaderService)> Setup(IUnitOfWork unitOfWork, DataContext context)
    {
        context.Library.Add(new LibraryBuilder("Manga")
            .WithFolderPath(new FolderPathBuilder(Root + "data/").Build())
            .Build());

        var logger = Substitute.For<ILogger<CleanupService>>();
        var messageHub = Substitute.For<IEventHub>();
        var readerService = new ReaderService(unitOfWork, Substitute.For<ILogger<ReaderService>>(), Substitute.For<IEventHub>(),
            Substitute.For<IImageService>(),
            new DirectoryService(Substitute.For<ILogger<DirectoryService>>(), new MockFileSystem()),
            Substitute.For<IScrobblingService>(), Substitute.For<IReadingSessionService>(), Substitute.For<IClientInfoAccessor>());

        return Task.FromResult<(ILogger<CleanupService>, IEventHub, IReaderService)>((logger, messageHub, readerService));
    }

    #endregion

    #region DeleteSeriesCoverImages

    [Fact]
    public async Task DeleteSeriesCoverImages_ShouldDeleteAll()
    {
        var filesystem = CreateFileSystem();
        filesystem.AddFile($"{CoverImageDirectory}{ImageService.GetSeriesFormat(1)}.jpg", new MockFileData(""));
        filesystem.AddFile($"{CoverImageDirectory}{ImageService.GetSeriesFormat(3)}.jpg", new MockFileData(""));
        filesystem.AddFile($"{CoverImageDirectory}{ImageService.GetSeriesFormat(1000)}.jpg", new MockFileData(""));

        // Delete all Series to reset state
        var (unitOfWork, context, _) = await CreateDatabase();
        var (logger, messageHub, readerService) = await Setup(unitOfWork, context);

        var s = new SeriesBuilder("Test 1").Build();
        s.CoverImage = $"{ImageService.GetSeriesFormat(1)}.jpg";
        s.LibraryId = 1;
        context.Series.Add(s);
        s = new SeriesBuilder("Test 2").Build();
        s.CoverImage = $"{ImageService.GetSeriesFormat(3)}.jpg";
        s.LibraryId = 1;
        context.Series.Add(s);
        s = new SeriesBuilder("Test 3").Build();
        s.CoverImage = $"{ImageService.GetSeriesFormat(1000)}.jpg";
        s.LibraryId = 1;
        context.Series.Add(s);

        var ds = new DirectoryService(Substitute.For<ILogger<DirectoryService>>(), filesystem);
        var cleanupService = new CleanupService(logger, unitOfWork, messageHub,
            ds);

        await cleanupService.DeleteSeriesCoverImages();

        Assert.Empty(ds.GetFiles(CoverImageDirectory));
    }

    [Fact]
    public async Task DeleteSeriesCoverImages_ShouldNotDeleteLinkedFiles()
    {
        var filesystem = CreateFileSystem();
        filesystem.AddFile($"{CoverImageDirectory}{ImageService.GetSeriesFormat(1)}.jpg", new MockFileData(""));
        filesystem.AddFile($"{CoverImageDirectory}{ImageService.GetSeriesFormat(3)}.jpg", new MockFileData(""));
        filesystem.AddFile($"{CoverImageDirectory}{ImageService.GetSeriesFormat(1000)}.jpg", new MockFileData(""));

        // Delete all Series to reset state
        var (unitOfWork, context, _) = await CreateDatabase();
        var (logger, messageHub, readerService) = await Setup(unitOfWork, context);

        // Add 2 series with cover images
        var s = new SeriesBuilder("Test 1").Build();
        s.CoverImage = $"{ImageService.GetSeriesFormat(1)}.jpg";
        s.LibraryId = 1;
        context.Series.Add(s);
        s = new SeriesBuilder("Test 2").Build();
        s.CoverImage = $"{ImageService.GetSeriesFormat(3)}.jpg";
        s.LibraryId = 1;
        context.Series.Add(s);


        await context.SaveChangesAsync();
        var ds = new DirectoryService(Substitute.For<ILogger<DirectoryService>>(), filesystem);
        var cleanupService = new CleanupService(logger, unitOfWork, messageHub,
            ds);

        await cleanupService.DeleteSeriesCoverImages();

        Assert.Equal(2, ds.GetFiles(CoverImageDirectory).Count());
    }
    #endregion

    #region DeleteChapterCoverImages
    [Fact]
    public async Task DeleteChapterCoverImages_ShouldNotDeleteLinkedFiles()
    {
        var filesystem = CreateFileSystem();
        filesystem.AddFile($"{CoverImageDirectory}v01_c01.jpg", new MockFileData(""));
        filesystem.AddFile($"{CoverImageDirectory}v01_c03.jpg", new MockFileData(""));
        filesystem.AddFile($"{CoverImageDirectory}v01_c1000.jpg", new MockFileData(""));

        // Delete all Series to reset state
        var (unitOfWork, context, _) = await CreateDatabase();
        var (logger, messageHub, readerService) = await Setup(unitOfWork, context);

        // Add 2 series with cover images
        context.Series.Add(new SeriesBuilder("Test 1")
            .WithVolume(new VolumeBuilder("1")
                .WithChapter(new ChapterBuilder(API.Services.Tasks.Scanner.Parser.Parser.DefaultChapter).WithCoverImage("v01_c01.jpg").Build())
                .WithCoverImage("v01_c01.jpg")
                .Build())
            .WithCoverImage("series_01.jpg")
            .WithLibraryId(1)
            .Build());

        context.Series.Add(new SeriesBuilder("Test 2")
            .WithVolume(new VolumeBuilder("1")
                .WithChapter(new ChapterBuilder(API.Services.Tasks.Scanner.Parser.Parser.DefaultChapter).WithCoverImage("v01_c03.jpg").Build())
                .WithCoverImage("v01_c03.jpg")
                .Build())
            .WithCoverImage("series_03.jpg")
            .WithLibraryId(1)
            .Build());


        await context.SaveChangesAsync();
        var ds = new DirectoryService(Substitute.For<ILogger<DirectoryService>>(), filesystem);
        var cleanupService = new CleanupService(logger, unitOfWork, messageHub,
            ds);

        await cleanupService.DeleteChapterCoverImages();

        Assert.Equal(2, ds.GetFiles(CoverImageDirectory).Count());
    }
    #endregion

    // #region DeleteTagCoverImages
    //
    // [Fact]
    // public async Task DeleteTagCoverImages_ShouldNotDeleteLinkedFiles()
    // {
    //     var filesystem = CreateFileSystem();
    //     filesystem.AddFile($"{CoverImageDirectory}{ImageService.GetCollectionTagFormat(1)}.jpg", new MockFileData(""));
    //     filesystem.AddFile($"{CoverImageDirectory}{ImageService.GetCollectionTagFormat(2)}.jpg", new MockFileData(""));
    //     filesystem.AddFile($"{CoverImageDirectory}{ImageService.GetCollectionTagFormat(1000)}.jpg", new MockFileData(""));
    //
    //     // Delete all Series to reset state
    //     var (unitOfWork, context, _) = await CreateDatabase();
    //     var (logger, messageHub, readerService) = await Setup(unitOfWork, context);
    //
    //     // Add 2 series with cover images
    //
    //     _context.Series.Add(new SeriesBuilder("Test 1")
    //         .WithMetadata(new SeriesMetadataBuilder()
    //             .WithCollectionTag(new AppUserCollectionBuilder("Something")
    //                 .WithCoverImage($"{ImageService.GetCollectionTagFormat(1)}.jpg")
    //                 .Build())
    //             .Build())
    //         .WithCoverImage($"{ImageService.GetSeriesFormat(1)}.jpg")
    //         .WithLibraryId(1)
    //         .Build());
    //
    //     _context.Series.Add(new SeriesBuilder("Test 2")
    //         .WithMetadata(new SeriesMetadataBuilder()
    //             .WithCollectionTag(new AppUserCollectionBuilder("Something")
    //                 .WithCoverImage($"{ImageService.GetCollectionTagFormat(2)}.jpg")
    //                 .Build())
    //             .Build())
    //         .WithCoverImage($"{ImageService.GetSeriesFormat(3)}.jpg")
    //         .WithLibraryId(1)
    //         .Build());
    //
    //
    //     await _context.SaveChangesAsync();
    //     var ds = new DirectoryService(Substitute.For<ILogger<DirectoryService>>(), filesystem);
    //     var cleanupService = new CleanupService(logger, _unitOfWork, messageHub,
    //         ds);
    //
    //     await cleanupService.DeleteTagCoverImages();
    //
    //     Assert.Equal(2, ds.GetFiles(CoverImageDirectory).Count());
    // }
    //
    // #endregion

    #region DeleteReadingListCoverImages
    [Fact]
    public async Task DeleteReadingListCoverImages_ShouldNotDeleteLinkedFiles()
    {
        var filesystem = CreateFileSystem();
        filesystem.AddFile($"{CoverImageDirectory}{ImageService.GetReadingListFormat(1)}.jpg", new MockFileData(""));
        filesystem.AddFile($"{CoverImageDirectory}{ImageService.GetReadingListFormat(2)}.jpg", new MockFileData(""));
        filesystem.AddFile($"{CoverImageDirectory}{ImageService.GetReadingListFormat(3)}.jpg", new MockFileData(""));

        // Delete all Series to reset state
        var (unitOfWork, context, _) = await CreateDatabase();
        var (logger, messageHub, readerService) = await Setup(unitOfWork, context);

        context.Users.Add(new AppUser()
        {
            UserName = "Joe",
            ReadingLists = new List<ReadingList>()
            {
                new ReadingListBuilder("Something")
                    .WithRating(AgeRating.Unknown)
                    .WithCoverImage($"{ImageService.GetReadingListFormat(1)}.jpg")
                    .Build(),
                new ReadingListBuilder("Something 2")
                    .WithRating(AgeRating.Unknown)
                    .WithCoverImage($"{ImageService.GetReadingListFormat(2)}.jpg")
                    .Build(),
            }
        });

        await context.SaveChangesAsync();
        var ds = new DirectoryService(Substitute.For<ILogger<DirectoryService>>(), filesystem);
        var cleanupService = new CleanupService(logger, unitOfWork, messageHub, ds);

        await cleanupService.DeleteReadingListCoverImages();

        Assert.Equal(2, ds.GetFiles(CoverImageDirectory).Count());
    }
    #endregion

    #region CleanupCacheDirectory

    [Fact]
    public async Task CleanupCacheDirectory_ClearAllFiles()
    {
        var filesystem = CreateFileSystem();
        filesystem.AddFile($"{CacheDirectory}01.jpg", new MockFileData(""));
        filesystem.AddFile($"{CacheDirectory}02.jpg", new MockFileData(""));

        var (unitOfWork, context, _) = await CreateDatabase();
        var (logger, messageHub, readerService) = await Setup(unitOfWork, context);

        var ds = new DirectoryService(Substitute.For<ILogger<DirectoryService>>(), filesystem);
        var cleanupService = new CleanupService(logger, unitOfWork, messageHub, ds);
        await cleanupService.CleanupCacheAndTempDirectories();
        Assert.Empty(ds.GetFiles(CacheDirectory, searchOption: SearchOption.AllDirectories));
    }

    [Fact]
    public async Task CleanupCacheDirectory_ClearAllFilesInSubDirectory()
    {
        var filesystem = CreateFileSystem();
        filesystem.AddFile($"{CacheDirectory}01.jpg", new MockFileData(""));
        filesystem.AddFile($"{CacheDirectory}subdir/02.jpg", new MockFileData(""));

        var (unitOfWork, context, _) = await CreateDatabase();
        var (logger, messageHub, readerService) = await Setup(unitOfWork, context);

        var ds = new DirectoryService(Substitute.For<ILogger<DirectoryService>>(), filesystem);
        var cleanupService = new CleanupService(logger, unitOfWork, messageHub, ds);
        await cleanupService.CleanupCacheAndTempDirectories();
        Assert.Empty(ds.GetFiles(CacheDirectory, searchOption: SearchOption.AllDirectories));
    }

    #endregion

    #region CleanupBackups

    [Fact]
    public async Task CleanupBackups_LeaveOneFile_SinceAllAreExpired()
    {
        var filesystem = CreateFileSystem();
        var filesystemFile = new MockFileData("")
        {
            CreationTime = DateTimeOffset.Now.Subtract(TimeSpan.FromDays(31))
        };
        filesystem.AddFile($"{BackupDirectory}kavita_backup_11_29_2021_12_00_13 AM.zip", filesystemFile);
        filesystem.AddFile($"{BackupDirectory}kavita_backup_12_3_2021_9_27_58 AM.zip", filesystemFile);
        filesystem.AddFile($"{BackupDirectory}randomfile.zip", filesystemFile);

        var (unitOfWork, context, _) = await CreateDatabase();
        var (logger, messageHub, readerService) = await Setup(unitOfWork, context);

        var ds = new DirectoryService(Substitute.For<ILogger<DirectoryService>>(), filesystem);
        var cleanupService = new CleanupService(logger, unitOfWork, messageHub, ds);
        await cleanupService.CleanupBackups();
        Assert.Single(ds.GetFiles(BackupDirectory, searchOption: SearchOption.AllDirectories));
    }

    [Fact]
    public async Task CleanupBackups_LeaveLestExpired()
    {
        var filesystem = CreateFileSystem();
        var filesystemFile = new MockFileData("")
        {
            CreationTime = DateTimeOffset.Now.Subtract(TimeSpan.FromDays(31))
        };
        filesystem.AddFile($"{BackupDirectory}kavita_backup_11_29_2021_12_00_13 AM.zip", filesystemFile);
        filesystem.AddFile($"{BackupDirectory}kavita_backup_12_3_2021_9_27_58 AM.zip", filesystemFile);
        filesystem.AddFile($"{BackupDirectory}randomfile.zip", new MockFileData("")
        {
            CreationTime = DateTimeOffset.Now.Subtract(TimeSpan.FromDays(14))
        });

        var (unitOfWork, context, _) = await CreateDatabase();
        var (logger, messageHub, readerService) = await Setup(unitOfWork, context);

        var ds = new DirectoryService(Substitute.For<ILogger<DirectoryService>>(), filesystem);
        var cleanupService = new CleanupService(logger, unitOfWork, messageHub, ds);
        await cleanupService.CleanupBackups();
        Assert.True(filesystem.File.Exists($"{BackupDirectory}randomfile.zip"));
    }

    #endregion

    #region CleanupLogs

    [Fact]
    public async Task CleanupLogs_LeaveOneFile_SinceAllAreExpired()
    {
        var filesystem = CreateFileSystem();
        foreach (var i in Enumerable.Range(1, 10))
        {
            var day = API.Services.Tasks.Scanner.Parser.Parser.PadZeros($"{i}");
            filesystem.AddFile($"{LogDirectory}kavita202009{day}.log", new MockFileData("")
            {
                CreationTime = DateTimeOffset.Now.Subtract(TimeSpan.FromDays(31))
            });
        }

        var (unitOfWork, context, _) = await CreateDatabase();
        var (logger, messageHub, readerService) = await Setup(unitOfWork, context);

        var ds = new DirectoryService(Substitute.For<ILogger<DirectoryService>>(), filesystem);
        var cleanupService = new CleanupService(logger, unitOfWork, messageHub, ds);
        await cleanupService.CleanupLogs();
        Assert.Single(ds.GetFiles(LogDirectory, searchOption: SearchOption.AllDirectories));
    }

    [Fact]
    public async Task CleanupLogs_LeaveLestExpired()
    {
        var filesystem = CreateFileSystem();
        foreach (var i in Enumerable.Range(1, 9))
        {
            var day = API.Services.Tasks.Scanner.Parser.Parser.PadZeros($"{i}");
            filesystem.AddFile($"{LogDirectory}kavita202009{day}.log", new MockFileData("")
            {
                CreationTime = DateTimeOffset.Now.Subtract(TimeSpan.FromDays(31 - i))
            });
        }
        filesystem.AddFile($"{LogDirectory}kavita20200910.log", new MockFileData("")
        {
            CreationTime = DateTimeOffset.Now.Subtract(TimeSpan.FromDays(31 - 10))
        });
        filesystem.AddFile($"{LogDirectory}kavita20200911.log", new MockFileData("")
        {
            CreationTime = DateTimeOffset.Now.Subtract(TimeSpan.FromDays(31 - 11))
        });

        var (unitOfWork, context, _) = await CreateDatabase();
        var (logger, messageHub, readerService) = await Setup(unitOfWork, context);


        var ds = new DirectoryService(Substitute.For<ILogger<DirectoryService>>(), filesystem);
        var cleanupService = new CleanupService(logger, unitOfWork, messageHub, ds);
        await cleanupService.CleanupLogs();
        Assert.True(filesystem.File.Exists($"{LogDirectory}kavita20200911.log"));
    }

    #endregion

    #region CleanupDbEntries

    [Fact]
    public async Task CleanupDbEntries_CleanupAbandonedChapters()
    {
        var (unitOfWork, context, _) = await CreateDatabase();
        var (logger, messageHub, readerService) = await Setup(unitOfWork, context);

        var c = new ChapterBuilder(API.Services.Tasks.Scanner.Parser.Parser.DefaultChapter)
            .WithPages(1)
            .Build();
        var series = new SeriesBuilder("Test")
            .WithFormat(MangaFormat.Epub)
            .WithVolume(new VolumeBuilder(API.Services.Tasks.Scanner.Parser.Parser.LooseLeafVolume)
                .WithChapter(c)
                .Build())
            .Build();
        series.Library = new LibraryBuilder("Test LIb").Build();

        context.Series.Add(series);


        context.AppUser.Add(new AppUser()
        {
            UserName = "majora2007"
        });

        await context.SaveChangesAsync();

        var user = await unitOfWork.UserRepository.GetUserByUsernameAsync("majora2007", AppUserIncludes.Progress);
        await readerService.MarkChaptersUntilAsRead(user, 1, 5);
        await context.SaveChangesAsync();

        // Validate correct chapters have read status
        Assert.Equal(1, (await unitOfWork.AppUserProgressRepository.GetUserProgressAsync(1, 1)).PagesRead);

        var cleanupService = new CleanupService(Substitute.For<ILogger<CleanupService>>(), unitOfWork,
            Substitute.For<IEventHub>(),
            new DirectoryService(Substitute.For<ILogger<DirectoryService>>(), new MockFileSystem()));

        // Delete the Chapter
        context.Chapter.Remove(c);
        await unitOfWork.CommitAsync();
        Assert.Empty(await unitOfWork.AppUserProgressRepository.GetUserProgressForSeriesAsync(1, 1));

        // NOTE: This may not be needed, the underlying DB structure seems fixed as of v0.7
        await cleanupService.CleanupDbEntries();

        Assert.Empty(await unitOfWork.AppUserProgressRepository.GetUserProgressForSeriesAsync(1, 1));
    }

    [Fact]
    public async Task CleanupDbEntries_RemoveTagsWithoutSeries()
    {
        var (unitOfWork, context, _) = await CreateDatabase();
        var (logger, messageHub, readerService) = await Setup(unitOfWork, context);

        var s = new SeriesBuilder("Test")
            .WithFormat(MangaFormat.Epub)
            .WithMetadata(new SeriesMetadataBuilder().Build())
            .Build();
        s.Library = new LibraryBuilder("Test LIb").Build();
        context.Series.Add(s);

        var c = new AppUserCollection()
        {
            Title = "Test Tag",
            NormalizedTitle = "Test Tag".ToNormalized(),
            AgeRating = AgeRating.Unknown,
            Items = new List<Series>() {s}
        };

        context.AppUser.Add(new AppUser()
        {
            UserName = "majora2007",
            Collections = new List<AppUserCollection>() {c}
        });
        await context.SaveChangesAsync();

        var cleanupService = new CleanupService(Substitute.For<ILogger<CleanupService>>(), unitOfWork,
            Substitute.For<IEventHub>(),
            new DirectoryService(Substitute.For<ILogger<DirectoryService>>(), new MockFileSystem()));

        // Delete the Chapter
        context.Series.Remove(s);
        await unitOfWork.CommitAsync();

        await cleanupService.CleanupDbEntries();

        Assert.Empty(await unitOfWork.CollectionTagRepository.GetAllCollectionsAsync());
    }

    #endregion

    #region CleanupWantToRead

    [Fact]
    public async Task CleanupWantToRead_ShouldRemoveFullyReadSeries()
    {
        var (unitOfWork, context, _) = await CreateDatabase();
        var (logger, messageHub, readerService) = await Setup(unitOfWork, context);

        var s = new SeriesBuilder("Test CleanupWantToRead_ShouldRemoveFullyReadSeries")
            .WithMetadata(new SeriesMetadataBuilder().WithPublicationStatus(PublicationStatus.Completed).Build())
            .Build();

        s.Library = new LibraryBuilder("Test LIb").Build();
        context.Series.Add(s);

        var user = new AppUser()
        {
            UserName = "CleanupWantToRead_ShouldRemoveFullyReadSeries",
        };
        context.AppUser.Add(user);

        await unitOfWork.CommitAsync();

        // Add want to read
        user.WantToRead = new List<AppUserWantToRead>()
        {
            new AppUserWantToRead()
            {
                SeriesId = s.Id
            }
        };
        await unitOfWork.CommitAsync();

        await readerService.MarkSeriesAsRead(user, s.Id);
        await unitOfWork.CommitAsync();

        var cleanupService = new CleanupService(Substitute.For<ILogger<CleanupService>>(), unitOfWork,
            Substitute.For<IEventHub>(),
            new DirectoryService(Substitute.For<ILogger<DirectoryService>>(), new MockFileSystem()));


        await cleanupService.CleanupWantToRead();

        var wantToRead =
            await unitOfWork.SeriesRepository.GetWantToReadForUserAsync(user.Id, new UserParams(), new FilterDto());

        Assert.Equal(0, wantToRead.TotalCount);
    }
    #endregion

    #region ConsolidateProgress

    [Fact]
    public async Task ConsolidateProgress_ShouldRemoveDuplicates()
    {
        var (unitOfWork, context, _) = await CreateDatabase();
        var (logger, messageHub, readerService) = await Setup(unitOfWork, context);

        var s = new SeriesBuilder("Test ConsolidateProgress_ShouldRemoveDuplicates")
            .WithVolume(new VolumeBuilder("1")
                .WithChapter(new ChapterBuilder("1")
                    .WithPages(3)
                    .Build())
                .Build())
            .Build();

        s.Library = new LibraryBuilder("Test Lib").Build();
        context.Series.Add(s);

        var user = new AppUser()
        {
            UserName = "ConsolidateProgress_ShouldRemoveDuplicates",
        };
        context.AppUser.Add(user);

        await unitOfWork.CommitAsync();

        // Add 2 progress events
        user.Progresses ??= [];
        user.Progresses.Add(new AppUserProgress()
        {
            ChapterId = 1,
            VolumeId = 1,
            SeriesId = 1,
            LibraryId = s.LibraryId,
            PagesRead = 1,
        });
        await unitOfWork.CommitAsync();

        // Add a duplicate with higher page number
        user.Progresses.Add(new AppUserProgress()
        {
            ChapterId = 1,
            VolumeId = 1,
            SeriesId = 1,
            LibraryId = s.LibraryId,
            PagesRead = 3,
        });
        await unitOfWork.CommitAsync();

        Assert.Equal(2, (await unitOfWork.AppUserProgressRepository.GetAllProgress()).Count());

        var cleanupService = new CleanupService(Substitute.For<ILogger<CleanupService>>(), unitOfWork,
            Substitute.For<IEventHub>(),
            new DirectoryService(Substitute.For<ILogger<DirectoryService>>(), new MockFileSystem()));


        await cleanupService.ConsolidateProgress();

        var progress = await unitOfWork.AppUserProgressRepository.GetAllProgress();

        Assert.Single(progress);
        Assert.True(progress.First().PagesRead == 3);
    }
    #endregion


    #region EnsureChapterProgressIsCapped

    [Fact]
    public async Task EnsureChapterProgressIsCapped_ShouldNormalizeProgress()
    {
        var (unitOfWork, context, _) = await CreateDatabase();
        var (logger, messageHub, readerService) = await Setup(unitOfWork, context);

        var s = new SeriesBuilder("Test CleanupWantToRead_ShouldRemoveFullyReadSeries")
            .WithMetadata(new SeriesMetadataBuilder().WithPublicationStatus(PublicationStatus.Completed).Build())
            .Build();

        s.Library = new LibraryBuilder("Test LIb").Build();
        var c = new ChapterBuilder("1").WithPages(2).Build();
        c.UserProgress = new List<AppUserProgress>();
        s.Volumes = new List<Volume>()
        {
            new VolumeBuilder(API.Services.Tasks.Scanner.Parser.Parser.LooseLeafVolume).WithChapter(c).Build()
        };
        context.Series.Add(s);

        var user = new AppUser()
        {
            UserName = "EnsureChapterProgressIsCapped",
            Progresses = new List<AppUserProgress>()
        };
        context.AppUser.Add(user);

        await unitOfWork.CommitAsync();

        await readerService.MarkChaptersAsRead(user, s.Id, new List<Chapter>() {c});
        await unitOfWork.CommitAsync();

        var chapter = await unitOfWork.ChapterRepository.GetChapterDtoAsync(c.Id, 1);
        await unitOfWork.ChapterRepository.AddChapterModifiers(user.Id, chapter);

        Assert.NotNull(chapter);
        Assert.Equal(2, chapter.PagesRead);

        // Update chapter to have 1 page
        c.Pages = 1;
        unitOfWork.ChapterRepository.Update(c);
        await unitOfWork.CommitAsync();

        chapter = await unitOfWork.ChapterRepository.GetChapterDtoAsync(c.Id, 1);
        await unitOfWork.ChapterRepository.AddChapterModifiers(user.Id, chapter);
        Assert.NotNull(chapter);
        Assert.Equal(2, chapter.PagesRead);
        Assert.Equal(1, chapter.Pages);

        var cleanupService = new CleanupService(Substitute.For<ILogger<CleanupService>>(), unitOfWork,
            Substitute.For<IEventHub>(),
            new DirectoryService(Substitute.For<ILogger<DirectoryService>>(), new MockFileSystem()));

        await cleanupService.EnsureChapterProgressIsCapped();
        chapter = await unitOfWork.ChapterRepository.GetChapterDtoAsync(c.Id, 1);
        await unitOfWork.ChapterRepository.AddChapterModifiers(user.Id, chapter);

        Assert.NotNull(chapter);
        Assert.Equal(1, chapter.PagesRead);

        context.AppUser.Remove(user);
        await unitOfWork.CommitAsync();
    }
    #endregion

    #region CleanupBookmarks
    //
    // [Fact]
    // public async Task CleanupBookmarks_LeaveAllFiles()
    // {
    //     var filesystem = CreateFileSystem();
    //     filesystem.AddFile($"{BookmarkDirectory}1/1/1/0001.jpg", new MockFileData(""));
    //     filesystem.AddFile($"{BookmarkDirectory}1/1/1/0002.jpg", new MockFileData(""));
    //
    //     // Delete all Series to reset state
    //     var (unitOfWork, context, _) = await CreateDatabase();
    //     var (logger, messageHub, readerService) = await Setup(unitOfWork, context);
    //
    //     _context.Series.Add(new Series()
    //     {
    //         Name = "Test",
    //         Library = new Library() {
    //             Name = "Test LIb",
    //             Type = LibraryType.Manga,
    //         },
    //         Volumes = new List<Volume>()
    //         {
    //             new Volume()
    //             {
    //                 Chapters = new List<Chapter>()
    //                 {
    //                     new Chapter()
    //                     {
    //
    //                     }
    //                 }
    //             }
    //         }
    //     });
    //
    //     await _context.SaveChangesAsync();
    //
    //     _context.AppUser.Add(new AppUser()
    //     {
    //         Bookmarks = new List<AppUserBookmark>()
    //         {
    //             new AppUserBookmark()
    //             {
    //                 AppUserId = 1,
    //                 ChapterId = 1,
    //                 Page = 1,
    //                 FileName = "1/1/1/0001.jpg",
    //                 SeriesId = 1,
    //                 VolumeId = 1
    //             },
    //             new AppUserBookmark()
    //             {
    //                 AppUserId = 1,
    //                 ChapterId = 1,
    //                 Page = 2,
    //                 FileName = "1/1/1/0002.jpg",
    //                 SeriesId = 1,
    //                 VolumeId = 1
    //             }
    //         }
    //     });
    //
    //     await _context.SaveChangesAsync();
    //
    //
    //     var ds = new DirectoryService(Substitute.For<ILogger<DirectoryService>>(), filesystem);
    //     var cleanupService = new CleanupService(logger, _unitOfWork, messageHub,
    //         ds);
    //
    //     await cleanupService.CleanupBookmarks();
    //
    //     Assert.Equal(2, ds.GetFiles(BookmarkDirectory, searchOption:SearchOption.AllDirectories).Count());
    //
    // }
    //
    // [Fact]
    // public async Task CleanupBookmarks_LeavesOneFiles()
    // {
    //     var filesystem = CreateFileSystem();
    //     filesystem.AddFile($"{BookmarkDirectory}1/1/1/0001.jpg", new MockFileData(""));
    //     filesystem.AddFile($"{BookmarkDirectory}1/1/2/0002.jpg", new MockFileData(""));
    //
    //     // Delete all Series to reset state
    //     var (unitOfWork, context, _) = await CreateDatabase();
    //     var (logger, messageHub, readerService) = await Setup(unitOfWork, context);
    //
    //     _context.Series.Add(new Series()
    //     {
    //         Name = "Test",
    //         Library = new Library() {
    //             Name = "Test LIb",
    //             Type = LibraryType.Manga,
    //         },
    //         Volumes = new List<Volume>()
    //         {
    //             new Volume()
    //             {
    //                 Chapters = new List<Chapter>()
    //                 {
    //                     new Chapter()
    //                     {
    //
    //                     }
    //                 }
    //             }
    //         }
    //     });
    //
    //     await _context.SaveChangesAsync();
    //
    //     _context.AppUser.Add(new AppUser()
    //     {
    //         Bookmarks = new List<AppUserBookmark>()
    //         {
    //             new AppUserBookmark()
    //             {
    //                 AppUserId = 1,
    //                 ChapterId = 1,
    //                 Page = 1,
    //                 FileName = "1/1/1/0001.jpg",
    //                 SeriesId = 1,
    //                 VolumeId = 1
    //             }
    //         }
    //     });
    //
    //     await _context.SaveChangesAsync();
    //
    //
    //     var ds = new DirectoryService(Substitute.For<ILogger<DirectoryService>>(), filesystem);
    //     var cleanupService = new CleanupService(logger, _unitOfWork, messageHub,
    //         ds);
    //
    //     await cleanupService.CleanupBookmarks();
    //
    //     Assert.Equal(1, ds.GetFiles(BookmarkDirectory, searchOption:SearchOption.AllDirectories).Count());
    //     Assert.Equal(1, ds.FileSystem.Directory.GetDirectories($"{BookmarkDirectory}1/1/").Length);
    // }
    //
    #endregion
}
