using System.Collections.Generic;
using System.Data.Common;
using System.Globalization;
using System.IO.Abstractions.TestingHelpers;
using System.Linq;
using System.Threading.Tasks;
using API.Data;
using API.Data.Repositories;
using API.DTOs;
using API.DTOs.Progress;
using API.DTOs.Reader;
using API.Entities;
using API.Entities.Enums;
using API.Entities.Metadata;
using API.Extensions;
using API.Helpers;
using API.Helpers.Builders;
using API.Services;
using API.Services.Plus;
using API.Services.Tasks;
using API.SignalR;
using API.Tests.Helpers;
using AutoMapper;
using Hangfire;
using Hangfire.InMemory;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using NSubstitute;
using Xunit;
using Xunit.Abstractions;

namespace API.Tests.Services;

public class ReaderServiceTests
{
    private readonly ITestOutputHelper _testOutputHelper;
    private readonly IUnitOfWork _unitOfWork;
    private readonly DataContext _context;
    private readonly ReaderService _readerService;

    private const string CacheDirectory = "C:/kavita/config/cache/";
    private const string CoverImageDirectory = "C:/kavita/config/covers/";
    private const string BackupDirectory = "C:/kavita/config/backups/";
    private const string DataDirectory = "C:/data/";

    public ReaderServiceTests(ITestOutputHelper testOutputHelper)
    {
        _testOutputHelper = testOutputHelper;
        var contextOptions = new DbContextOptionsBuilder().UseSqlite(CreateInMemoryDatabase()).Options;

        _context = new DataContext(contextOptions);
        Task.Run(SeedDb).GetAwaiter().GetResult();

        var config = new MapperConfiguration(cfg => cfg.AddProfile<AutoMapperProfiles>());
        var mapper = config.CreateMapper();
        _unitOfWork = new UnitOfWork(_context, mapper, null);
        _readerService = new ReaderService(_unitOfWork, Substitute.For<ILogger<ReaderService>>(),
            Substitute.For<IEventHub>(), Substitute.For<IImageService>(),
            new DirectoryService(Substitute.For<ILogger<DirectoryService>>(), new MockFileSystem()),
            Substitute.For<IScrobblingService>());
    }

    #region Setup

    private static DbConnection CreateInMemoryDatabase()
    {
        var connection = new SqliteConnection("Filename=:memory:");

        connection.Open();

        return connection;
    }

    private async Task<bool> SeedDb()
    {
        await _context.Database.MigrateAsync();
        var filesystem = CreateFileSystem();

        await Seed.SeedSettings(_context,
            new DirectoryService(Substitute.For<ILogger<DirectoryService>>(), filesystem));

        var setting = await _context.ServerSetting.Where(s => s.Key == ServerSettingKey.CacheDirectory).SingleAsync();
        setting.Value = CacheDirectory;

        setting = await _context.ServerSetting.Where(s => s.Key == ServerSettingKey.BackupDirectory).SingleAsync();
        setting.Value = BackupDirectory;

        _context.ServerSetting.Update(setting);

        _context.Library.Add(new LibraryBuilder("Manga")
            .WithFolderPath(new FolderPathBuilder("C:/data/").Build())
            .Build());
        return await _context.SaveChangesAsync() > 0;
    }

    private async Task ResetDb()
    {
        _context.Series.RemoveRange(_context.Series.ToList());

        await _context.SaveChangesAsync();
    }

    private static MockFileSystem CreateFileSystem()
    {
        var fileSystem = new MockFileSystem();
        fileSystem.Directory.SetCurrentDirectory("C:/kavita/");
        fileSystem.AddDirectory("C:/kavita/config/");
        fileSystem.AddDirectory(CacheDirectory);
        fileSystem.AddDirectory(CoverImageDirectory);
        fileSystem.AddDirectory(BackupDirectory);
        fileSystem.AddDirectory(DataDirectory);

        return fileSystem;
    }

    #endregion

    #region FormatBookmarkFolderPath

    [Theory]
    [InlineData("/manga/", 1, 1, 1, "/manga/1/1/1")]
    [InlineData("C:/manga/", 1, 1, 10001, "C:/manga/1/1/10001")]
    public void FormatBookmarkFolderPathTest(string baseDir, int userId, int seriesId, int chapterId, string expected)
    {
        Assert.Equal(expected, ReaderService.FormatBookmarkFolderPath(baseDir, userId, seriesId, chapterId));
    }

    #endregion

    #region CapPageToChapter

    [Fact]
    public async Task CapPageToChapterTest()
    {
        await ResetDb();

        var series = new SeriesBuilder("Test")
            .WithVolume(new VolumeBuilder(API.Services.Tasks.Scanner.Parser.Parser.LooseLeafVolume)
                .WithChapter(new ChapterBuilder(API.Services.Tasks.Scanner.Parser.Parser.DefaultChapter)
                    .WithPages(1)
                    .Build())
                .Build())
            .Build();
        series.Library = new LibraryBuilder("Test LIb", LibraryType.Manga).Build();


        _context.Series.Add(series);


        await _context.SaveChangesAsync();


        Assert.Equal(0, (await _readerService.CapPageToChapter(1, -1)).Item1);
        Assert.Equal(1, (await _readerService.CapPageToChapter(1, 10)).Item1);
    }

    #endregion

    #region SaveReadingProgress

    [Fact]
    public async Task SaveReadingProgress_ShouldCreateNewEntity()
    {
        await ResetDb();

        var series = new SeriesBuilder("Test")
            .WithVolume(new VolumeBuilder(API.Services.Tasks.Scanner.Parser.Parser.LooseLeafVolume)
                .WithChapter(new ChapterBuilder(API.Services.Tasks.Scanner.Parser.Parser.DefaultChapter)
                    .WithPages(1)
                    .Build())
                .Build())
            .Build();
        series.Library = new LibraryBuilder("Test LIb", LibraryType.Manga).Build();

        _context.Series.Add(series);

        _context.AppUser.Add(new AppUser()
        {
            UserName = "majora2007"
        });

        await _context.SaveChangesAsync();


        JobStorage.Current = new InMemoryStorage();
        var successful = await _readerService.SaveReadingProgress(new ProgressDto()
        {
            ChapterId = 1,
            PageNum = 1,
            SeriesId = 1,
            VolumeId = 1,
            BookScrollId = null
        }, 1);

        Assert.True(successful);
        Assert.NotNull(await _unitOfWork.AppUserProgressRepository.GetUserProgressAsync(1, 1));
    }

    [Fact]
    public async Task SaveReadingProgress_ShouldUpdateExisting()
    {
        await ResetDb();

        var series = new SeriesBuilder("Test")
            .WithVolume(new VolumeBuilder(API.Services.Tasks.Scanner.Parser.Parser.LooseLeafVolume)
                .WithChapter(new ChapterBuilder(API.Services.Tasks.Scanner.Parser.Parser.DefaultChapter)
                    .WithPages(1)
                    .Build())
                .Build())
            .Build();
        series.Library = new LibraryBuilder("Test LIb", LibraryType.Manga).Build();

        _context.Series.Add(series);

        _context.AppUser.Add(new AppUser()
        {
            UserName = "majora2007"
        });

        await _context.SaveChangesAsync();

        JobStorage.Current = new InMemoryStorage();
        var successful = await _readerService.SaveReadingProgress(new ProgressDto()
        {
            ChapterId = 1,
            PageNum = 1,
            SeriesId = 1,
            VolumeId = 1,
            BookScrollId = null
        }, 1);

        Assert.True(successful);
        Assert.NotNull(await _unitOfWork.AppUserProgressRepository.GetUserProgressAsync(1, 1));

        Assert.True(await _readerService.SaveReadingProgress(new ProgressDto()
        {
            ChapterId = 1,
            PageNum = 1,
            SeriesId = 1,
            VolumeId = 1,
            BookScrollId = "/h1/"
        }, 1));

        Assert.Equal("/h1/", (await _unitOfWork.AppUserProgressRepository.GetUserProgressAsync(1, 1)).BookScrollId);

    }


    #endregion

    #region MarkChaptersAsRead

    [Fact]
    public async Task MarkChaptersAsReadTest()
    {
        await ResetDb();

        var series = new SeriesBuilder("Test")
            .WithVolume(new VolumeBuilder(API.Services.Tasks.Scanner.Parser.Parser.LooseLeafVolume)
                .WithChapter(new ChapterBuilder(API.Services.Tasks.Scanner.Parser.Parser.DefaultChapter)
                    .WithPages(1)
                    .Build())
                .WithChapter(new ChapterBuilder(API.Services.Tasks.Scanner.Parser.Parser.DefaultChapter)
                    .WithPages(2)
                    .Build())
                .Build())
            .Build();
        series.Library = new LibraryBuilder("Test LIb", LibraryType.Manga).Build();

        _context.Series.Add(series);

        _context.AppUser.Add(new AppUser()
        {
            UserName = "majora2007"
        });

        await _context.SaveChangesAsync();



        var volumes = await _unitOfWork.VolumeRepository.GetVolumes(1);
        await _readerService.MarkChaptersAsRead(await _unitOfWork.UserRepository.GetUserByIdAsync(1, AppUserIncludes.Progress), 1, volumes.First().Chapters);
        await _context.SaveChangesAsync();

        Assert.Equal(2, (await _unitOfWork.UserRepository.GetUserByIdAsync(1, AppUserIncludes.Progress)).Progresses.Count);
    }
    #endregion

    #region MarkChapterAsUnread

    [Fact]
    public async Task MarkChapterAsUnreadTest()
    {
        await ResetDb();

        var series = new SeriesBuilder("Test")
            .WithVolume(new VolumeBuilder(API.Services.Tasks.Scanner.Parser.Parser.LooseLeafVolume)
                .WithChapter(new ChapterBuilder(API.Services.Tasks.Scanner.Parser.Parser.DefaultChapter)
                    .WithPages(1)
                    .Build())
                .WithChapter(new ChapterBuilder(API.Services.Tasks.Scanner.Parser.Parser.DefaultChapter)
                    .WithPages(2)
                    .Build())
                .Build())
            .Build();
        series.Library = new LibraryBuilder("Test LIb", LibraryType.Manga).Build();

        _context.Series.Add(series);

        _context.AppUser.Add(new AppUser()
        {
            UserName = "majora2007"
        });

        await _context.SaveChangesAsync();



        var volumes = (await _unitOfWork.VolumeRepository.GetVolumes(1)).ToList();
        await _readerService.MarkChaptersAsRead(await _unitOfWork.UserRepository.GetUserByIdAsync(1, AppUserIncludes.Progress), 1, volumes.First().Chapters);

        await _context.SaveChangesAsync();
        Assert.Equal(2, (await _unitOfWork.UserRepository.GetUserByIdAsync(1, AppUserIncludes.Progress)).Progresses.Count);

        await _readerService.MarkChaptersAsUnread(await _unitOfWork.UserRepository.GetUserByIdAsync(1, AppUserIncludes.Progress), 1, volumes.First().Chapters);
        await _context.SaveChangesAsync();

        var progresses = (await _unitOfWork.UserRepository.GetUserByIdAsync(1, AppUserIncludes.Progress)).Progresses;
        Assert.Equal(0, progresses.Max(p => p.PagesRead));
        Assert.Equal(2, progresses.Count);
    }

    #endregion

    #region GetNextChapterIdAsync

    [Fact]
    public async Task GetNextChapterIdAsync_ShouldGetNextVolume()
    {
        // V1 -> V2
        await ResetDb();

        var series = new SeriesBuilder("Test")
            .WithVolume(new VolumeBuilder("1")
                .WithChapter(new ChapterBuilder("1").Build())
                .WithChapter(new ChapterBuilder("2").Build())
                .Build())

            .WithVolume(new VolumeBuilder("2")
                .WithChapter(new ChapterBuilder("21").Build())
                .WithChapter(new ChapterBuilder("22").Build())
                .Build())

            .WithVolume(new VolumeBuilder("3")
                .WithChapter(new ChapterBuilder("31").Build())
                .WithChapter(new ChapterBuilder("32").Build())
                .Build())
            .Build();
        series.Library = new LibraryBuilder("Test LIb", LibraryType.Manga).Build();

        _context.Series.Add(series);

        _context.AppUser.Add(new AppUser()
        {
            UserName = "majora2007"
        });

        await _context.SaveChangesAsync();



        var nextChapter = await _readerService.GetNextChapterIdAsync(1, 1, 1, 1);
        var actualChapter = await _unitOfWork.ChapterRepository.GetChapterAsync(nextChapter);
        Assert.NotNull(actualChapter);
        Assert.Equal("2", actualChapter.Range);
    }

    [Fact]
    public async Task GetNextChapterIdAsync_ShouldGetNextVolume_WhenUsingRanges()
    {
        // V1 -> V2
        await ResetDb();

        var series = new SeriesBuilder("Test")
            .WithVolume(new VolumeBuilder("1-2")
                .WithChapter(new ChapterBuilder(API.Services.Tasks.Scanner.Parser.Parser.DefaultChapter).Build())
                .Build())

            .WithVolume(new VolumeBuilder("3-4")
                .WithChapter(new ChapterBuilder("1").Build())
                .Build())
            .Build();
        series.Library = new LibraryBuilder("Test Lib", LibraryType.Manga).Build();

        _context.Series.Add(series);

        _context.AppUser.Add(new AppUser()
        {
            UserName = "majora2007"
        });

        await _context.SaveChangesAsync();

        var nextChapter = await _readerService.GetNextChapterIdAsync(1, 1, 1, 1);
        var actualChapter = await _unitOfWork.ChapterRepository.GetChapterAsync(nextChapter);
        Assert.NotNull(actualChapter);
        Assert.Equal("3-4", actualChapter.Volume.Name);
        Assert.Equal("1", actualChapter.Range);
    }

    [Fact]
    public async Task GetNextChapterIdAsync_ShouldGetNextVolume_OnlyFloats()
    {
        // V1 -> V2
        await ResetDb();

        var series = new SeriesBuilder("Test")
            .WithVolume(new VolumeBuilder("1.0")
                .WithChapter(new ChapterBuilder("1").Build())
                .Build())

            .WithVolume(new VolumeBuilder("2.1")
                .WithChapter(new ChapterBuilder("21").Build())
                .Build())

            .WithVolume(new VolumeBuilder("2.2")
                .WithChapter(new ChapterBuilder("31").Build())
                .Build())

            .WithVolume(new VolumeBuilder("3.1")
                .WithChapter(new ChapterBuilder("31").Build())
                .Build())


            .Build();
        series.Library = new LibraryBuilder("Test LIb", LibraryType.Manga).Build();

        _context.Series.Add(series);

        _context.AppUser.Add(new AppUser()
        {
            UserName = "majora2007"
        });

        await _context.SaveChangesAsync();



        var nextChapter = await _readerService.GetNextChapterIdAsync(1, 2, 2, 1);
        var actualChapter = await _unitOfWork.ChapterRepository.GetChapterAsync(nextChapter);
        Assert.NotNull(actualChapter);
        Assert.Equal("31", actualChapter.Range);
    }

    [Fact]
    public async Task GetNextChapterIdAsync_ShouldRollIntoNextVolume()
    {
        await ResetDb();

        var series = new SeriesBuilder("Test")
            .WithVolume(new VolumeBuilder("1")
                .WithChapter(new ChapterBuilder("1").Build())
                .WithChapter(new ChapterBuilder("2").Build())
                .Build())

            .WithVolume(new VolumeBuilder("2")
                .WithChapter(new ChapterBuilder("21").Build())
                .WithChapter(new ChapterBuilder("22").Build())
                .Build())

            .WithVolume(new VolumeBuilder("3")
                .WithChapter(new ChapterBuilder("31").Build())
                .WithChapter(new ChapterBuilder("32").Build())
                .Build())
            .Build();
        series.Library = new LibraryBuilder("Test LIb", LibraryType.Manga).Build();

        _context.Series.Add(series);


        _context.AppUser.Add(new AppUser()
        {
            UserName = "majora2007"
        });

        await _context.SaveChangesAsync();

        var nextChapter = await _readerService.GetNextChapterIdAsync(1, 1, 2, 1);
        var actualChapter = await _unitOfWork.ChapterRepository.GetChapterAsync(nextChapter);
        Assert.NotNull(actualChapter);
        Assert.Equal("21", actualChapter.Range);
    }

    [Fact]
    public async Task GetNextChapterIdAsync_ShouldRollIntoNextVolumeWithFloat()
    {
        await ResetDb();

        var series = new SeriesBuilder("Test")
            .WithVolume(new VolumeBuilder("1")
                .WithChapter(new ChapterBuilder("1").Build())
                .WithChapter(new ChapterBuilder("2").Build())
                .Build())

            .WithVolume(new VolumeBuilder("1.5")
                .WithChapter(new ChapterBuilder("21").Build())
                .WithChapter(new ChapterBuilder("22").Build())
                .Build())

            .WithVolume(new VolumeBuilder("3")
                .WithChapter(new ChapterBuilder("31").Build())
                .WithChapter(new ChapterBuilder("32").Build())
                .Build())
            .Build();
        series.Library = new LibraryBuilder("Test LIb", LibraryType.Manga).Build();

        _context.Series.Add(series);


        _context.AppUser.Add(new AppUser()
        {
            UserName = "majora2007"
        });

        await _context.SaveChangesAsync();


        var nextChapter = await _readerService.GetNextChapterIdAsync(1, 1, 2, 1);
        var actualChapter = await _unitOfWork.ChapterRepository.GetChapterAsync(nextChapter);
        Assert.NotNull(actualChapter);
        Assert.Equal("21", actualChapter.Range);
    }

    [Fact]
    public async Task GetNextChapterIdAsync_ShouldRollIntoChaptersFromVolume()
    {
        await ResetDb();

        var series = new SeriesBuilder("Test")
            .WithVolume(new VolumeBuilder(API.Services.Tasks.Scanner.Parser.Parser.LooseLeafVolume)
                .WithChapter(new ChapterBuilder("21").Build())
                .WithChapter(new ChapterBuilder("22").Build())
                .Build())

            .WithVolume(new VolumeBuilder("1")
                .WithChapter(new ChapterBuilder("1").Build())
                .WithChapter(new ChapterBuilder("2").Build())
                .Build())
            .Build();
        series.Library = new LibraryBuilder("Test LIb", LibraryType.Manga).Build();

        _context.Series.Add(series);

        _context.AppUser.Add(new AppUser()
        {
            UserName = "majora2007"
        });

        await _context.SaveChangesAsync();

        var nextChapter = await _readerService.GetNextChapterIdAsync(1, 2, 4, 1);
        Assert.NotEqual(-1, nextChapter);
        var actualChapter = await _unitOfWork.ChapterRepository.GetChapterAsync(nextChapter);
        Assert.NotNull(actualChapter);
        Assert.Equal("21", actualChapter.Range);
    }

    [Fact]
    public async Task GetNextChapterIdAsync_ShouldRollIntoNextChapter_WhenVolumesAreOnlyOneChapter_AndNextChapterIs0()
    {
        await ResetDb();

        var series = new SeriesBuilder("Test")
            .WithVolume(new VolumeBuilder(API.Services.Tasks.Scanner.Parser.Parser.LooseLeafVolume)
                .WithChapter(new ChapterBuilder("66").Build())
                .WithChapter(new ChapterBuilder("67").Build())
                .Build())

            .WithVolume(new VolumeBuilder("1")
                .WithChapter(new ChapterBuilder("1").Build())
                .Build())

            .WithVolume(new VolumeBuilder("2")
                .WithChapter(new ChapterBuilder(API.Services.Tasks.Scanner.Parser.Parser.DefaultChapter).Build())
                .Build())
            .Build();
        series.Library = new LibraryBuilder("Test LIb", LibraryType.Manga).Build();

        _context.Series.Add(series);


        _context.AppUser.Add(new AppUser()
        {
            UserName = "majora2007"
        });

        await _context.SaveChangesAsync();



        var nextChapter = await _readerService.GetNextChapterIdAsync(1, 2, 3, 1);
        Assert.NotEqual(-1, nextChapter);
        var actualChapter = await _unitOfWork.ChapterRepository.GetChapterAsync(nextChapter);
        Assert.NotNull(actualChapter);
        Assert.Equal(API.Services.Tasks.Scanner.Parser.Parser.DefaultChapter, actualChapter.Range);
    }

    [Fact]
    public async Task GetNextChapterIdAsync_ShouldFindNoNextChapterFromSpecial()
    {
        await ResetDb();

        var series = new SeriesBuilder("Test")
            .WithVolume(new VolumeBuilder("1")
                .WithChapter(new ChapterBuilder("1").Build())
                .WithChapter(new ChapterBuilder("2").Build())
                .Build())

            .WithVolume(new VolumeBuilder(API.Services.Tasks.Scanner.Parser.Parser.SpecialVolume)
                .WithChapter(new ChapterBuilder("A.cbz").WithIsSpecial(true).WithSortOrder(API.Services.Tasks.Scanner.Parser.Parser.SpecialVolumeNumber).Build())
                .WithChapter(new ChapterBuilder("B.cbz").WithIsSpecial(true).WithSortOrder(API.Services.Tasks.Scanner.Parser.Parser.SpecialVolumeNumber + 1).Build())
                .Build())
            .Build();
        series.Library = new LibraryBuilder("Test LIb", LibraryType.Manga).Build();

        _context.Series.Add(series);
        _context.AppUser.Add(new AppUser()
        {
            UserName = "majora2007"
        });

        await _context.SaveChangesAsync();

        var nextChapter = await _readerService.GetNextChapterIdAsync(1, 2, 4, 1);
        Assert.Equal(-1, nextChapter);
    }

    [Fact]
    public async Task GetNextChapterIdAsync_ShouldFindNoNextChapterFromVolume()
    {
        await ResetDb();

        var series = new SeriesBuilder("Test")
            .WithVolume(new VolumeBuilder("1")
                .WithChapter(new ChapterBuilder("1").Build())
                .WithChapter(new ChapterBuilder("2").Build())
                .Build())
            .Build();
        series.Library = new LibraryBuilder("Test LIb", LibraryType.Manga).Build();

        _context.Series.Add(series);
        _context.AppUser.Add(new AppUser()
        {
            UserName = "majora2007"
        });

        await _context.SaveChangesAsync();

        var nextChapter = await _readerService.GetNextChapterIdAsync(1, 1, 2, 1);
        Assert.Equal(-1, nextChapter);
    }

    [Fact]
    public async Task GetNextChapterIdAsync_ShouldFindNoNextChapterFromLastChapter_NoSpecials()
    {
        await ResetDb();

        var series = new SeriesBuilder("Test")
            .WithVolume(new VolumeBuilder(API.Services.Tasks.Scanner.Parser.Parser.LooseLeafVolume)
                .WithChapter(new ChapterBuilder("1").Build())
                .WithChapter(new ChapterBuilder("2").Build())
                .Build())
            .Build();
        series.Library = new LibraryBuilder("Test LIb", LibraryType.Manga).Build();

        _context.Series.Add(series);
        _context.AppUser.Add(new AppUser()
        {
            UserName = "majora2007"
        });

        await _context.SaveChangesAsync();

        var nextChapter = await _readerService.GetNextChapterIdAsync(1, 1, 2, 1);
        Assert.Equal(-1, nextChapter);
    }

    // This is commented out because, while valid, I can't solve how to make this pass (https://github.com/Kareadita/Kavita/issues/2099)
    [Fact]
    public async Task GetNextChapterIdAsync_ShouldFindNoNextChapterFromLastChapter_NoSpecials_FirstIsVolume()
    {
        await ResetDb();

        var series = new SeriesBuilder("Test")
            .WithVolume(new VolumeBuilder(API.Services.Tasks.Scanner.Parser.Parser.LooseLeafVolume)
                .WithChapter(new ChapterBuilder("1").Build())
                .WithChapter(new ChapterBuilder("2").Build())
                .Build())
            .WithVolume(new VolumeBuilder("1")
                .WithChapter(new ChapterBuilder(API.Services.Tasks.Scanner.Parser.Parser.DefaultChapter).Build())
                .Build())
            .Build();
        series.Library = new LibraryBuilder("Test LIb", LibraryType.Manga).Build();

        _context.Series.Add(series);
        _context.AppUser.Add(new AppUser()
        {
            UserName = "majora2007"
        });

        await _context.SaveChangesAsync();

        var nextChapter = await _readerService.GetNextChapterIdAsync(1, 1, 2, 1);
        Assert.Equal(-1, nextChapter);
    }

    [Fact]
    public async Task GetNextChapterIdAsync_ShouldFindNoNextChapterFromLastChapter_WithSpecials()
    {
        await ResetDb();

        var series = new SeriesBuilder("Test")
            .WithVolume(new VolumeBuilder(API.Services.Tasks.Scanner.Parser.Parser.LooseLeafVolume)
                .WithChapter(new ChapterBuilder("1").Build())
                .WithChapter(new ChapterBuilder("2").Build())
                .Build())

            .WithVolume(new VolumeBuilder(API.Services.Tasks.Scanner.Parser.Parser.SpecialVolume)
                .WithChapter(new ChapterBuilder(API.Services.Tasks.Scanner.Parser.Parser.DefaultChapter)
                    .WithIsSpecial(true)
                    .WithSortOrder(API.Services.Tasks.Scanner.Parser.Parser.SpecialVolumeNumber + 1)
                    .Build())
                .Build())

            .WithVolume(new VolumeBuilder("1")
                .WithChapter(new ChapterBuilder("2").Build())
                .Build())
            .Build();
        series.Library = new LibraryBuilder("Test LIb", LibraryType.Manga).Build();

        _context.Series.Add(series);
        _context.AppUser.Add(new AppUser()
        {
            UserName = "majora2007"
        });

        await _context.SaveChangesAsync();

        var nextChapter = await _readerService.GetNextChapterIdAsync(1, 2, 3, 1);
        Assert.Equal(-1, nextChapter);
    }



    [Fact]
    public async Task GetNextChapterIdAsync_ShouldMoveFromVolumeToSpecial_NoLooseLeafChapters()
    {
        await ResetDb();

        var series = new SeriesBuilder("Test")
            .WithVolume(new VolumeBuilder("1")
                .WithChapter(new ChapterBuilder("1").Build())
                .WithChapter(new ChapterBuilder("2").Build())
                .Build())

            .WithVolume(new VolumeBuilder(API.Services.Tasks.Scanner.Parser.Parser.SpecialVolume)
                .WithChapter(new ChapterBuilder("A.cbz")
                    .WithIsSpecial(true)
                    .WithSortOrder(API.Services.Tasks.Scanner.Parser.Parser.SpecialVolumeNumber + 1)
                    .Build())
                .WithChapter(new ChapterBuilder("B.cbz")
                    .WithIsSpecial(true)
                    .WithSortOrder(API.Services.Tasks.Scanner.Parser.Parser.SpecialVolumeNumber + 2)
                    .Build())
                .Build())
            .Build();
        series.Library = new LibraryBuilder("Test LIb", LibraryType.Manga).Build();

        _context.Series.Add(series);


        _context.AppUser.Add(new AppUser()
        {
            UserName = "majora2007"
        });

        await _context.SaveChangesAsync();

        var nextChapter = await _readerService.GetNextChapterIdAsync(1, 1, 2, 1);
        Assert.NotEqual(-1, nextChapter);
        var actualChapter = await _unitOfWork.ChapterRepository.GetChapterAsync(nextChapter);
        Assert.NotNull(actualChapter);
        Assert.Equal("A.cbz", actualChapter.Range);
    }

    [Fact]
    public async Task GetNextChapterIdAsync_ShouldMoveFromLooseLeafChapterToSpecial()
    {
        await ResetDb();

        var series = new SeriesBuilder("Test")
            .WithVolume(new VolumeBuilder(API.Services.Tasks.Scanner.Parser.Parser.LooseLeafVolume)
                .WithChapter(new ChapterBuilder("1").Build())
                .WithChapter(new ChapterBuilder("2").Build())
                .Build())

            .WithVolume(new VolumeBuilder(API.Services.Tasks.Scanner.Parser.Parser.SpecialVolume)
                .WithChapter(new ChapterBuilder("A.cbz")
                    .WithIsSpecial(true)
                    .WithSortOrder(API.Services.Tasks.Scanner.Parser.Parser.SpecialVolumeNumber + 1)
                    .WithPages(1)
                    .Build())
                .Build())
            .Build();
        series.Library = new LibraryBuilder("Test LIb", LibraryType.Manga).Build();

        _context.Series.Add(series);

        _context.AppUser.Add(new AppUser()
        {
            UserName = "majora2007"
        });

        await _context.SaveChangesAsync();


        var nextChapter = await _readerService.GetNextChapterIdAsync(1, 1, 2, 1);
        Assert.NotEqual(-1, nextChapter);
        var actualChapter = await _unitOfWork.ChapterRepository.GetChapterAsync(nextChapter);
        Assert.NotNull(actualChapter);
        Assert.Equal("A.cbz", actualChapter.Range);
    }

    [Fact]
    public async Task GetNextChapterIdAsync_ShouldFindNoNextChapterFromSpecial_WithVolumeAndLooseLeafChapters()
    {
        await ResetDb();

        var series = new SeriesBuilder("Test")
            .WithVolume(new VolumeBuilder(API.Services.Tasks.Scanner.Parser.Parser.LooseLeafVolume)
                .WithChapter(new ChapterBuilder("1").Build())
                .WithChapter(new ChapterBuilder("2").Build())
                .Build())

            .WithVolume(new VolumeBuilder("1")
                .WithChapter(new ChapterBuilder(API.Services.Tasks.Scanner.Parser.Parser.DefaultChapter).Build())
                .Build())

            .WithVolume(new VolumeBuilder(API.Services.Tasks.Scanner.Parser.Parser.SpecialVolume)
                .WithChapter(new ChapterBuilder("A.cbz")
                    .WithIsSpecial(true)
                    .WithSortOrder(API.Services.Tasks.Scanner.Parser.Parser.SpecialVolumeNumber + 1)
                    .WithPages(1)
                    .Build())
                .Build())
            .Build();
        series.Library = new LibraryBuilder("Test LIb", LibraryType.Manga).Build();

        _context.Series.Add(series);

        _context.AppUser.Add(new AppUser()
        {
            UserName = "majora2007"
        });

        await _context.SaveChangesAsync();

        var nextChapter = await _readerService.GetNextChapterIdAsync(1, 3, 4, 1);
        Assert.Equal(-1, nextChapter);
    }


    [Fact]
    public async Task GetNextChapterIdAsync_ShouldMoveFromSpecialToSpecial()
    {
        await ResetDb();

        var series = new SeriesBuilder("Test")
            .WithVolume(new VolumeBuilder("1")
                .WithChapter(new ChapterBuilder("1").Build())
                .WithChapter(new ChapterBuilder("2").Build())
                .Build())
            .WithVolume(new VolumeBuilder(API.Services.Tasks.Scanner.Parser.Parser.SpecialVolume)
                .WithChapter(new ChapterBuilder("A.cbz")
                    .WithIsSpecial(true)
                    .WithSortOrder(API.Services.Tasks.Scanner.Parser.Parser.SpecialVolumeNumber + 1)
                    .Build())
                .WithChapter(new ChapterBuilder("B.cbz")
                    .WithIsSpecial(true)
                    .WithSortOrder(API.Services.Tasks.Scanner.Parser.Parser.SpecialVolumeNumber + 2)
                    .Build())
                .Build())
            .Build();
        series.Library = new LibraryBuilder("Test LIb", LibraryType.Manga).Build();

        _context.Series.Add(series);

        _context.AppUser.Add(new AppUser()
        {
            UserName = "majora2007"
        });

        await _context.SaveChangesAsync();


        var nextChapter = await _readerService.GetNextChapterIdAsync(1, 2, 3, 1);
        Assert.NotEqual(-1, nextChapter);
        var actualChapter = await _unitOfWork.ChapterRepository.GetChapterAsync(nextChapter);
        Assert.NotNull(actualChapter);
        Assert.Equal("B.cbz", actualChapter.Range);
    }

    [Fact]
    public async Task GetNextChapterIdAsync_ShouldRollIntoNextVolume_WhenAllVolumesHaveAChapterToo()
    {
        await ResetDb();

        var series = new SeriesBuilder("Test")
            .WithVolume(new VolumeBuilder("1")
                .WithChapter(new ChapterBuilder("12").Build())
                .Build())

            .WithVolume(new VolumeBuilder("2")
                .WithChapter(new ChapterBuilder("12").Build())
                .Build())
            .Build();
        series.Library = new LibraryBuilder("Test LIb", LibraryType.Manga).Build();

        _context.Series.Add(series);

        var user = new AppUserBuilder("majora2007", "fake").Build();

        _context.AppUser.Add(user);

        await _context.SaveChangesAsync();

        await _readerService.MarkChaptersAsRead(user, 1, new List<Chapter>()
        {
            series.Volumes.First().Chapters.First()
        });

        var nextChapter = await _readerService.GetNextChapterIdAsync(1, 1, 1, 1);
        var actualChapter = await _unitOfWork.ChapterRepository.GetChapterAsync(nextChapter, ChapterIncludes.Volumes);
        Assert.Equal(2, actualChapter.Volume.MinNumber);
    }

    #endregion

    #region GetPrevChapterIdAsync

    [Fact]
    public async Task GetPrevChapterIdAsync_ShouldGetPrevVolume()
    {
        // V1 -> V2
        await ResetDb();

        var series = new SeriesBuilder("Test")
            .WithVolume(new VolumeBuilder("1")
                .WithChapter(new ChapterBuilder("1").Build())
                .WithChapter(new ChapterBuilder("2").Build())
                .Build())

            .WithVolume(new VolumeBuilder("2")
                .WithChapter(new ChapterBuilder("21").Build())
                .WithChapter(new ChapterBuilder("22").Build())
                .Build())

            .WithVolume(new VolumeBuilder("3")
                .WithChapter(new ChapterBuilder("31").Build())
                .WithChapter(new ChapterBuilder("32").Build())
                .Build())
            .Build();
        series.Library = new LibraryBuilder("Test LIb", LibraryType.Manga).Build();

        _context.Series.Add(series);

        _context.AppUser.Add(new AppUser()
        {
            UserName = "majora2007"
        });

        await _context.SaveChangesAsync();



        var prevChapter = await _readerService.GetPrevChapterIdAsync(1, 1, 2, 1);
        var actualChapter = await _unitOfWork.ChapterRepository.GetChapterAsync(prevChapter);
        Assert.NotNull(actualChapter);
        Assert.Equal("1", actualChapter.Range);
    }

    [Fact]
    public async Task GetPrevChapterIdAsync_ShouldGetPrevVolume_WithFloatVolume()
    {
        // V1 -> V2
        await ResetDb();

        var series = new SeriesBuilder("Test")
            .WithVolume(new VolumeBuilder("1")
                .WithChapter(new ChapterBuilder("1").Build())
                .WithChapter(new ChapterBuilder("2").Build())
                .Build())

            .WithVolume(new VolumeBuilder("1.5")
                .WithChapter(new ChapterBuilder("21").Build())
                .WithChapter(new ChapterBuilder("22").Build())
                .Build())

            .WithVolume(new VolumeBuilder("3")
                .WithChapter(new ChapterBuilder("31").Build())
                .WithChapter(new ChapterBuilder("32").Build())
                .Build())
            .Build();
        series.Library = new LibraryBuilder("Test LIb", LibraryType.Manga).Build();
        _context.Series.Add(series);
        _context.AppUser.Add(new AppUser()
        {
            UserName = "majora2007"
        });

        await _context.SaveChangesAsync();



        var prevChapter = await _readerService.GetPrevChapterIdAsync(1, 3, 5, 1);
        var actualChapter = await _unitOfWork.ChapterRepository.GetChapterAsync(prevChapter);
        Assert.NotNull(actualChapter);
        Assert.Equal("22", actualChapter.Range);
    }

    [Fact]
    public async Task GetPrevChapterIdAsync_ShouldGetPrevVolume_2()
    {
        await ResetDb();

        var series = new SeriesBuilder("Test")
            .WithVolume(new VolumeBuilder(API.Services.Tasks.Scanner.Parser.Parser.LooseLeafVolume)
                .WithChapter(new ChapterBuilder("40").WithPages(1).Build())
                .WithChapter(new ChapterBuilder("50").WithPages(1).Build())
                .WithChapter(new ChapterBuilder("60").WithPages(1).Build())
                .Build())

            .WithVolume(new VolumeBuilder(API.Services.Tasks.Scanner.Parser.Parser.SpecialVolume)
                .WithChapter(new ChapterBuilder("Some Special Title")
                    .WithIsSpecial(true)
                    .WithSortOrder(API.Services.Tasks.Scanner.Parser.Parser.SpecialVolumeNumber + 1)
                    .WithPages(1)
                    .Build())
                .Build())

            .WithVolume(new VolumeBuilder("1997")
                .WithChapter(new ChapterBuilder("1").WithPages(1).Build())
                .Build())

            .WithVolume(new VolumeBuilder("2001")
                .WithChapter(new ChapterBuilder("21").WithPages(1).Build())
                .Build())

            .WithVolume(new VolumeBuilder("2005")
                .WithChapter(new ChapterBuilder("31").WithPages(1).Build())
                .Build())
            .Build();
        series.Library = new LibraryBuilder("Test LIb", LibraryType.Manga).Build();
        _context.Series.Add(series);
        _context.AppUser.Add(new AppUser()
        {
            UserName = "majora2007"
        });

        await _context.SaveChangesAsync();



        // prevChapter should be id from ch.21 from volume 2001
        var prevChapter = await _readerService.GetPrevChapterIdAsync(1, 5, 7, 1);

        var actualChapter = await _unitOfWork.ChapterRepository.GetChapterAsync(prevChapter);
        Assert.NotNull(actualChapter);
        Assert.Equal("21", actualChapter.Range);
    }

    [Fact]
    public async Task GetPrevChapterIdAsync_ShouldRollIntoPrevVolume()
    {
        await ResetDb();

        var series = new SeriesBuilder("Test")
            .WithVolume(new VolumeBuilder("1")
                .WithChapter(new ChapterBuilder("1").WithPages(1).Build())
                .WithChapter(new ChapterBuilder("2").WithPages(1).Build())
                .Build())

            .WithVolume(new VolumeBuilder("2")
                .WithChapter(new ChapterBuilder("21").WithPages(1).Build())
                .WithChapter(new ChapterBuilder("22").WithPages(1).Build())
                .Build())

            .WithVolume(new VolumeBuilder("3")
                .WithChapter(new ChapterBuilder("31").WithPages(1).Build())
                .WithChapter(new ChapterBuilder("32").WithPages(1).Build())
                .Build())
            .Build();
        series.Library = new LibraryBuilder("Test LIb", LibraryType.Manga).Build();

        _context.Series.Add(series);

        _context.AppUser.Add(new AppUser()
        {
            UserName = "majora2007"
        });

        await _context.SaveChangesAsync();




        var prevChapter = await _readerService.GetPrevChapterIdAsync(1, 2, 3, 1);
        var actualChapter = await _unitOfWork.ChapterRepository.GetChapterAsync(prevChapter);
        Assert.NotNull(actualChapter);
        Assert.Equal("2", actualChapter.Range);
    }

    [Fact]
    public async Task GetPrevChapterIdAsync_ShouldMoveFromSpecialToVolume()
    {
        await ResetDb();

        var series = new SeriesBuilder("Test")
            .WithVolume(new VolumeBuilder("1")
                .WithChapter(new ChapterBuilder("1").Build())
                .WithChapter(new ChapterBuilder("2").Build())
                .Build())

            .WithVolume(new VolumeBuilder(API.Services.Tasks.Scanner.Parser.Parser.SpecialVolume)
                .WithChapter(new ChapterBuilder("A.cbz").WithIsSpecial(true).WithSortOrder(API.Services.Tasks.Scanner.Parser.Parser.SpecialVolumeNumber + 1).Build())
                .WithChapter(new ChapterBuilder("B.cbz").WithIsSpecial(true).WithSortOrder(API.Services.Tasks.Scanner.Parser.Parser.SpecialVolumeNumber + 2).Build())
                .Build())
            .Build();
        series.Library = new LibraryBuilder("Test LIb", LibraryType.Manga).Build();

        _context.Series.Add(series);

        _context.AppUser.Add(new AppUser()
        {
            UserName = "majora2007"
        });

        await _context.SaveChangesAsync();




        var prevChapter = await _readerService.GetPrevChapterIdAsync(1, 2, 3, 1);
        Assert.Equal(2, prevChapter);
        var actualChapter = await _unitOfWork.ChapterRepository.GetChapterAsync(prevChapter);
        Assert.NotNull(actualChapter);
        Assert.Equal("2", actualChapter.Range);
    }

    [Fact]
    public async Task GetPrevChapterIdAsync_ShouldFindNoPrevChapterFromVolume()
    {
        await ResetDb();

        var series = new SeriesBuilder("Test")
            .WithVolume(new VolumeBuilder("1")
                .WithChapter(new ChapterBuilder("1").Build())
                .WithChapter(new ChapterBuilder("2").Build())
                .Build())
            .Build();
        series.Library = new LibraryBuilder("Test LIb", LibraryType.Manga).Build();

        _context.Series.Add(series);

        _context.AppUser.Add(new AppUser()
        {
            UserName = "majora2007"
        });

        await _context.SaveChangesAsync();




        var prevChapter = await _readerService.GetPrevChapterIdAsync(1, 1, 1, 1);
        Assert.Equal(-1, prevChapter);
    }

    [Fact]
    public async Task GetPrevChapterIdAsync_ShouldFindNoPrevChapterFromVolumeWithZeroChapter()
    {
        await ResetDb();

        var series = new SeriesBuilder("Test")
            .WithVolume(new VolumeBuilder("1")
                .WithChapter(new ChapterBuilder(API.Services.Tasks.Scanner.Parser.Parser.DefaultChapter).Build())
                .Build())
            .Build();
        series.Library = new LibraryBuilder("Test LIb", LibraryType.Manga).Build();

        _context.Series.Add(series);

        _context.AppUser.Add(new AppUser()
        {
            UserName = "majora2007"
        });

        await _context.SaveChangesAsync();




        var prevChapter = await _readerService.GetPrevChapterIdAsync(1, 1, 1, 1);
        Assert.Equal(-1, prevChapter);
    }

    [Fact]
    public async Task GetPrevChapterIdAsync_ShouldFindNoPrevChapterFromVolumeWithZeroChapterAndHasNormalChapters()
    {
        await ResetDb();

        var series = new SeriesBuilder("Test")
            .WithVolume(new VolumeBuilder(API.Services.Tasks.Scanner.Parser.Parser.LooseLeafVolume)
                .WithChapter(new ChapterBuilder("1").Build())
                .WithChapter(new ChapterBuilder("2").Build())
                .Build())

            .WithVolume(new VolumeBuilder("1")
                .WithChapter(new ChapterBuilder(API.Services.Tasks.Scanner.Parser.Parser.DefaultChapter).Build())
                .Build())
            .Build();
        series.Library = new LibraryBuilder("Test LIb", LibraryType.Manga).Build();

        _context.Series.Add(series);

        _context.AppUser.Add(new AppUser()
        {
            UserName = "majora2007"
        });

        await _context.SaveChangesAsync();

        var prevChapter = await _readerService.GetPrevChapterIdAsync(1, 2, 3, 1);
        Assert.Equal(-1, prevChapter);
    }

    [Fact]
    public async Task GetPrevChapterIdAsync_ShouldFindNoPrevChapterFromVolumeWithZeroChapterAndHasNormalChapters2()
    {
        await ResetDb();

        var series = new SeriesBuilder("Test")
            .WithVolume(new VolumeBuilder(API.Services.Tasks.Scanner.Parser.Parser.LooseLeafVolume)
                .WithChapter(new ChapterBuilder("5").Build())
                .WithChapter(new ChapterBuilder("6").Build())
                .WithChapter(new ChapterBuilder("7").Build())
                .Build())

            .WithVolume(new VolumeBuilder("1")
                .WithChapter(new ChapterBuilder("1").Build())
                .WithChapter(new ChapterBuilder("2").Build())
                .Build())

            .WithVolume(new VolumeBuilder("2")
                .WithChapter(new ChapterBuilder("3").Build())
                .WithChapter(new ChapterBuilder("4").Build())
                .Build())
            .Build();
        series.Library = new LibraryBuilder("Test LIb", LibraryType.Manga).Build();

        _context.Series.Add(series);


        _context.AppUser.Add(new AppUser()
        {
            UserName = "majora2007"
        });

        await _context.SaveChangesAsync();



        var prevChapter = await _readerService.GetPrevChapterIdAsync(1, 2,5, 1);
        var chapterInfoDto = await _unitOfWork.ChapterRepository.GetChapterInfoDtoAsync(prevChapter);
        Assert.Equal(1, chapterInfoDto.ChapterNumber.AsFloat());

        // This is first chapter of first volume
        prevChapter = await _readerService.GetPrevChapterIdAsync(1, 2,4, 1);
        Assert.Equal(-1, prevChapter);
    }

    [Fact]
    public async Task GetPrevChapterIdAsync_ShouldFindNoPrevChapterFromChapter()
    {
        await ResetDb();

        var series = new SeriesBuilder("Test")
            .WithVolume(new VolumeBuilder(API.Services.Tasks.Scanner.Parser.Parser.LooseLeafVolume)
                .WithChapter(new ChapterBuilder("1").Build())
                .WithChapter(new ChapterBuilder("2").Build())
                .Build())
            .Build();
        series.Library = new LibraryBuilder("Test LIb", LibraryType.Manga).Build();

        _context.Series.Add(series);

        _context.AppUser.Add(new AppUser()
        {
            UserName = "majora2007"
        });

        await _context.SaveChangesAsync();




        var prevChapter = await _readerService.GetPrevChapterIdAsync(1, 1, 1, 1);
        Assert.Equal(-1, prevChapter);
    }

    [Fact]
    public async Task GetPrevChapterIdAsync_ShouldMoveFromSpecialToSpecial()
    {
        await ResetDb();

        var series = new SeriesBuilder("Test")
            .WithVolume(new VolumeBuilder("1")
                .WithChapter(new ChapterBuilder("1").Build())
                .WithChapter(new ChapterBuilder("2").Build())
                .Build())
            .WithVolume(new VolumeBuilder(API.Services.Tasks.Scanner.Parser.Parser.SpecialVolume)
                .WithChapter(new ChapterBuilder("A.cbz")
                    .WithIsSpecial(true)
                    .WithSortOrder(API.Services.Tasks.Scanner.Parser.Parser.SpecialVolumeNumber + 1)
                    .Build())
                .WithChapter(new ChapterBuilder("B.cbz")
                    .WithIsSpecial(true)
                    .WithSortOrder(API.Services.Tasks.Scanner.Parser.Parser.SpecialVolumeNumber + 2)
                    .Build())
                .Build())
            .Build();
        series.Library = new LibraryBuilder("Test LIb", LibraryType.Manga).Build();

        _context.Series.Add(series);


        _context.AppUser.Add(new AppUser()
        {
            UserName = "majora2007"
        });

        await _context.SaveChangesAsync();




        var prevChapter = await _readerService.GetPrevChapterIdAsync(1, 2, 4, 1);
        Assert.NotEqual(-1, prevChapter);
        var actualChapter = await _unitOfWork.ChapterRepository.GetChapterAsync(prevChapter);
        Assert.NotNull(actualChapter);
        Assert.Equal("A.cbz", actualChapter.Range);
    }

    [Fact]
    public async Task GetPrevChapterIdAsync_ShouldMoveFromChapterToVolume()
    {
        await ResetDb();

        var series = new SeriesBuilder("Test")
            .WithVolume(new VolumeBuilder(API.Services.Tasks.Scanner.Parser.Parser.LooseLeafVolume)
                .WithChapter(new ChapterBuilder("1").Build())
                .WithChapter(new ChapterBuilder("2").Build())
                .Build())
            .WithVolume(new VolumeBuilder("1")
                .WithChapter(new ChapterBuilder("21").Build())
                .WithChapter(new ChapterBuilder("22").Build())
                .Build())

            .Build();
        series.Library = new LibraryBuilder("Test LIb", LibraryType.Manga).Build();

        _context.Series.Add(series);

        _context.AppUser.Add(new AppUser()
        {
            UserName = "majora2007"
        });

        await _context.SaveChangesAsync();

        var prevChapter = await _readerService.GetPrevChapterIdAsync(1, 1, 1, 1);
        Assert.NotEqual(-1, prevChapter);
        var actualChapter = await _unitOfWork.ChapterRepository.GetChapterAsync(prevChapter);
        Assert.NotNull(actualChapter);
        Assert.Equal("22", actualChapter.Range);
    }

    [Fact]
    public async Task GetPrevChapterIdAsync_ShouldRollIntoPrevVolume_WhenAllVolumesHaveAChapterToo()
    {
        await ResetDb();

        var series = new SeriesBuilder("Test")
            .WithVolume(new VolumeBuilder("1")
                .WithChapter(new ChapterBuilder("12").Build())
                .Build())

            .WithVolume(new VolumeBuilder("2")
                .WithChapter(new ChapterBuilder("12").Build())
                .Build())
            .Build();
        series.Library = new LibraryBuilder("Test LIb", LibraryType.Manga).Build();

        _context.Series.Add(series);

        var user = new AppUserBuilder("majora2007", "fake").Build();

        _context.AppUser.Add(user);

        await _context.SaveChangesAsync();

        var nextChapter = await _readerService.GetPrevChapterIdAsync(1, 2, 2, 1);
        var actualChapter = await _unitOfWork.ChapterRepository.GetChapterAsync(nextChapter, ChapterIncludes.Volumes);
        Assert.Equal(1, actualChapter.Volume.MinNumber);
    }

    #endregion

    #region GetContinuePoint

    [Fact]
    public async Task GetContinuePoint_ShouldReturnFirstVolume_NoProgress()
    {
        await ResetDb();
        var series = new SeriesBuilder("Test")
            .WithVolume(new VolumeBuilder(API.Services.Tasks.Scanner.Parser.Parser.LooseLeafVolume)
                .WithChapter(new ChapterBuilder("95").Build())
                .WithChapter(new ChapterBuilder("96").Build())
                .Build())
            .WithVolume(new VolumeBuilder("1")
                .WithChapter(new ChapterBuilder("1").Build())
                .WithChapter(new ChapterBuilder("2").Build())
                .Build())
            .WithVolume(new VolumeBuilder("2")
                .WithChapter(new ChapterBuilder("21").Build())
                .WithChapter(new ChapterBuilder("22").Build())
                .Build())
            .WithVolume(new VolumeBuilder("3")
                .WithChapter(new ChapterBuilder("31").Build())
                .WithChapter(new ChapterBuilder("32").Build())
                .Build())

            .Build();
        series.Library = new LibraryBuilder("Test LIb", LibraryType.Manga).Build();

        _context.Series.Add(series);


        _context.AppUser.Add(new AppUser()
        {
            UserName = "majora2007"
        });

        await _context.SaveChangesAsync();




        var nextChapter = await _readerService.GetContinuePoint(1, 1);

        Assert.Equal("1", nextChapter.Range);
    }

    [Fact]
    public async Task GetContinuePoint_ShouldReturnFirstVolume_WhenFirstVolumeIsAlsoTaggedAsChapter1_WithProgress()
    {
        await ResetDb();
        var series = new SeriesBuilder("Test")
            .WithVolume(new VolumeBuilder("1")
                .WithChapter(new ChapterBuilder("1").WithPages(3).Build())
                .Build())
            .WithVolume(new VolumeBuilder("2")
                .WithChapter(new ChapterBuilder(API.Services.Tasks.Scanner.Parser.Parser.DefaultChapter).WithPages(1).Build())
                .Build())
            .WithPages(4)
            .Build();
        series.Library = new LibraryBuilder("Test LIb", LibraryType.Manga).Build();

        _context.Series.Add(series);

        _context.AppUser.Add(new AppUser()
        {
            UserName = "majora2007"
        });

        await _context.SaveChangesAsync();




        await _readerService.SaveReadingProgress(new ProgressDto()
        {
            PageNum = 2,
            ChapterId = 1,
            SeriesId = 1,
            VolumeId = 1
        }, 1);
        var nextChapter = await _readerService.GetContinuePoint(1, 1);

        Assert.Equal("1", nextChapter.Range);
    }

    [Fact]
    public async Task GetContinuePoint_ShouldReturnFirstVolume_WhenFirstVolumeIsAlsoTaggedAsChapter1Through11_WithProgress()
    {
        await ResetDb();
        var series = new SeriesBuilder("Test")
            .WithVolume(new VolumeBuilder("1")
                .WithChapter(new ChapterBuilder("1", "1-11").WithPages(3).Build())
                .Build())
            .WithVolume(new VolumeBuilder("2")
                .WithChapter(new ChapterBuilder(API.Services.Tasks.Scanner.Parser.Parser.DefaultChapter).WithPages(1).Build())
                .Build())
            .WithPages(4)
            .Build();
        series.Library = new LibraryBuilder("Test LIb", LibraryType.Manga).Build();

        _context.Series.Add(series);

        _context.AppUser.Add(new AppUser()
        {
            UserName = "majora2007"
        });

        await _context.SaveChangesAsync();




        await _readerService.SaveReadingProgress(new ProgressDto()
        {
            PageNum = 2,
            ChapterId = 1,
            SeriesId = 1,
            VolumeId = 1
        }, 1);
        var nextChapter = await _readerService.GetContinuePoint(1, 1);

        Assert.Equal("1-11", nextChapter.Range);
    }

    [Fact]
    public async Task GetContinuePoint_ShouldReturnFirstNonSpecial()
    {
        await ResetDb();
        var series = new SeriesBuilder("Test")
            .WithVolume(new VolumeBuilder("1")
                .WithChapter(new ChapterBuilder("1").WithPages(1).Build())
                .WithChapter(new ChapterBuilder("2").WithPages(1).Build())
                .Build())

            .WithVolume(new VolumeBuilder("2")
                .WithChapter(new ChapterBuilder("21").WithPages(1).Build())
                .WithChapter(new ChapterBuilder("22").WithPages(1).Build())
                .Build())
            .WithVolume(new VolumeBuilder("3")
                .WithChapter(new ChapterBuilder("31").WithPages(1).Build())
                .WithChapter(new ChapterBuilder("32").WithPages(1).Build())
                .Build())
            .Build();
        series.Library = new LibraryBuilder("Test LIb", LibraryType.Manga).Build();

        _context.Series.Add(series);

        _context.AppUser.Add(new AppUser()
        {
            UserName = "majora2007"
        });

        await _context.SaveChangesAsync();




        // Save progress on first volume chapters and 1st of second volume
        await _readerService.SaveReadingProgress(new ProgressDto()
        {
            PageNum = 1,
            ChapterId = 1,
            SeriesId = 1,
            VolumeId = 1
        }, 1);
        await _readerService.SaveReadingProgress(new ProgressDto()
        {
            PageNum = 1,
            ChapterId = 2,
            SeriesId = 1,
            VolumeId = 1
        }, 1);
        await _readerService.SaveReadingProgress(new ProgressDto()
        {
            PageNum = 1,
            ChapterId = 3,
            SeriesId = 1,
            VolumeId = 2
        }, 1);

        await _context.SaveChangesAsync();

        var nextChapter = await _readerService.GetContinuePoint(1, 1);

        Assert.Equal("22", nextChapter.Range);


    }

    [Fact]
    public async Task GetContinuePoint_ShouldReturnFirstNonSpecial2()
    {
        await ResetDb();
        var series = new SeriesBuilder("Test")
            // Loose chapters
            .WithVolume(new VolumeBuilder(API.Services.Tasks.Scanner.Parser.Parser.LooseLeafVolume)
                .WithChapter(new ChapterBuilder("45").WithPages(1).Build())
                .WithChapter(new ChapterBuilder("46").WithPages(1).Build())
                .WithChapter(new ChapterBuilder("47").WithPages(1).Build())
                .WithChapter(new ChapterBuilder("48").WithPages(1).Build())
                .Build())
            .WithVolume(new VolumeBuilder(API.Services.Tasks.Scanner.Parser.Parser.SpecialVolume)
                .WithChapter(new ChapterBuilder("Some Special Title")
                    .WithSortOrder(API.Services.Tasks.Scanner.Parser.Parser.SpecialVolumeNumber + 1)
                    .WithIsSpecial(true).WithPages(1)
                    .Build())
                .Build())
            // One file volume
            .WithVolume(new VolumeBuilder("1")
                .WithChapter(new ChapterBuilder(API.Services.Tasks.Scanner.Parser.Parser.DefaultChapter).WithPages(1).Build()) // Read
                .Build())
            // Chapter-based volume
            .WithVolume(new VolumeBuilder("2")
                .WithChapter(new ChapterBuilder("21").WithPages(1).Build()) // Read
                .WithChapter(new ChapterBuilder("22").WithPages(1).Build())
                .Build())
            // Chapter-based volume
            .WithVolume(new VolumeBuilder("3")
                .WithChapter(new ChapterBuilder("31").WithPages(1).Build())
                .WithChapter(new ChapterBuilder("32").WithPages(1).Build())
                .Build())
            .Build();
        series.Library = new LibraryBuilder("Test LIb", LibraryType.Manga).Build();

        _context.Series.Add(series);

        _context.AppUser.Add(new AppUser()
        {
            UserName = "majora2007"
        });

        await _context.SaveChangesAsync();



        // Save progress on first volume and 1st chapter of second volume
        await _readerService.SaveReadingProgress(new ProgressDto()
        {
            PageNum = 1,
            ChapterId = 6, // Chapter 0 volume 1 id
            SeriesId = 1,
            VolumeId = 2 // Volume 1 id
        }, 1);


        await _readerService.SaveReadingProgress(new ProgressDto()
        {
            PageNum = 1,
            ChapterId = 7, // Chapter 21 volume 2 id
            SeriesId = 1,
            VolumeId = 3 // Volume 2 id
        }, 1);

        await _context.SaveChangesAsync();

        var nextChapter = await _readerService.GetContinuePoint(1, 1);

        Assert.Equal("22", nextChapter.Range);


    }


    [Fact]
    public async Task GetContinuePoint_ShouldReturnFirstChapter_WhenHasSpecial()
    {
        await ResetDb();
        var series = new SeriesBuilder("Test")
            // Loose chapters
            .WithVolume(new VolumeBuilder(API.Services.Tasks.Scanner.Parser.Parser.LooseLeafVolume)
                .WithChapter(new ChapterBuilder("1").WithPages(1).Build())
                .WithChapter(new ChapterBuilder("2").WithPages(1).Build())
                .Build())
            .WithVolume(new VolumeBuilder(API.Services.Tasks.Scanner.Parser.Parser.SpecialVolume)
                .WithChapter(new ChapterBuilder("Prologue").WithIsSpecial(true).WithSortOrder(API.Services.Tasks.Scanner.Parser.Parser.SpecialVolumeNumber + 1).WithPages(1).Build())
                .Build())
            .Build();
        series.Library = new LibraryBuilder("Test LIb", LibraryType.Manga).Build();

        _context.Series.Add(series);

        _context.AppUser.Add(new AppUser()
        {
            UserName = "majora2007"
        });

        await _context.SaveChangesAsync();

        var nextChapter = await _readerService.GetContinuePoint(1, 1);

        Assert.Equal("1", nextChapter.Range);
    }

    [Fact]
    public async Task GetContinuePoint_ShouldReturnFirstSpecial()
    {
        await ResetDb();
        var series = new SeriesBuilder("Test")
            .WithVolume(new VolumeBuilder("1")
                .WithChapter(new ChapterBuilder("1").WithPages(1).Build())
                .WithChapter(new ChapterBuilder("2").WithPages(1).Build())
                .Build())
            .WithVolume(new VolumeBuilder("2")
                .WithChapter(new ChapterBuilder("21").WithPages(1).Build())
                .Build())
            .WithVolume(new VolumeBuilder(API.Services.Tasks.Scanner.Parser.Parser.LooseLeafVolume)
                .WithChapter(new ChapterBuilder("31").WithPages(1).Build())
                .WithChapter(new ChapterBuilder("32").WithPages(1).Build())
                .Build())
            .Build();
        series.Library = new LibraryBuilder("Test LIb", LibraryType.Manga).Build();

        _context.Series.Add(series);

        _context.AppUser.Add(new AppUser()
        {
            UserName = "majora2007"
        });

        await _context.SaveChangesAsync();




        // Save progress on first volume chapters and 1st of second volume
        await _readerService.SaveReadingProgress(new ProgressDto()
        {
            PageNum = 1,
            ChapterId = 1,
            SeriesId = 1,
            VolumeId = 1
        }, 1);
        await _readerService.SaveReadingProgress(new ProgressDto()
        {
            PageNum = 1,
            ChapterId = 2,
            SeriesId = 1,
            VolumeId = 1
        }, 1);
        await _readerService.SaveReadingProgress(new ProgressDto()
        {
            PageNum = 1,
            ChapterId = 3,
            SeriesId = 1,
            VolumeId = 2
        }, 1);

        await _context.SaveChangesAsync();

        var nextChapter = await _readerService.GetContinuePoint(1, 1);

        Assert.Equal("31", nextChapter.Range);
    }

    [Fact]
    public async Task GetContinuePoint_ShouldReturnFirstChapter_WhenNonRead_LooseLeafChaptersAndVolumes()
    {
        await ResetDb();
        var series = new SeriesBuilder("Test")
            .WithVolume(new VolumeBuilder(API.Services.Tasks.Scanner.Parser.Parser.LooseLeafVolume)
                .WithChapter(new ChapterBuilder("230").WithPages(1).Build())
                .WithChapter(new ChapterBuilder("231").WithPages(1).Build())
                .Build())

            .WithVolume(new VolumeBuilder("1")
                .WithChapter(new ChapterBuilder("1").WithPages(1).Build())
                .WithChapter(new ChapterBuilder("2").WithPages(1).Build())
                .Build())
            .WithVolume(new VolumeBuilder("2")
                .WithChapter(new ChapterBuilder("21").WithPages(1).Build())
                .Build())
            .Build();
        series.Library = new LibraryBuilder("Test LIb", LibraryType.Manga).Build();

        _context.Series.Add(series);


        _context.AppUser.Add(new AppUser()
        {
            UserName = "majora2007"
        });

        await _context.SaveChangesAsync();


        var nextChapter = await _readerService.GetContinuePoint(1, 1);

        Assert.Equal("1", nextChapter.Range);
    }

    [Fact]
    public async Task GetContinuePoint_ShouldReturnLooseChapter_WhenAllVolumesRead_HasSpecialAndLooseChapters_Unread()
    {
        await ResetDb();
        var series = new SeriesBuilder("Test")
            .WithVolume(new VolumeBuilder(API.Services.Tasks.Scanner.Parser.Parser.LooseLeafVolume)
                .WithChapter(new ChapterBuilder("100").WithPages(1).Build())
                .WithChapter(new ChapterBuilder("101").WithPages(1).Build())
                .Build())
            .WithVolume(new VolumeBuilder(API.Services.Tasks.Scanner.Parser.Parser.SpecialVolume)
                .WithChapter(new ChapterBuilder("Christmas Eve").WithIsSpecial(true).WithSortOrder(API.Services.Tasks.Scanner.Parser.Parser.SpecialVolumeNumber + 1).WithPages(1).Build())
                .Build())

            .WithVolume(new VolumeBuilder("1")
                .WithChapter(new ChapterBuilder(API.Services.Tasks.Scanner.Parser.Parser.DefaultChapter).WithPages(1).Build())
                .Build())
            .WithVolume(new VolumeBuilder("2")
                .WithChapter(new ChapterBuilder(API.Services.Tasks.Scanner.Parser.Parser.DefaultChapter).WithPages(1).Build())
                .Build())
            .Build();
        series.Library = new LibraryBuilder("Test LIb", LibraryType.Manga).Build();

        _context.Series.Add(series);

        var user = new AppUser()
        {
            UserName = "majora2007"
        };
        _context.AppUser.Add(user);

        await _context.SaveChangesAsync();

        // Mark everything but chapter 101 as read
        await _readerService.MarkSeriesAsRead(user, 1);
        await _unitOfWork.CommitAsync();

        // Unmark last chapter as read
        var vol = await _unitOfWork.VolumeRepository.GetVolumeByIdAsync(1);
        foreach (var chapt in vol.Chapters)
        {
            await _readerService.SaveReadingProgress(new ProgressDto()
            {
                PageNum = 0,
                ChapterId = chapt.Id,
                SeriesId = 1,
                VolumeId = 1
            }, 1);
        }
        await _context.SaveChangesAsync();

        var nextChapter = await _readerService.GetContinuePoint(1, 1);

        Assert.Equal("100", nextChapter.Range);
    }

    [Fact]
    public async Task GetContinuePoint_ShouldReturnLooseChapter_WhenAllVolumesAndAFewLooseChaptersRead()
    {
        await ResetDb();
        var series = new SeriesBuilder("Test")
            .WithVolume(new VolumeBuilder(API.Services.Tasks.Scanner.Parser.Parser.LooseLeafVolume)
                .WithChapter(new ChapterBuilder("100").WithPages(1).Build())
                .WithChapter(new ChapterBuilder("101").WithPages(1).Build())
                .WithChapter(new ChapterBuilder("102").WithPages(1).Build())
                .Build())

            .WithVolume(new VolumeBuilder("1")
                .WithChapter(new ChapterBuilder("1").WithPages(1).Build())
                .WithChapter(new ChapterBuilder("2").WithPages(1).Build())
                .Build())
            .WithVolume(new VolumeBuilder("2")
                .WithChapter(new ChapterBuilder("21").WithPages(1).Build())
                .Build())
            .Build();
        series.Library = new LibraryBuilder("Test LIb", LibraryType.Manga).Build();

        _context.Series.Add(series);

        var user = new AppUser()
        {
            UserName = "majora2007"
        };
        _context.AppUser.Add(user);

        await _context.SaveChangesAsync();

        // Mark everything but chapter 101 as read
        await _readerService.MarkSeriesAsRead(user, 1);
        await _unitOfWork.CommitAsync();

        // Unmark last chapter as read
        var vol = await _unitOfWork.VolumeRepository.GetVolumeByIdAsync(1);
        await _readerService.SaveReadingProgress(new ProgressDto()
        {
            PageNum = 0,
            ChapterId = vol.Chapters.ElementAt(1).Id,
            SeriesId = 1,
            VolumeId = 1
        }, 1);
        await _readerService.SaveReadingProgress(new ProgressDto()
        {
            PageNum = 0,
            ChapterId = vol.Chapters.ElementAt(2).Id,
            SeriesId = 1,
            VolumeId = 1
        }, 1);
        await _context.SaveChangesAsync();

        var nextChapter = await _readerService.GetContinuePoint(1, 1);

        Assert.Equal("101", nextChapter.Range);
    }

    [Fact]
    public async Task GetContinuePoint_ShouldReturnFirstChapter_WhenAllRead()
    {
        await ResetDb();
        var series = new SeriesBuilder("Test")
            .WithVolume(new VolumeBuilder("1")
                .WithChapter(new ChapterBuilder("1").WithPages(1).Build())
                .WithChapter(new ChapterBuilder("2").WithPages(1).Build())
                .Build())

            .WithVolume(new VolumeBuilder("2")
                .WithChapter(new ChapterBuilder("21").WithPages(1).Build())
                .Build())
            .Build();
        series.Library = new LibraryBuilder("Test LIb", LibraryType.Manga).Build();

        _context.Series.Add(series);

        _context.AppUser.Add(new AppUser()
        {
            UserName = "majora2007"
        });

        await _context.SaveChangesAsync();



        // Save progress on first volume chapters and 1st of second volume
        await _readerService.SaveReadingProgress(new ProgressDto()
        {
            PageNum = 1,
            ChapterId = 1,
            SeriesId = 1,
            VolumeId = 1
        }, 1);
        await _readerService.SaveReadingProgress(new ProgressDto()
        {
            PageNum = 1,
            ChapterId = 2,
            SeriesId = 1,
            VolumeId = 1
        }, 1);
        await _readerService.SaveReadingProgress(new ProgressDto()
        {
            PageNum = 1,
            ChapterId = 3,
            SeriesId = 1,
            VolumeId = 2
        }, 1);

        await _context.SaveChangesAsync();

        var nextChapter = await _readerService.GetContinuePoint(1, 1);

        Assert.Equal("1", nextChapter.Range);
    }

    [Fact]
    public async Task GetContinuePoint_ShouldReturnFirstChapter_WhenAllReadAndAllChapters()
    {
        await ResetDb();
        var series = new SeriesBuilder("Test")

            .WithVolume(new VolumeBuilder(API.Services.Tasks.Scanner.Parser.Parser.LooseLeafVolume)
                .WithChapter(new ChapterBuilder("1").WithPages(1).Build())
                .WithChapter(new ChapterBuilder("2").WithPages(1).Build())
                .WithChapter(new ChapterBuilder("3").WithPages(1).Build())
                .Build())

            .WithVolume(new VolumeBuilder("1")
                .WithChapter(new ChapterBuilder("11").WithPages(1).Build())
                .WithChapter(new ChapterBuilder("22").WithPages(1).Build())
                .Build())
            .Build();
        series.Library = new LibraryBuilder("Test LIb", LibraryType.Manga).Build();

        _context.Series.Add(series);

        _context.AppUser.Add(new AppUser()
        {
            UserName = "majora2007"
        });

        await _context.SaveChangesAsync();



        // Save progress on first volume chapters and 1st of second volume
        var user = await _unitOfWork.UserRepository.GetUserByIdAsync(1, AppUserIncludes.Progress);
        await _readerService.MarkSeriesAsRead(user, 1);
        await _context.SaveChangesAsync();

        var nextChapter = await _readerService.GetContinuePoint(1, 1);

        Assert.Equal("11", nextChapter.Range);
    }

    [Fact]
    public async Task GetContinuePoint_ShouldReturnFirstSpecial_WhenAllReadAndAllChapters()
    {
        await ResetDb();
        var series = new SeriesBuilder("Test")

            .WithVolume(new VolumeBuilder(API.Services.Tasks.Scanner.Parser.Parser.LooseLeafVolume)
                .WithChapter(new ChapterBuilder("1").WithPages(1).Build())
                .WithChapter(new ChapterBuilder("2").WithPages(1).Build())
                .WithChapter(new ChapterBuilder("3").WithPages(1).Build())
                .Build())
            .WithVolume(new VolumeBuilder(API.Services.Tasks.Scanner.Parser.Parser.SpecialVolume)
                .WithChapter(new ChapterBuilder("Some Special Title").WithIsSpecial(true).WithSortOrder(API.Services.Tasks.Scanner.Parser.Parser.SpecialVolumeNumber + 1).WithPages(1).Build())
                .Build())
            .Build();
        series.Library = new LibraryBuilder("Test LIb", LibraryType.Manga).Build();

        _context.Series.Add(series);

        _context.AppUser.Add(new AppUser()
        {
            UserName = "majora2007"
        });

        await _context.SaveChangesAsync();



        // Save progress on first volume chapters and 1st of second volume
        await _readerService.SaveReadingProgress(new ProgressDto()
        {
            PageNum = 1,
            ChapterId = 1,
            SeriesId = 1,
            VolumeId = 1
        }, 1);
        await _readerService.SaveReadingProgress(new ProgressDto()
        {
            PageNum = 1,
            ChapterId = 2,
            SeriesId = 1,
            VolumeId = 1
        }, 1);
        await _readerService.SaveReadingProgress(new ProgressDto()
        {
            PageNum = 1,
            ChapterId = 3,
            SeriesId = 1,
            VolumeId = 1
        }, 1);

        await _context.SaveChangesAsync();

        var nextChapter = await _readerService.GetContinuePoint(1, 1);

        Assert.Equal("Some Special Title", nextChapter.Range);
    }

    [Fact]
    public async Task GetContinuePoint_ShouldReturnFirstVolumeChapter_WhenPreExistingProgress()
    {
        await ResetDb();
        var series = new SeriesBuilder("Test")

            .WithVolume(new VolumeBuilder(API.Services.Tasks.Scanner.Parser.Parser.LooseLeafVolume)
                .WithChapter(new ChapterBuilder("230").WithPages(1).Build())
                //.WithChapter(new ChapterBuilder("231").WithPages(1).Build())  (Added later)
                .Build())

            .WithVolume(new VolumeBuilder("1")
                .WithChapter(new ChapterBuilder("1").WithPages(1).Build())
                .WithChapter(new ChapterBuilder("2").WithPages(1).Build())
                .Build())
            .WithVolume(new VolumeBuilder("2")
                .WithChapter(new ChapterBuilder(API.Services.Tasks.Scanner.Parser.Parser.DefaultChapter).WithPages(1).Build())
                //.WithChapter(new ChapterBuilder("14.9").WithPages(1).Build()) (added later)
                .Build())
            .Build();
        series.Library = new LibraryBuilder("Test LIb", LibraryType.Manga).Build();

        _context.Series.Add(series);

        _context.AppUser.Add(new AppUser()
        {
            UserName = "majora2007"
        });

        await _context.SaveChangesAsync();



        var user = await _unitOfWork.UserRepository.GetUserByIdAsync(1, AppUserIncludes.Progress);
        await _readerService.MarkSeriesAsRead(user, 1);
        await _context.SaveChangesAsync();

        // Add 2 new unread series to the Series
        series.Volumes[0].Chapters.Add(new ChapterBuilder("231")
            .WithPages(1)
            .Build());
        series.Volumes[2].Chapters.Add(new ChapterBuilder("14.9")
            .WithPages(1)
            .Build());
        _context.Series.Attach(series);
        await _context.SaveChangesAsync();

        // This tests that if you add a series later to a volume and a loose leaf chapter, we continue from that volume, rather than loose leaf
        var nextChapter = await _readerService.GetContinuePoint(1, 1);
        Assert.Equal("14.9", nextChapter.Range);
    }

    [Fact]
    public async Task GetContinuePoint_ShouldReturnUnreadSingleVolume_WhenThereAreSomeSingleVolumesBeforeLooseLeafChapters()
    {
        await ResetDb();
        var readChapter1 = new ChapterBuilder(API.Services.Tasks.Scanner.Parser.Parser.DefaultChapter).WithPages(1).Build();
        var readChapter2 = new ChapterBuilder(API.Services.Tasks.Scanner.Parser.Parser.DefaultChapter).WithPages(1).Build();
        var volume = new VolumeBuilder("3").WithChapter(new ChapterBuilder(API.Services.Tasks.Scanner.Parser.Parser.DefaultChapter).WithPages(1).Build()).Build();

        var series = new SeriesBuilder("Test")

            .WithVolume(new VolumeBuilder(API.Services.Tasks.Scanner.Parser.Parser.LooseLeafVolume)
                .WithChapter(new ChapterBuilder("51").WithPages(1).Build())
                .WithChapter(new ChapterBuilder("52").WithPages(1).Build())
                .WithChapter(new ChapterBuilder("53").WithPages(1).Build())
                .Build())

            .WithVolume(new VolumeBuilder("1")
                .WithChapter(readChapter1)
                .Build())
            .WithVolume(new VolumeBuilder("2")
                .WithChapter(readChapter2)
                .Build())
            // 3, 4, and all loose leafs are unread should be unread
            .WithVolume(new VolumeBuilder("3")
                .WithChapter(new ChapterBuilder(API.Services.Tasks.Scanner.Parser.Parser.DefaultChapter).WithPages(1).Build())
                .Build())
            .WithVolume(new VolumeBuilder("4")
                .WithChapter(new ChapterBuilder("40").WithPages(1).Build())
                .WithChapter(new ChapterBuilder("41").WithPages(1).Build())
                .Build())
            .Build();
        series.Library = new LibraryBuilder("Test LIb", LibraryType.Manga).Build();

        _context.Series.Add(series);


        _context.AppUser.Add(new AppUser()
        {
            UserName = "majora2007"
        });

        await _context.SaveChangesAsync();



        // Save progress on first volume chapters and 1st of second volume
        var user = await _unitOfWork.UserRepository.GetUserByIdAsync(1, AppUserIncludes.Progress);
        await _readerService.MarkChaptersAsRead(user, 1,
            new List<Chapter>()
            {
                readChapter1, readChapter2
            });
        await _context.SaveChangesAsync();

        var nextChapter = await _readerService.GetContinuePoint(1, 1);

        Assert.Equal(4, nextChapter.VolumeId);
    }


    /// <summary>
    /// Volume 1-10 are fully read (single volumes),
    /// Special 1 is fully read
    /// Chapters 56-90 are read
    /// Chapter 91 has partial progress on
    /// </summary>
    [Fact]
    public async Task GetContinuePoint_ShouldReturnLastLooseChapter()
    {
        await ResetDb();
        var series = new SeriesBuilder("Test")
            .WithVolume(new VolumeBuilder("1")
                .WithChapter(new ChapterBuilder("1").WithPages(1).Build())
                .Build())
            .WithVolume(new VolumeBuilder("2")
                .WithChapter(new ChapterBuilder("21").WithPages(1).Build())
                .WithChapter(new ChapterBuilder("22").WithPages(1).Build())
                .Build())
            .WithVolume(new VolumeBuilder(API.Services.Tasks.Scanner.Parser.Parser.LooseLeafVolume)
                .WithChapter(new ChapterBuilder("51").WithPages(1).Build())
                .WithChapter(new ChapterBuilder("52").WithPages(1).Build())
                .WithChapter(new ChapterBuilder("91").WithPages(2).Build())
                .Build())
            .WithVolume(new VolumeBuilder(API.Services.Tasks.Scanner.Parser.Parser.SpecialVolume)
                .WithChapter(new ChapterBuilder("Special").WithIsSpecial(true).WithSortOrder(API.Services.Tasks.Scanner.Parser.Parser.SpecialVolumeNumber + 1).WithPages(1).Build())
                .Build())
            .Build();
        series.Library = new LibraryBuilder("Test LIb", LibraryType.Manga).Build();

        _context.Series.Add(series);

        _context.AppUser.Add(new AppUser()
        {
            UserName = "majora2007"
        });

        await _context.SaveChangesAsync();

        await _readerService.SaveReadingProgress(new ProgressDto()
        {
            PageNum = 1,
            ChapterId = 1,
            SeriesId = 1,
            VolumeId = 1
        }, 1);
        await _readerService.SaveReadingProgress(new ProgressDto()
        {
            PageNum = 1,
            ChapterId = 2,
            SeriesId = 1,
            VolumeId = 1
        }, 1);
        await _readerService.SaveReadingProgress(new ProgressDto()
        {
            PageNum = 1,
            ChapterId = 3,
            SeriesId = 1,
            VolumeId = 2
        }, 1);

        await _readerService.SaveReadingProgress(new ProgressDto()
        {
            PageNum = 1,
            ChapterId = 4,
            SeriesId = 1,
            VolumeId = 2
        }, 1);

        await _readerService.SaveReadingProgress(new ProgressDto()
        {
            PageNum = 1,
            ChapterId = 5,
            SeriesId = 1,
            VolumeId = 2
        }, 1);

        // Chapter 91 has partial progress, hence it should resume there
        await _readerService.SaveReadingProgress(new ProgressDto()
        {
            PageNum = 1,
            ChapterId = 6,
            SeriesId = 1,
            VolumeId = 2
        }, 1);

        // Special is fully read
        await _readerService.SaveReadingProgress(new ProgressDto()
        {
            PageNum = 1,
            ChapterId = 7,
            SeriesId = 1,
            VolumeId = 2
        }, 1);

        await _context.SaveChangesAsync();

        var nextChapter = await _readerService.GetContinuePoint(1, 1);

        Assert.Equal("91", nextChapter.Range);
    }

    [Fact]
    public async Task GetContinuePoint_DuplicateIssueNumberBetweenChapters()
    {
        await ResetDb();
        var series = new SeriesBuilder("Test")
            .WithVolume(new VolumeBuilder("1")
                .WithChapter(new ChapterBuilder("1").WithPages(1).Build())
                .WithChapter(new ChapterBuilder("2").WithPages(1).Build())
                .WithChapter(new ChapterBuilder("21").WithPages(1).Build())
                .WithChapter(new ChapterBuilder("22").WithPages(1).Build())
                .WithChapter(new ChapterBuilder("32").WithPages(1).Build())
                .Build())
            .WithVolume(new VolumeBuilder("2")
                .WithChapter(new ChapterBuilder("1").WithPages(1).Build())
                .WithChapter(new ChapterBuilder("2").WithPages(1).Build())
                .WithChapter(new ChapterBuilder("21").WithPages(1).Build())
                .WithChapter(new ChapterBuilder("22").WithPages(1).Build())
                .WithChapter(new ChapterBuilder("32").WithPages(1).Build())
                .Build())
            .Build();
        series.Library = new LibraryBuilder("Test LIb", LibraryType.Manga).Build();

        _context.Series.Add(series);

        _context.AppUser.Add(new AppUser()
        {
            UserName = "majora2007"
        });

        await _context.SaveChangesAsync();

        await _readerService.SaveReadingProgress(new ProgressDto()
        {
            PageNum = 1,
            ChapterId = 1,
            SeriesId = 1,
            VolumeId = 1
        }, 1);

        await _context.SaveChangesAsync();

        var nextChapter = await _readerService.GetContinuePoint(1, 1);

        Assert.Equal("2", nextChapter.Range);
        Assert.Equal(1, nextChapter.VolumeId);

        // Mark chapter 2 as read
        await _readerService.SaveReadingProgress(new ProgressDto()
        {
            PageNum = 1,
            ChapterId = 2,
            SeriesId = 1,
            VolumeId = 1
        }, 1);
        await _context.SaveChangesAsync();

        nextChapter = await _readerService.GetContinuePoint(1, 1);

        Assert.Equal("21", nextChapter.Range);
        Assert.Equal(1, nextChapter.VolumeId);

        // Mark chapter 21 as read
        await _readerService.SaveReadingProgress(new ProgressDto()
        {
            PageNum = 1,
            ChapterId = 3,
            SeriesId = 1,
            VolumeId = 1
        }, 1);
        await _context.SaveChangesAsync();

        nextChapter = await _readerService.GetContinuePoint(1, 1);

        Assert.Equal("22", nextChapter.Range);
        Assert.Equal(1, nextChapter.VolumeId);
    }


    #endregion

    #region MarkChaptersUntilAsRead

    [Fact]
    public async Task MarkChaptersUntilAsRead_ShouldMarkAllChaptersAsRead()
    {
        await ResetDb();
        var series = new SeriesBuilder("Test")

            .WithVolume(new VolumeBuilder(API.Services.Tasks.Scanner.Parser.Parser.LooseLeafVolume)
                .WithChapter(new ChapterBuilder("1").WithPages(1).Build())
                .WithChapter(new ChapterBuilder("2").WithPages(1).Build())
                .WithChapter(new ChapterBuilder("3").WithPages(1).Build())
                .Build())
            .WithVolume(new VolumeBuilder(API.Services.Tasks.Scanner.Parser.Parser.SpecialVolume)
                .WithChapter(new ChapterBuilder("Some Special Title").WithSortOrder(API.Services.Tasks.Scanner.Parser.Parser.SpecialVolumeNumber + 1).WithIsSpecial(true).WithPages(1).Build())
                .Build())
            .Build();
        series.Library = new LibraryBuilder("Test LIb", LibraryType.Manga).Build();

        _context.Series.Add(series);

        _context.AppUser.Add(new AppUser()
        {
            UserName = "majora2007"
        });

        await _context.SaveChangesAsync();



        var user = await _unitOfWork.UserRepository.GetUserByUsernameAsync("majora2007", AppUserIncludes.Progress);
        await _readerService.MarkChaptersUntilAsRead(user, 1, 5);
        await _context.SaveChangesAsync();

        // Validate correct chapters have read status
        Assert.Equal(1, (await _unitOfWork.AppUserProgressRepository.GetUserProgressAsync(1, 1)).PagesRead);
        Assert.Equal(1, (await _unitOfWork.AppUserProgressRepository.GetUserProgressAsync(2, 1)).PagesRead);
        Assert.Equal(1, (await _unitOfWork.AppUserProgressRepository.GetUserProgressAsync(3, 1)).PagesRead);
        Assert.Null((await _unitOfWork.AppUserProgressRepository.GetUserProgressAsync(4, 1)));
    }

    [Fact]
    public async Task MarkChaptersUntilAsRead_ShouldMarkUptTillChapterNumberAsRead()
    {
        await ResetDb();
        var series = new SeriesBuilder("Test")

            .WithVolume(new VolumeBuilder(API.Services.Tasks.Scanner.Parser.Parser.LooseLeafVolume)
                .WithChapter(new ChapterBuilder("1").WithPages(1).Build())
                .WithChapter(new ChapterBuilder("2").WithPages(1).Build())
                .WithChapter(new ChapterBuilder("2.5").WithPages(1).Build())
                .WithChapter(new ChapterBuilder("3").WithPages(1).Build())
                .Build())
                .WithVolume(new VolumeBuilder(API.Services.Tasks.Scanner.Parser.Parser.SpecialVolume)
                    .WithChapter(new ChapterBuilder("Some Special Title").WithSortOrder(API.Services.Tasks.Scanner.Parser.Parser.SpecialVolumeNumber + 1).WithIsSpecial(true).WithPages(1).Build())
                    .Build())
            .Build();
        series.Library = new LibraryBuilder("Test LIb", LibraryType.Manga).Build();

        _context.Series.Add(series);

        _context.AppUser.Add(new AppUser()
        {
            UserName = "majora2007"
        });

        await _context.SaveChangesAsync();



        var user = await _unitOfWork.UserRepository.GetUserByUsernameAsync("majora2007", AppUserIncludes.Progress);
        await _readerService.MarkChaptersUntilAsRead(user, 1, 2.5f);
        await _context.SaveChangesAsync();

        // Validate correct chapters have read status
        Assert.Equal(1, (await _unitOfWork.AppUserProgressRepository.GetUserProgressAsync(1, 1)).PagesRead);
        Assert.Equal(1, (await _unitOfWork.AppUserProgressRepository.GetUserProgressAsync(2, 1)).PagesRead);
        Assert.Equal(1, (await _unitOfWork.AppUserProgressRepository.GetUserProgressAsync(3, 1)).PagesRead);
        Assert.Null((await _unitOfWork.AppUserProgressRepository.GetUserProgressAsync(4, 1)));
        Assert.Null((await _unitOfWork.AppUserProgressRepository.GetUserProgressAsync(5, 1)));
    }

    [Fact]
    public async Task MarkChaptersUntilAsRead_ShouldMarkAsRead_OnlyVolumesWithChapter0()
    {
        await ResetDb();
        var series = new SeriesBuilder("Test")

            .WithVolume(new VolumeBuilder("1")
                .WithChapter(new ChapterBuilder(API.Services.Tasks.Scanner.Parser.Parser.DefaultChapter).WithPages(1).Build())
                .Build())
            .WithVolume(new VolumeBuilder("2")
                .WithChapter(new ChapterBuilder(API.Services.Tasks.Scanner.Parser.Parser.DefaultChapter).WithPages(1).Build())
                .Build())
            .Build();
        series.Library = new LibraryBuilder("Test LIb", LibraryType.Manga).Build();

        _context.Series.Add(series);

        _context.AppUser.Add(new AppUser()
        {
            UserName = "majora2007"
        });

        await _context.SaveChangesAsync();



        var user = await _unitOfWork.UserRepository.GetUserByUsernameAsync("majora2007", AppUserIncludes.Progress);
        await _readerService.MarkChaptersUntilAsRead(user, 1, 2);
        await _context.SaveChangesAsync();

        // Validate correct chapters have read status
        Assert.True(await _unitOfWork.AppUserProgressRepository.UserHasProgress(LibraryType.Manga, 1));
    }

    [Fact]
    public async Task MarkChaptersUntilAsRead_ShouldMarkAsReadAnythingUntil()
    {
        await ResetDb();
        var series = new SeriesBuilder("Test")

            .WithVolume(new VolumeBuilder(API.Services.Tasks.Scanner.Parser.Parser.LooseLeafVolume)
                .WithChapter(new ChapterBuilder("45").WithPages(5).Build())
                .WithChapter(new ChapterBuilder("46").WithPages(46).Build())
                .WithChapter(new ChapterBuilder("47").WithPages(47).Build())
                .WithChapter(new ChapterBuilder("48").WithPages(48).Build())
                .WithChapter(new ChapterBuilder("49").WithPages(49).Build())
                .WithChapter(new ChapterBuilder("50").WithPages(50).Build())
                .Build())
            .WithVolume(new VolumeBuilder(API.Services.Tasks.Scanner.Parser.Parser.SpecialVolume)
                .WithChapter(new ChapterBuilder("Some Special Title").WithSortOrder(API.Services.Tasks.Scanner.Parser.Parser.SpecialVolumeNumber + 1).WithIsSpecial(true).WithPages(10).Build())
                .Build())

            .WithVolume(new VolumeBuilder("1")
                .WithChapter(new ChapterBuilder(API.Services.Tasks.Scanner.Parser.Parser.DefaultChapter).WithPages(6).Build())
                .Build())
            .WithVolume(new VolumeBuilder("2")
                .WithChapter(new ChapterBuilder(API.Services.Tasks.Scanner.Parser.Parser.DefaultChapter).WithPages(7).Build())
                .Build())
            .WithVolume(new VolumeBuilder("3")
                .WithChapter(new ChapterBuilder("12").WithPages(5).Build())
                .WithChapter(new ChapterBuilder("13").WithPages(5).Build())
                .WithChapter(new ChapterBuilder("14").WithPages(5).Build())
                .Build())
            .Build();
        series.Library = new LibraryBuilder("Test LIb", LibraryType.Manga).Build();

        _context.Series.Add(series);

        _context.AppUser.Add(new AppUser()
        {
            UserName = "majora2007"
        });

        await _context.SaveChangesAsync();



        var user = await _unitOfWork.UserRepository.GetUserByUsernameAsync("majora2007", AppUserIncludes.Progress);
        const int markReadUntilNumber = 47;

        await _readerService.MarkChaptersUntilAsRead(user, 1, markReadUntilNumber);
        await _context.SaveChangesAsync();

        var volumes = await _unitOfWork.VolumeRepository.GetVolumesDtoAsync(1, 1);
        Assert.True(volumes.SelectMany(v => v.Chapters).All(c =>
        {
            // Specials are ignored.
            var notReadChapterRanges = new[] {"Some Special Title", "48", "49", "50"};
            if (notReadChapterRanges.Contains(c.Range))
            {
                return c.PagesRead == 0;
            }
            // Pages read and total pages must match -> chapter fully read
            return c.Pages == c.PagesRead;

        }));
    }

    #endregion

    #region MarkSeriesAsRead

    [Fact]
    public async Task MarkSeriesAsReadTest()
    {
        await ResetDb();

        var series = new SeriesBuilder("Test")

            .WithVolume(new VolumeBuilder("1")
                .WithChapter(new ChapterBuilder(API.Services.Tasks.Scanner.Parser.Parser.DefaultChapter).WithPages(1).Build())
                .WithChapter(new ChapterBuilder("1").WithPages(2).Build())
                .Build())
            .WithVolume(new VolumeBuilder("2")
                .WithChapter(new ChapterBuilder(API.Services.Tasks.Scanner.Parser.Parser.DefaultChapter).WithPages(1).Build())
                .WithChapter(new ChapterBuilder("1").WithPages(2).Build())
                .Build())
            .Build();
        series.Library = new LibraryBuilder("Test LIb", LibraryType.Manga).Build();

        _context.Series.Add(series);

        _context.AppUser.Add(new AppUser()
        {
            UserName = "majora2007"
        });

        await _context.SaveChangesAsync();



        await _readerService.MarkSeriesAsRead(await _unitOfWork.UserRepository.GetUserByIdAsync(1, AppUserIncludes.Progress), 1);
        await _context.SaveChangesAsync();

        Assert.Equal(4, (await _unitOfWork.UserRepository.GetUserByIdAsync(1, AppUserIncludes.Progress)).Progresses.Count);
    }


    #endregion

    #region MarkSeriesAsUnread

    [Fact]
    public async Task MarkSeriesAsUnreadTest()
    {
        await ResetDb();
        var series = new SeriesBuilder("Test")

            .WithVolume(new VolumeBuilder(API.Services.Tasks.Scanner.Parser.Parser.LooseLeafVolume)
                .WithChapter(new ChapterBuilder(API.Services.Tasks.Scanner.Parser.Parser.DefaultChapter).WithPages(1).Build())
                .WithChapter(new ChapterBuilder("1").WithPages(2).Build())
                .Build())
            .Build();
        series.Library = new LibraryBuilder("Test LIb", LibraryType.Manga).Build();

        _context.Series.Add(series);

        _context.AppUser.Add(new AppUser()
        {
            UserName = "majora2007"
        });

        await _context.SaveChangesAsync();



        var volumes = (await _unitOfWork.VolumeRepository.GetVolumes(1)).ToList();
        await _readerService.MarkChaptersAsRead(await _unitOfWork.UserRepository.GetUserByIdAsync(1, AppUserIncludes.Progress), 1, volumes.First().Chapters);

        await _context.SaveChangesAsync();
        Assert.Equal(2, (await _unitOfWork.UserRepository.GetUserByIdAsync(1, AppUserIncludes.Progress)).Progresses.Count);

        await _readerService.MarkSeriesAsUnread(await _unitOfWork.UserRepository.GetUserByIdAsync(1, AppUserIncludes.Progress), 1);
        await _context.SaveChangesAsync();

        var progresses = (await _unitOfWork.UserRepository.GetUserByIdAsync(1, AppUserIncludes.Progress)).Progresses;
        Assert.Equal(0, progresses.Max(p => p.PagesRead));
        Assert.Equal(2, progresses.Count);
    }

    #endregion

    #region FormatChapterName

    [Fact]
    public void FormatChapterName_Manga_Chapter()
    {
        var actual = ReaderService.FormatChapterName(LibraryType.Manga, false, false);
        Assert.Equal("Chapter", actual);
    }

    [Fact]
    public void FormatChapterName_Book_Chapter_WithTitle()
    {
        var actual = ReaderService.FormatChapterName(LibraryType.Book, false, false);
        Assert.Equal("Book", actual);
    }

    [Fact]
    public void FormatChapterName_Comic()
    {
        var actual = ReaderService.FormatChapterName(LibraryType.Comic, false, false);
        Assert.Equal("Issue", actual);
    }

    [Fact]
    public void FormatChapterName_Comic_WithHash()
    {
        var actual = ReaderService.FormatChapterName(LibraryType.Comic, true, true);
        Assert.Equal("Issue #", actual);
    }

    #endregion

    #region MarkVolumesUntilAsRead
    [Fact]
    public async Task MarkVolumesUntilAsRead_ShouldMarkVolumesAsRead()
    {
        await ResetDb();
        var series = new SeriesBuilder("Test")

            .WithVolume(new VolumeBuilder(API.Services.Tasks.Scanner.Parser.Parser.LooseLeafVolume)
                .WithChapter(new ChapterBuilder("10").WithPages(1).Build())
                .WithChapter(new ChapterBuilder("20").WithPages(1).Build())
                .WithChapter(new ChapterBuilder("30").WithPages(1).Build())
                .Build())
            .WithVolume(new VolumeBuilder(API.Services.Tasks.Scanner.Parser.Parser.SpecialVolume)
                .WithChapter(new ChapterBuilder("Some Special Title").WithSortOrder(API.Services.Tasks.Scanner.Parser.Parser.SpecialVolumeNumber + 1).WithIsSpecial(true).WithPages(1).Build())
                .Build())
            .WithVolume(new VolumeBuilder("1997")
                .WithChapter(new ChapterBuilder(API.Services.Tasks.Scanner.Parser.Parser.DefaultChapter).WithPages(1).Build())
                .Build())

            .WithVolume(new VolumeBuilder("2002")
                .WithChapter(new ChapterBuilder(API.Services.Tasks.Scanner.Parser.Parser.DefaultChapter).WithPages(1).Build())
                .Build())

            .WithVolume(new VolumeBuilder("2003")
                .WithChapter(new ChapterBuilder(API.Services.Tasks.Scanner.Parser.Parser.DefaultChapter).WithPages(1).Build())
                .Build())
            .Build();
        series.Library = new LibraryBuilder("Test LIb", LibraryType.Manga).Build();

        _context.Series.Add(series);

        _context.AppUser.Add(new AppUser()
        {
            UserName = "majora2007"
        });

        await _context.SaveChangesAsync();



        var user = await _unitOfWork.UserRepository.GetUserByUsernameAsync("majora2007", AppUserIncludes.Progress);
        await _readerService.MarkVolumesUntilAsRead(user, 1, 2002);
        await _context.SaveChangesAsync();

        // Validate loose leaf chapters don't get marked as read
        Assert.Null((await _unitOfWork.AppUserProgressRepository.GetUserProgressAsync(1, 1)));
        Assert.Null((await _unitOfWork.AppUserProgressRepository.GetUserProgressAsync(2, 1)));
        Assert.Null((await _unitOfWork.AppUserProgressRepository.GetUserProgressAsync(3, 1)));

        // Validate that volumes 1997 and 2002 both have their respective chapter 0 marked as read
        Assert.Equal(1, (await _unitOfWork.AppUserProgressRepository.GetUserProgressAsync(5, 1)).PagesRead);
        Assert.Equal(1, (await _unitOfWork.AppUserProgressRepository.GetUserProgressAsync(6, 1)).PagesRead);
        // Validate that the chapter 0 of the following volume (2003) is not read
        Assert.Null(await _unitOfWork.AppUserProgressRepository.GetUserProgressAsync(7, 1));

    }

    [Fact]
    public async Task MarkVolumesUntilAsRead_ShouldMarkChapterBasedVolumesAsRead()
    {
        await ResetDb();
        var series = new SeriesBuilder("Test")
            .WithVolume(new VolumeBuilder(API.Services.Tasks.Scanner.Parser.Parser.LooseLeafVolume)
                .WithChapter(new ChapterBuilder("10").WithPages(1).Build())
                .WithChapter(new ChapterBuilder("20").WithPages(1).Build())
                .WithChapter(new ChapterBuilder("30").WithPages(1).Build())
                .Build())
            .WithVolume(new VolumeBuilder(API.Services.Tasks.Scanner.Parser.Parser.SpecialVolume)
                .WithChapter(new ChapterBuilder("Some Special Title").WithSortOrder(API.Services.Tasks.Scanner.Parser.Parser.SpecialVolumeNumber + 1).WithIsSpecial(true).WithPages(1).Build())
                .Build())
            .WithVolume(new VolumeBuilder("1997")
                .WithChapter(new ChapterBuilder("1").WithPages(1).Build())
                .Build())

            .WithVolume(new VolumeBuilder("2002")
                .WithChapter(new ChapterBuilder("2").WithPages(1).Build())
                .Build())

            .WithVolume(new VolumeBuilder("2003")
                .WithChapter(new ChapterBuilder("3").WithPages(1).Build())
                .Build())
            .Build();
        series.Library = new LibraryBuilder("Test LIb", LibraryType.Manga).Build();

        _context.Series.Add(series);

        _context.AppUser.Add(new AppUser()
        {
            UserName = "majora2007"
        });

        await _context.SaveChangesAsync();



        var user = await _unitOfWork.UserRepository.GetUserByUsernameAsync("majora2007", AppUserIncludes.Progress);
        await _readerService.MarkVolumesUntilAsRead(user, 1, 2002);
        await _context.SaveChangesAsync();

        // Validate loose leaf chapters don't get marked as read
        Assert.Null((await _unitOfWork.AppUserProgressRepository.GetUserProgressAsync(1, 1)));
        Assert.Null((await _unitOfWork.AppUserProgressRepository.GetUserProgressAsync(2, 1)));
        Assert.Null((await _unitOfWork.AppUserProgressRepository.GetUserProgressAsync(3, 1)));

        // Validate volumes chapter 0 have read status
        Assert.Equal(1, (await _unitOfWork.AppUserProgressRepository.GetUserProgressAsync(5, 1)).PagesRead);
        Assert.Equal(1, (await _unitOfWork.AppUserProgressRepository.GetUserProgressAsync(6, 1)).PagesRead);
        Assert.Null((await _unitOfWork.AppUserProgressRepository.GetUserProgressAsync(3, 1)));
    }

    #endregion

    #region GetPairs

    [Theory]
    [InlineData("No Wides", new [] {false, false, false}, new [] {"0,0", "1,1", "2,1"})]
    [InlineData("Test_odd_spread_1.zip", new [] {false, false, false, false, false, true},
        new [] {"0,0", "1,1", "2,1", "3,3", "4,3", "5,5"})]
    [InlineData("Test_odd_spread_2.zip", new [] {false, false, false, false, false, true, false, false},
        new [] {"0,0", "1,1", "2,1", "3,3", "4,3", "5,5", "6,6", "7,6"})]
    [InlineData("Test_even_spread_1.zip", new [] {false, false, false, false, false, false, true},
        new [] {"0,0", "1,1", "2,1", "3,3", "4,3", "5,5", "6,6"})]
    [InlineData("Test_even_spread_2.zip", new [] {false, false, false, false, false, false, true, false, false},
        new [] {"0,0", "1,1", "2,1", "3,3", "4,3", "5,5", "6,6", "7,7", "8,7"})]
    [InlineData("Edge_cases_SP01.zip", new [] {true, false, false, false},
        new [] {"0,0", "1,1", "2,1", "3,3"})]
    [InlineData("Edge_cases_SP02.zip", new [] {false, true, false, false, false},
        new [] {"0,0", "1,1", "2,2", "3,2", "4,4"})]
    [InlineData("Edge_cases_SP03.zip", new [] {false, false, false, false, false, true, true, false, false, false},
        new [] {"0,0", "1,1", "2,1", "3,3", "4,3", "5,5", "6,6", "7,7", "8,7", "9,9"})]
    [InlineData("Edge_cases_SP04.zip", new [] {false, false, false, false, false, true, false, true, false, false},
        new [] {"0,0", "1,1", "2,1", "3,3", "4,3", "5,5", "6,6", "7,7", "8,8", "9,8"})]
    [InlineData("Edge_cases_SP05.zip", new [] {false, false, false, false, false, true, false, false, true, false},
        new [] {"0,0", "1,1", "2,1", "3,3", "4,3", "5,5", "6,6", "7,6", "8,8", "9,9"})]
    public void GetPairs_ShouldReturnPairsForNoWideImages(string caseName, IList<bool> wides, IList<string> expectedPairs)
    {

        var files = wides.Select((b, i) => new FileDimensionDto() {PageNumber = i, Height = 1, Width = 1, FileName = string.Empty, IsWide = b}).ToList();
        var pairs = _readerService.GetPairs(files);
        var expectedDict = new Dictionary<int, int>();
        foreach (var pair in expectedPairs)
        {
            var token = pair.Split(',');
            expectedDict.Add(int.Parse(token[0]), int.Parse(token[1]));
        }

        _testOutputHelper.WriteLine("Case: {0}", caseName);
        _testOutputHelper.WriteLine("Expected: {0}", string.Join(", ", expectedDict.Select(kvp => $"{kvp.Key}->{kvp.Value}")));
        _testOutputHelper.WriteLine("Actual: {0}", string.Join(", ", pairs.Select(kvp => $"{kvp.Key}->{kvp.Value}")));

        Assert.Equal(expectedDict, pairs);
    }

    #endregion
}
