using API.Extensions;
using API.Helpers.Builders;
using API.Services.Plus;
using API.Services.Tasks;

namespace API.Tests.Services;
using System.Collections.Generic;
using System.Data.Common;
using System.IO.Abstractions.TestingHelpers;
using System.Linq;
using System.Threading.Tasks;
using Data;
using Data.Repositories;
using API.Entities;
using API.Entities.Enums;
using API.Helpers;
using API.Services;
using SignalR;
using Helpers;
using AutoMapper;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using NSubstitute;
using Xunit;

public class TachiyomiServiceTests
{
    private readonly IUnitOfWork _unitOfWork;
    private readonly IMapper _mapper;
    private readonly DataContext _context;
    private readonly ReaderService _readerService;
    private readonly TachiyomiService _tachiyomiService;
    private const string CacheDirectory = "C:/kavita/config/cache/";
    private const string CoverImageDirectory = "C:/kavita/config/covers/";
    private const string BackupDirectory = "C:/kavita/config/backups/";
    private const string DataDirectory = "C:/data/";


    public TachiyomiServiceTests()
    {
        var contextOptions = new DbContextOptionsBuilder().UseSqlite(CreateInMemoryDatabase()).Options;

        _context = new DataContext(contextOptions);
        Task.Run(SeedDb).GetAwaiter().GetResult();

        var config = new MapperConfiguration(cfg => cfg.AddProfile<AutoMapperProfiles>());
        _mapper = config.CreateMapper();
        _unitOfWork = new UnitOfWork(_context, _mapper, null);

        _readerService = new ReaderService(_unitOfWork, Substitute.For<ILogger<ReaderService>>(),
            Substitute.For<IEventHub>(), Substitute.For<IImageService>(),
            new DirectoryService(Substitute.For<ILogger<DirectoryService>>(), new MockFileSystem()),
            Substitute.For<IScrobblingService>());
        _tachiyomiService = new TachiyomiService(_unitOfWork, _mapper, Substitute.For<ILogger<TachiyomiService>>(), _readerService);

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

        _context.Library.Add(
            new LibraryBuilder("Manga")
                .WithFolderPath(new FolderPathBuilder("C:/data/").Build())
                .Build()
            );
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


    #region GetLatestChapter

    [Fact]
    public async Task GetLatestChapter_ShouldReturnChapter_NoProgress()
    {
        await ResetDb();

        var series = new SeriesBuilder("Test")
            .WithVolume(new VolumeBuilder(API.Services.Tasks.Scanner.Parser.Parser.LooseLeafVolume)
                .WithChapter(new ChapterBuilder("95").WithPages(1).Build())
                .WithChapter(new ChapterBuilder("96").WithPages(1).Build())
                .Build())
            .WithVolume(new VolumeBuilder("1")
                .WithChapter(new ChapterBuilder("1").WithPages(1).Build())
                .Build())
            .WithVolume(new VolumeBuilder("2")
                .WithChapter(new ChapterBuilder("3").WithPages(1).Build())
                .WithChapter(new ChapterBuilder("4").WithPages(1).Build())
                .Build())
            .WithVolume(new VolumeBuilder("3")
                .WithChapter(new ChapterBuilder("31").WithPages(1).Build())
                .WithChapter(new ChapterBuilder("32").WithPages(1).Build())
                .Build())
            .WithPages(7)
            .Build();

        var library = new LibraryBuilder("Test LIb", LibraryType.Manga)
            .WithSeries(series)
            .Build();


        _context.AppUser.Add(new AppUser()
        {
            UserName = "majora2007",
            Libraries = new List<Library>()
            {
                library
            }

        });
        await _context.SaveChangesAsync();

        var latestChapter = await _tachiyomiService.GetLatestChapter(1, 1);

        Assert.Null(latestChapter);
    }

    [Fact]
    public async Task GetLatestChapter_ShouldReturnMaxChapter_CompletelyRead()
    {
        await ResetDb();

        var series = new SeriesBuilder("Test")
            .WithVolume(new VolumeBuilder(API.Services.Tasks.Scanner.Parser.Parser.LooseLeafVolume)
                .WithChapter(new ChapterBuilder("95").WithPages(1).Build())
                .WithChapter(new ChapterBuilder("96").WithPages(1).Build())
                .Build())
            .WithVolume(new VolumeBuilder("1")
                .WithChapter(new ChapterBuilder("1").WithPages(1).Build())
                .Build())
            .WithVolume(new VolumeBuilder("2")
                .WithChapter(new ChapterBuilder("3").WithPages(1).Build())
                .WithChapter(new ChapterBuilder("4").WithPages(1).Build())
                .Build())
            .WithVolume(new VolumeBuilder("3")
                .WithChapter(new ChapterBuilder("31").WithPages(1).Build())
                .WithChapter(new ChapterBuilder("32").WithPages(1).Build())
                .Build())
            .WithPages(7)
            .Build();

        var library = new LibraryBuilder("Test LIb", LibraryType.Manga)
            .WithSeries(series)
            .Build();

        _context.AppUser.Add(new AppUser()
        {
            UserName = "majora2007",
            Libraries = new List<Library>()
            {
                library
            }

        });
        await _context.SaveChangesAsync();


        var user = await _unitOfWork.UserRepository.GetUserByUsernameAsync("majora2007", AppUserIncludes.Progress);
        await _readerService.MarkSeriesAsRead(user,1);

        await _context.SaveChangesAsync();


        var latestChapter = await _tachiyomiService.GetLatestChapter(1, 1);

        Assert.Equal("96", latestChapter.Number);
    }

    [Fact]
    public async Task GetLatestChapter_ShouldReturnHighestChapter_Progress()
    {
        await ResetDb();

        var series = new SeriesBuilder("Test")
            .WithVolume(new VolumeBuilder(API.Services.Tasks.Scanner.Parser.Parser.LooseLeafVolume)
                .WithChapter(new ChapterBuilder("95").WithPages(1).Build())
                .WithChapter(new ChapterBuilder("96").WithPages(1).Build())
                .Build())
            .WithVolume(new VolumeBuilder("1")
                .WithChapter(new ChapterBuilder("1").WithPages(1).Build())
                .Build())
            .WithVolume(new VolumeBuilder("2")
                .WithChapter(new ChapterBuilder("21").WithPages(1).Build())
                .WithChapter(new ChapterBuilder("22").WithPages(1).Build())
                .Build())
            .WithVolume(new VolumeBuilder("3")
                .WithChapter(new ChapterBuilder("31").WithPages(1).Build())
                .WithChapter(new ChapterBuilder("32").WithPages(1).Build())
                .Build())
            .WithPages(7)
            .Build();

        var library = new LibraryBuilder("Test LIb", LibraryType.Manga)
            .WithSeries(series)
            .Build();

        _context.AppUser.Add(new AppUser()
        {
            UserName = "majora2007",
            Libraries = new List<Library>()
            {
                library
            }

        });
        await _context.SaveChangesAsync();


        var user = await _unitOfWork.UserRepository.GetUserByUsernameAsync("majora2007", AppUserIncludes.Progress);
        await _tachiyomiService.MarkChaptersUntilAsRead(user,1,21);

        await _context.SaveChangesAsync();


        var latestChapter = await _tachiyomiService.GetLatestChapter(1, 1);

        Assert.Equal("21", latestChapter.Number);
    }

    [Fact]
    public async Task GetLatestChapter_ShouldReturnEncodedVolume_Progress()
    {
        await ResetDb();

        var series = new SeriesBuilder("Test")
            .WithVolume(new VolumeBuilder(API.Services.Tasks.Scanner.Parser.Parser.LooseLeafVolume)
                .WithChapter(new ChapterBuilder("95").WithPages(1).Build())
                .WithChapter(new ChapterBuilder("96").WithPages(1).Build())
                .Build())
            .WithVolume(new VolumeBuilder("1")
                .WithChapter(new ChapterBuilder("1").WithPages(1).Build())
                .Build())
            .WithVolume(new VolumeBuilder("2")
                .WithChapter(new ChapterBuilder("21").WithPages(1).Build())
                .WithChapter(new ChapterBuilder("22").WithPages(1).Build())
                .Build())
            .WithVolume(new VolumeBuilder("3")
                .WithChapter(new ChapterBuilder("31").WithPages(1).Build())
                .WithChapter(new ChapterBuilder("32").WithPages(1).Build())
                .Build())
            .WithPages(7)
            .Build();

        var library = new LibraryBuilder("Test LIb", LibraryType.Manga)
            .WithSeries(series)
            .Build();

        _context.AppUser.Add(new AppUser()
        {
            UserName = "majora2007",
            Libraries = new List<Library>()
            {
                library
            }

        });
        await _context.SaveChangesAsync();


        var user = await _unitOfWork.UserRepository.GetUserByUsernameAsync("majora2007", AppUserIncludes.Progress);

        await _tachiyomiService.MarkChaptersUntilAsRead(user,1,1/10_000F);

        await _context.SaveChangesAsync();


        var latestChapter = await _tachiyomiService.GetLatestChapter(1, 1);
        Assert.Equal("0.0001", latestChapter.Number);
    }

    [Fact]
    public async Task GetLatestChapter_ShouldReturnEncodedVolume_Progress2()
    {
        await ResetDb();

        var series = new SeriesBuilder("Test")
            .WithVolume(new VolumeBuilder("1")
                .WithChapter(new ChapterBuilder(API.Services.Tasks.Scanner.Parser.Parser.DefaultChapter)
                    .WithPages(199).Build())
                .Build())
            .WithVolume(new VolumeBuilder("2")
                .WithChapter(new ChapterBuilder(API.Services.Tasks.Scanner.Parser.Parser.DefaultChapter)
                    .WithPages(192).Build())
                .Build())
            .WithVolume(new VolumeBuilder("3")
                .WithChapter(new ChapterBuilder(API.Services.Tasks.Scanner.Parser.Parser.DefaultChapter)
                    .WithPages(255).Build())
                .Build())
            .WithPages(646)
            .Build();

        var library = new LibraryBuilder("Test LIb", LibraryType.Manga)
            .WithSeries(series)
            .Build();

        _context.AppUser.Add(new AppUser()
        {
            UserName = "majora2007",
            Libraries = new List<Library>()
            {
                library
            }

        });
        await _context.SaveChangesAsync();


        var user = await _unitOfWork.UserRepository.GetUserByUsernameAsync("majora2007", AppUserIncludes.Progress);

        await _readerService.MarkSeriesAsRead(user, 1);

        await _context.SaveChangesAsync();


        var latestChapter = await _tachiyomiService.GetLatestChapter(1, 1);
        Assert.Equal("0.0003", latestChapter.Number);
    }


    [Fact]
    public async Task GetLatestChapter_ShouldReturnEncodedYearlyVolume_Progress()
    {
        await ResetDb();

        var series = new SeriesBuilder("Test")
            .WithVolume(new VolumeBuilder(API.Services.Tasks.Scanner.Parser.Parser.LooseLeafVolume)
                .WithChapter(new ChapterBuilder("95").WithPages(1).Build())
                .WithChapter(new ChapterBuilder("96").WithPages(1).Build())
                .Build())
            .WithVolume(new VolumeBuilder("1997")
                .WithChapter(new ChapterBuilder("1").WithPages(1).Build())
                .Build())
            .WithVolume(new VolumeBuilder("2002")
                .WithChapter(new ChapterBuilder("2").WithPages(1).Build())
                .Build())
            .WithVolume(new VolumeBuilder("2005")
                .WithChapter(new ChapterBuilder("3").WithPages(1).Build())
                .Build())
            .WithPages(7)
            .Build();

        var library = new LibraryBuilder("Test LIb", LibraryType.Manga)
            .WithSeries(series)
            .Build();

        _context.AppUser.Add(new AppUser()
        {
            UserName = "majora2007",
            Libraries = new List<Library>()
            {
                library
            }

        });
        await _context.SaveChangesAsync();

        var user = await _unitOfWork.UserRepository.GetUserByUsernameAsync("majora2007", AppUserIncludes.Progress);

        await _tachiyomiService.MarkChaptersUntilAsRead(user,1,2002/10_000F);

        await _context.SaveChangesAsync();


        var latestChapter = await _tachiyomiService.GetLatestChapter(1, 1);
        Assert.Equal("0.2002", latestChapter.Number);
    }

    #endregion


    #region MarkChaptersUntilAsRead

    [Fact]
    public async Task MarkChaptersUntilAsRead_ShouldReturnChapter_NoProgress()
    {
        await ResetDb();

        var series = new SeriesBuilder("Test")
            .WithVolume(new VolumeBuilder(API.Services.Tasks.Scanner.Parser.Parser.LooseLeafVolume)
                .WithChapter(new ChapterBuilder("95").WithPages(1).Build())
                .WithChapter(new ChapterBuilder("96").WithPages(1).Build())
                .Build())
            .WithVolume(new VolumeBuilder("1")
                .WithChapter(new ChapterBuilder("1").WithPages(1).Build())
                .Build())
            .WithVolume(new VolumeBuilder("2")
                .WithChapter(new ChapterBuilder("3").WithPages(1).Build())
                .WithChapter(new ChapterBuilder("4").WithPages(1).Build())
                .Build())
            .WithVolume(new VolumeBuilder("3")
                .WithChapter(new ChapterBuilder("31").WithPages(1).Build())
                .WithChapter(new ChapterBuilder("32").WithPages(1).Build())
                .Build())
            .WithPages(7)
            .Build();

        var library = new LibraryBuilder("Test LIb", LibraryType.Manga)
            .WithSeries(series)
            .Build();

        _context.AppUser.Add(new AppUser()
        {
            UserName = "majora2007",
            Libraries = new List<Library>()
            {
                library
            }

        });
        await _context.SaveChangesAsync();

        var latestChapter = await _tachiyomiService.GetLatestChapter(1, 1);

        Assert.Null(latestChapter);
    }
    [Fact]
    public async Task MarkChaptersUntilAsRead_ShouldReturnMaxChapter_CompletelyRead()
    {
        await ResetDb();

        var series = new SeriesBuilder("Test")
            .WithVolume(new VolumeBuilder(API.Services.Tasks.Scanner.Parser.Parser.LooseLeafVolume)
                .WithChapter(new ChapterBuilder("95").WithPages(1).Build())
                .WithChapter(new ChapterBuilder("96").WithPages(1).Build())
                .Build())
            .WithVolume(new VolumeBuilder("1")
                .WithChapter(new ChapterBuilder("1").WithPages(1).Build())
                .Build())
            .WithVolume(new VolumeBuilder("2")
                .WithChapter(new ChapterBuilder("3").WithPages(1).Build())
                .WithChapter(new ChapterBuilder("4").WithPages(1).Build())
                .Build())
            .WithVolume(new VolumeBuilder("3")
                .WithChapter(new ChapterBuilder("31").WithPages(1).Build())
                .WithChapter(new ChapterBuilder("32").WithPages(1).Build())
                .Build())
            .WithPages(7)
            .Build();

        var library = new LibraryBuilder("Test LIb", LibraryType.Manga)
            .WithSeries(series)
            .Build();

        _context.AppUser.Add(new AppUser()
        {
            UserName = "majora2007",
            Libraries = new List<Library>()
            {
                library
            }

        });
        await _context.SaveChangesAsync();

        var user = await _unitOfWork.UserRepository.GetUserByUsernameAsync("majora2007", AppUserIncludes.Progress);
        await _readerService.MarkSeriesAsRead(user,1);

        await _context.SaveChangesAsync();


        var latestChapter = await _tachiyomiService.GetLatestChapter(1, 1);

        Assert.Equal("96", latestChapter.Number);
    }

    [Fact]
    public async Task MarkChaptersUntilAsRead_ShouldReturnHighestChapter_Progress()
    {
        await ResetDb();

        var series = new SeriesBuilder("Test")
            .WithVolume(new VolumeBuilder(API.Services.Tasks.Scanner.Parser.Parser.LooseLeafVolume)
                .WithChapter(new ChapterBuilder("95").WithPages(1).Build())
                .WithChapter(new ChapterBuilder("96").WithPages(1).Build())
                .Build())
            .WithVolume(new VolumeBuilder("1")
                .WithChapter(new ChapterBuilder("1").WithPages(1).Build())
                .Build())
            .WithVolume(new VolumeBuilder("2")
                .WithChapter(new ChapterBuilder("21").WithPages(1).Build())
                .WithChapter(new ChapterBuilder("23").WithPages(1).Build())
                .Build())
            .WithVolume(new VolumeBuilder("3")
                .WithChapter(new ChapterBuilder("31").WithPages(1).Build())
                .WithChapter(new ChapterBuilder("32").WithPages(1).Build())
                .Build())
            .WithPages(7)
            .Build();

        var library = new LibraryBuilder("Test LIb", LibraryType.Manga)
            .WithSeries(series)
            .Build();

        _context.AppUser.Add(new AppUser()
        {
            UserName = "majora2007",
            Libraries = new List<Library>()
            {
                library
            }

        });
        await _context.SaveChangesAsync();

        var user = await _unitOfWork.UserRepository.GetUserByUsernameAsync("majora2007", AppUserIncludes.Progress);
        await _tachiyomiService.MarkChaptersUntilAsRead(user,1,21);

        await _context.SaveChangesAsync();


        var latestChapter = await _tachiyomiService.GetLatestChapter(1, 1);

        Assert.Equal("21", latestChapter.Number);
    }
    [Fact]
    public async Task MarkChaptersUntilAsRead_ShouldReturnEncodedVolume_Progress()
    {
        await ResetDb();
        var series = new SeriesBuilder("Test")
            .WithVolume(new VolumeBuilder(API.Services.Tasks.Scanner.Parser.Parser.LooseLeafVolume)
                .WithChapter(new ChapterBuilder("95").WithPages(1).Build())
                .WithChapter(new ChapterBuilder("96").WithPages(1).Build())
                .Build())
            .WithVolume(new VolumeBuilder("1")
                .WithChapter(new ChapterBuilder("1").WithPages(1).Build())
                .Build())
            .WithVolume(new VolumeBuilder("2")
                .WithChapter(new ChapterBuilder("21").WithPages(1).Build())
                .WithChapter(new ChapterBuilder("23").WithPages(1).Build())
                .Build())
            .WithVolume(new VolumeBuilder("3")
                .WithChapter(new ChapterBuilder("31").WithPages(1).Build())
                .WithChapter(new ChapterBuilder("32").WithPages(1).Build())
                .Build())
            .WithPages(7)
            .Build();

        var library = new LibraryBuilder("Test LIb", LibraryType.Manga)
            .WithSeries(series)
            .Build();

        _context.AppUser.Add(new AppUser()
        {
            UserName = "majora2007",
            Libraries = new List<Library>()
            {
                library
            }

        });
        await _context.SaveChangesAsync();

        var user = await _unitOfWork.UserRepository.GetUserByUsernameAsync("majora2007", AppUserIncludes.Progress);

        await _tachiyomiService.MarkChaptersUntilAsRead(user,1,1/10_000F);

        await _context.SaveChangesAsync();


        var latestChapter = await _tachiyomiService.GetLatestChapter(1, 1);
        Assert.Equal("0.0001", latestChapter.Number);
    }

    #endregion

}
