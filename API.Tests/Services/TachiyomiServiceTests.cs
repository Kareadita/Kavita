namespace API.Tests.Services;
using System.Collections.Generic;
using System.Data.Common;
using System.IO.Abstractions.TestingHelpers;
using System.Linq;
using System.Threading.Tasks;
using API.Data;
using API.Data.Repositories;
using API.Entities;
using API.Entities.Enums;
using API.Helpers;
using API.Services;
using API.SignalR;
using API.Tests.Helpers;
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

        _context.Library.Add(new Library()
        {
            Name = "Manga", Folders = new List<FolderPath>() {new FolderPath() {Path = "C:/data/"}}
        });
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

        var series = new Series()
        {
            Name = "Test",
            Volumes = new List<Volume>()
            {
                EntityFactory.CreateVolume("0", new List<Chapter>()
                {
                    EntityFactory.CreateChapter("95", false, new List<MangaFile>(), 1),
                    EntityFactory.CreateChapter("96", false, new List<MangaFile>(), 1),
                }),
                EntityFactory.CreateVolume("1", new List<Chapter>()
                {
                    EntityFactory.CreateChapter("1", true, new List<MangaFile>(), 1),
                }),
                EntityFactory.CreateVolume("2", new List<Chapter>()
                {
                    EntityFactory.CreateChapter("3", false, new List<MangaFile>(), 1),
                    EntityFactory.CreateChapter("4", false, new List<MangaFile>(), 1),
                }),
                EntityFactory.CreateVolume("3", new List<Chapter>()
                {
                    EntityFactory.CreateChapter("31", false, new List<MangaFile>(), 1),
                    EntityFactory.CreateChapter("32", false, new List<MangaFile>(), 1),
                }),
            }
        };
        series.Pages = 7;
        var library = new Library()
        {
            Name = "Test LIb",
            Type = LibraryType.Manga,
            Series = new List<Series>() { series }
        };

        _context.AppUser.Add(new AppUser()
        {
            UserName = "majora2007",
            Libraries = new List<Library>()
            {
                library
            }

        });
        await _context.SaveChangesAsync();

        var readerService = new ReaderService(_unitOfWork, Substitute.For<ILogger<ReaderService>>(), Substitute.For<IEventHub>());
        var tachiyomiService = new TachiyomiService(_unitOfWork, _mapper, readerService);

        var latestChapter = await tachiyomiService.GetLatestChapter(1, 1);

        Assert.Null(latestChapter);
    }

    [Fact]
    public async Task GetLatestChapter_ShouldReturnMaxChapter_CompletelyRead()
    {
        await ResetDb();

        var series = new Series()
        {
            Name = "Test",
            Volumes = new List<Volume>()
            {
                EntityFactory.CreateVolume("0", new List<Chapter>()
                {
                    EntityFactory.CreateChapter("95", false, new List<MangaFile>(), 1),
                    EntityFactory.CreateChapter("96", false, new List<MangaFile>(), 1),
                }),
                EntityFactory.CreateVolume("1", new List<Chapter>()
                {
                    EntityFactory.CreateChapter("1", true, new List<MangaFile>(), 1),
                }),
                EntityFactory.CreateVolume("2", new List<Chapter>()
                {
                    EntityFactory.CreateChapter("3", false, new List<MangaFile>(), 1),
                    EntityFactory.CreateChapter("4", false, new List<MangaFile>(), 1),
                }),
                EntityFactory.CreateVolume("3", new List<Chapter>()
                {
                    EntityFactory.CreateChapter("31", false, new List<MangaFile>(), 1),
                    EntityFactory.CreateChapter("32", false, new List<MangaFile>(), 1),
                }),
            }
        };
        series.Pages = 7;
        var library = new Library()
        {
            Name = "Test LIb",
            Type = LibraryType.Manga,
            Series = new List<Series>() { series }
        };

        _context.AppUser.Add(new AppUser()
        {
            UserName = "majora2007",
            Libraries = new List<Library>()
            {
                library
            }

        });
        await _context.SaveChangesAsync();

        var readerService = new ReaderService(_unitOfWork, Substitute.For<ILogger<ReaderService>>(), Substitute.For<IEventHub>());
        var tachiyomiService = new TachiyomiService(_unitOfWork, _mapper, readerService);

        var user = await _unitOfWork.UserRepository.GetUserByUsernameAsync("majora2007", AppUserIncludes.Progress);
        await readerService.MarkSeriesAsRead(user,1);

        await _context.SaveChangesAsync();


        var latestChapter = await tachiyomiService.GetLatestChapter(1, 1);

        Assert.Equal("96",latestChapter.Number);
    }

    [Fact]
    public async Task GetLatestChapter_ShouldReturnHighestChapter_Progress()
    {
        await ResetDb();

        var series = new Series()
        {
            Name = "Test",
            Volumes = new List<Volume>()
            {
                EntityFactory.CreateVolume("0", new List<Chapter>()
                {
                    EntityFactory.CreateChapter("95", false, new List<MangaFile>(), 1),
                    EntityFactory.CreateChapter("96", false, new List<MangaFile>(), 1),
                }),
                EntityFactory.CreateVolume("1", new List<Chapter>()
                {
                    EntityFactory.CreateChapter("1", false, new List<MangaFile>(), 1),
                }),
                EntityFactory.CreateVolume("2", new List<Chapter>()
                {
                    EntityFactory.CreateChapter("21", false, new List<MangaFile>(), 1),
                    EntityFactory.CreateChapter("23", false, new List<MangaFile>(), 1),
                }),
                EntityFactory.CreateVolume("3", new List<Chapter>()
                {
                    EntityFactory.CreateChapter("31", false, new List<MangaFile>(), 1),
                    EntityFactory.CreateChapter("32", false, new List<MangaFile>(), 1),
                }),
            }
        };
        series.Pages = 7;
        var library = new Library()
        {
            Name = "Test LIb",
            Type = LibraryType.Manga,
            Series = new List<Series>() { series }
        };

        _context.AppUser.Add(new AppUser()
        {
            UserName = "majora2007",
            Libraries = new List<Library>()
            {
                library
            }

        });
        await _context.SaveChangesAsync();

        var readerService = new ReaderService(_unitOfWork, Substitute.For<ILogger<ReaderService>>(), Substitute.For<IEventHub>());
        var tachiyomiService = new TachiyomiService(_unitOfWork, _mapper, readerService);

        var user = await _unitOfWork.UserRepository.GetUserByUsernameAsync("majora2007", AppUserIncludes.Progress);
        await tachiyomiService.MarkChaptersUntilAsRead(user,1,21);

        await _context.SaveChangesAsync();


        var latestChapter = await tachiyomiService.GetLatestChapter(1, 1);

        Assert.Equal("21",latestChapter.Number);
    }
    [Fact]
    public async Task GetLatestChapter_ShouldReturnEncodedVolume_Progress()
    {
        await ResetDb();

        var series = new Series()
        {
            Name = "Test",
            Volumes = new List<Volume>()
            {
                EntityFactory.CreateVolume("0", new List<Chapter>()
                {
                    EntityFactory.CreateChapter("95", false, new List<MangaFile>(), 1),
                    EntityFactory.CreateChapter("96", false, new List<MangaFile>(), 1),
                }),
                EntityFactory.CreateVolume("1", new List<Chapter>()
                {
                    EntityFactory.CreateChapter("1", true, new List<MangaFile>(), 1),
                }),
                EntityFactory.CreateVolume("2", new List<Chapter>()
                {
                    EntityFactory.CreateChapter("21", false, new List<MangaFile>(), 1),
                    EntityFactory.CreateChapter("23", false, new List<MangaFile>(), 1),
                }),
                EntityFactory.CreateVolume("3", new List<Chapter>()
                {
                    EntityFactory.CreateChapter("31", false, new List<MangaFile>(), 1),
                    EntityFactory.CreateChapter("32", false, new List<MangaFile>(), 1),
                }),
            }
        };
        series.Pages = 7;
        var library = new Library()
        {
            Name = "Test LIb",
            Type = LibraryType.Manga,
            Series = new List<Series>() { series }
        };

        _context.AppUser.Add(new AppUser()
        {
            UserName = "majora2007",
            Libraries = new List<Library>()
            {
                library
            }

        });
        await _context.SaveChangesAsync();

        var readerService = new ReaderService(_unitOfWork, Substitute.For<ILogger<ReaderService>>(), Substitute.For<IEventHub>());
        var tachiyomiService = new TachiyomiService(_unitOfWork, _mapper, readerService);

        var user = await _unitOfWork.UserRepository.GetUserByUsernameAsync("majora2007", AppUserIncludes.Progress);

        await tachiyomiService.MarkChaptersUntilAsRead(user,1,1/10000F);

        await _context.SaveChangesAsync();


        var latestChapter = await tachiyomiService.GetLatestChapter(1, 1);
        Assert.Equal("0.0001",latestChapter.Number);
    }

    [Fact]
    public async Task GetLatestChapter_ShouldReturnEncodedVolume_Progress2()
    {
        await ResetDb();

        var series = new Series()
        {
            Name = "Test",
            Volumes = new List<Volume>()
            {
                EntityFactory.CreateVolume("1", new List<Chapter>()
                {
                    EntityFactory.CreateChapter("0", false, new List<MangaFile>(), 199),
                }),
                EntityFactory.CreateVolume("2", new List<Chapter>()
                {
                    EntityFactory.CreateChapter("0", false, new List<MangaFile>(), 192),
                }),
                EntityFactory.CreateVolume("3", new List<Chapter>()
                {
                    EntityFactory.CreateChapter("0", false, new List<MangaFile>(), 255),
                }),
            }
        };
        series.Pages = 7;
        var library = new Library()
        {
            Name = "Test LIb",
            Type = LibraryType.Manga,
            Series = new List<Series>() { series }
        };

        _context.AppUser.Add(new AppUser()
        {
            UserName = "majora2007",
            Libraries = new List<Library>()
            {
                library
            }

        });
        await _context.SaveChangesAsync();

        var readerService = new ReaderService(_unitOfWork, Substitute.For<ILogger<ReaderService>>(), Substitute.For<IEventHub>());
        var tachiyomiService = new TachiyomiService(_unitOfWork, _mapper, readerService);

        var user = await _unitOfWork.UserRepository.GetUserByUsernameAsync("majora2007", AppUserIncludes.Progress);

        await readerService.MarkSeriesAsRead(user, 1);

        await _context.SaveChangesAsync();


        var latestChapter = await tachiyomiService.GetLatestChapter(1, 1);
        Assert.Equal("0.0003",latestChapter.Number);
    }


    [Fact]
    public async Task GetLatestChapter_ShouldReturnEncodedYearlyVolume_Progress()
    {
        await ResetDb();

        var series = new Series()
        {
            Name = "Test",
            Volumes = new List<Volume>()
            {
                EntityFactory.CreateVolume("0", new List<Chapter>()
                {
                    EntityFactory.CreateChapter("95", false, new List<MangaFile>(), 1),
                    EntityFactory.CreateChapter("96", false, new List<MangaFile>(), 1),
                }),
                EntityFactory.CreateVolume("1997", new List<Chapter>()
                {
                    EntityFactory.CreateChapter("1", false, new List<MangaFile>(), 1),
                }),
                EntityFactory.CreateVolume("2002", new List<Chapter>()
                {
                    EntityFactory.CreateChapter("2", false, new List<MangaFile>(), 1),
                }),
                EntityFactory.CreateVolume("2005", new List<Chapter>()
                {
                    EntityFactory.CreateChapter("3", false, new List<MangaFile>(), 1),
                }),
            }
        };
        series.Pages = 7;
        var library = new Library()
        {
            Name = "Test LIb",
            Type = LibraryType.Comic,
            Series = new List<Series>() { series }
        };

        _context.AppUser.Add(new AppUser()
        {
            UserName = "majora2007",
            Libraries = new List<Library>()
            {
                library
            }

        });
        await _context.SaveChangesAsync();

        var readerService = new ReaderService(_unitOfWork, Substitute.For<ILogger<ReaderService>>(), Substitute.For<IEventHub>());
        var tachiyomiService = new TachiyomiService(_unitOfWork, _mapper, readerService);

        var user = await _unitOfWork.UserRepository.GetUserByUsernameAsync("majora2007", AppUserIncludes.Progress);

        await tachiyomiService.MarkChaptersUntilAsRead(user,1,2002/10000F);

        await _context.SaveChangesAsync();


        var latestChapter = await tachiyomiService.GetLatestChapter(1, 1);
        Assert.Equal("0.2002",latestChapter.Number);
    }

    #endregion


    #region MarkChaptersUntilAsRead

    [Fact]
    public async Task MarkChaptersUntilAsRead_ShouldReturnChapter_NoProgress()
    {
        await ResetDb();

        var series = new Series()
        {
            Name = "Test",
            Volumes = new List<Volume>()
            {
                EntityFactory.CreateVolume("0", new List<Chapter>()
                {
                    EntityFactory.CreateChapter("95", false, new List<MangaFile>(), 1),
                    EntityFactory.CreateChapter("96", false, new List<MangaFile>(), 1),
                }),
                EntityFactory.CreateVolume("1", new List<Chapter>()
                {
                    EntityFactory.CreateChapter("1", true, new List<MangaFile>(), 1),
                }),
                EntityFactory.CreateVolume("2", new List<Chapter>()
                {
                    EntityFactory.CreateChapter("3", false, new List<MangaFile>(), 1),
                    EntityFactory.CreateChapter("4", false, new List<MangaFile>(), 1),
                }),
                EntityFactory.CreateVolume("3", new List<Chapter>()
                {
                    EntityFactory.CreateChapter("31", false, new List<MangaFile>(), 1),
                    EntityFactory.CreateChapter("32", false, new List<MangaFile>(), 1),
                }),
            }
        };
        series.Pages = 7;
        var library = new Library()
        {
            Name = "Test LIb",
            Type = LibraryType.Manga,
            Series = new List<Series>() { series }
        };

        _context.AppUser.Add(new AppUser()
        {
            UserName = "majora2007",
            Libraries = new List<Library>()
            {
                library
            }

        });
        await _context.SaveChangesAsync();

        var readerService = new ReaderService(_unitOfWork, Substitute.For<ILogger<ReaderService>>(), Substitute.For<IEventHub>());
        var tachiyomiService = new TachiyomiService(_unitOfWork, _mapper, readerService);

        var latestChapter = await tachiyomiService.GetLatestChapter(1, 1);

        Assert.Null(latestChapter);
    }
    [Fact]
    public async Task MarkChaptersUntilAsRead_ShouldReturnMaxChapter_CompletelyRead()
    {
        await ResetDb();

        var series = new Series()
        {
            Name = "Test",
            Volumes = new List<Volume>()
            {
                EntityFactory.CreateVolume("0", new List<Chapter>()
                {
                    EntityFactory.CreateChapter("95", false, new List<MangaFile>(), 1),
                    EntityFactory.CreateChapter("96", false, new List<MangaFile>(), 1),
                }),
                EntityFactory.CreateVolume("1", new List<Chapter>()
                {
                    EntityFactory.CreateChapter("1", true, new List<MangaFile>(), 1),
                }),
                EntityFactory.CreateVolume("2", new List<Chapter>()
                {
                    EntityFactory.CreateChapter("3", false, new List<MangaFile>(), 1),
                    EntityFactory.CreateChapter("4", false, new List<MangaFile>(), 1),
                }),
                EntityFactory.CreateVolume("3", new List<Chapter>()
                {
                    EntityFactory.CreateChapter("31", false, new List<MangaFile>(), 1),
                    EntityFactory.CreateChapter("32", false, new List<MangaFile>(), 1),
                }),
            }
        };
        series.Pages = 7;
        var library = new Library()
        {
            Name = "Test LIb",
            Type = LibraryType.Manga,
            Series = new List<Series>() { series }
        };

        _context.AppUser.Add(new AppUser()
        {
            UserName = "majora2007",
            Libraries = new List<Library>()
            {
                library
            }

        });
        await _context.SaveChangesAsync();

        var readerService = new ReaderService(_unitOfWork, Substitute.For<ILogger<ReaderService>>(), Substitute.For<IEventHub>());
        var tachiyomiService = new TachiyomiService(_unitOfWork, _mapper, readerService);

        var user = await _unitOfWork.UserRepository.GetUserByUsernameAsync("majora2007", AppUserIncludes.Progress);
        await readerService.MarkSeriesAsRead(user,1);

        await _context.SaveChangesAsync();


        var latestChapter = await tachiyomiService.GetLatestChapter(1, 1);

        Assert.Equal("96",latestChapter.Number);
    }

    [Fact]
    public async Task MarkChaptersUntilAsRead_ShouldReturnHighestChapter_Progress()
    {
        await ResetDb();

        var series = new Series()
        {
            Name = "Test",
            Volumes = new List<Volume>()
            {
                EntityFactory.CreateVolume("0", new List<Chapter>()
                {
                    EntityFactory.CreateChapter("95", false, new List<MangaFile>(), 1),
                    EntityFactory.CreateChapter("96", false, new List<MangaFile>(), 1),
                }),
                EntityFactory.CreateVolume("1", new List<Chapter>()
                {
                    EntityFactory.CreateChapter("1", false, new List<MangaFile>(), 1),
                }),
                EntityFactory.CreateVolume("2", new List<Chapter>()
                {
                    EntityFactory.CreateChapter("21", false, new List<MangaFile>(), 1),
                    EntityFactory.CreateChapter("23", false, new List<MangaFile>(), 1),
                }),
                EntityFactory.CreateVolume("3", new List<Chapter>()
                {
                    EntityFactory.CreateChapter("31", false, new List<MangaFile>(), 1),
                    EntityFactory.CreateChapter("32", false, new List<MangaFile>(), 1),
                }),
            }
        };
        series.Pages = 7;
        var library = new Library()
        {
            Name = "Test LIb",
            Type = LibraryType.Manga,
            Series = new List<Series>() { series }
        };

        _context.AppUser.Add(new AppUser()
        {
            UserName = "majora2007",
            Libraries = new List<Library>()
            {
                library
            }

        });
        await _context.SaveChangesAsync();

        var readerService = new ReaderService(_unitOfWork, Substitute.For<ILogger<ReaderService>>(), Substitute.For<IEventHub>());
        var tachiyomiService = new TachiyomiService(_unitOfWork, _mapper, readerService);

        var user = await _unitOfWork.UserRepository.GetUserByUsernameAsync("majora2007", AppUserIncludes.Progress);
        await tachiyomiService.MarkChaptersUntilAsRead(user,1,21);

        await _context.SaveChangesAsync();


        var latestChapter = await tachiyomiService.GetLatestChapter(1, 1);

        Assert.Equal("21",latestChapter.Number);
    }
    [Fact]
    public async Task MarkChaptersUntilAsRead_ShouldReturnEncodedVolume_Progress()
    {
        await ResetDb();

        var series = new Series()
        {
            Name = "Test",
            Volumes = new List<Volume>()
            {
                EntityFactory.CreateVolume("0", new List<Chapter>()
                {
                    EntityFactory.CreateChapter("95", false, new List<MangaFile>(), 1),
                    EntityFactory.CreateChapter("96", false, new List<MangaFile>(), 1),
                }),
                EntityFactory.CreateVolume("1", new List<Chapter>()
                {
                    EntityFactory.CreateChapter("1", true, new List<MangaFile>(), 1),
                }),
                EntityFactory.CreateVolume("2", new List<Chapter>()
                {
                    EntityFactory.CreateChapter("21", false, new List<MangaFile>(), 1),
                    EntityFactory.CreateChapter("23", false, new List<MangaFile>(), 1),
                }),
                EntityFactory.CreateVolume("3", new List<Chapter>()
                {
                    EntityFactory.CreateChapter("31", false, new List<MangaFile>(), 1),
                    EntityFactory.CreateChapter("32", false, new List<MangaFile>(), 1),
                }),
            }
        };
        series.Pages = 7;
        var library = new Library()
        {
            Name = "Test LIb",
            Type = LibraryType.Manga,
            Series = new List<Series>() { series }
        };

        _context.AppUser.Add(new AppUser()
        {
            UserName = "majora2007",
            Libraries = new List<Library>()
            {
                library
            }

        });
        await _context.SaveChangesAsync();

        var readerService = new ReaderService(_unitOfWork, Substitute.For<ILogger<ReaderService>>(), Substitute.For<IEventHub>());
        var tachiyomiService = new TachiyomiService(_unitOfWork, _mapper, readerService);

        var user = await _unitOfWork.UserRepository.GetUserByUsernameAsync("majora2007", AppUserIncludes.Progress);

        await tachiyomiService.MarkChaptersUntilAsRead(user,1,1/10000F);

        await _context.SaveChangesAsync();


        var latestChapter = await tachiyomiService.GetLatestChapter(1, 1);
        Assert.Equal("0.0001",latestChapter.Number);
    }

    #endregion

}
