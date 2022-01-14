using System.Collections.Generic;
using System.Data.Common;
using System.IO;
using System.IO.Abstractions.TestingHelpers;
using System.Linq;
using System.Threading.Tasks;
using API.Data;
using API.Data.Repositories;
using API.DTOs;
using API.Entities;
using API.Entities.Enums;
using API.Services;
using API.SignalR;
using AutoMapper;
using Microsoft.AspNetCore.SignalR;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.Extensions.Logging;
using NSubstitute;
using Xunit;

namespace API.Tests.Services;

public class ReaderServiceTests
{
    private readonly ILogger<CacheService> _logger = Substitute.For<ILogger<CacheService>>();
    private readonly IUnitOfWork _unitOfWork;
    private readonly IHubContext<MessageHub> _messageHub = Substitute.For<IHubContext<MessageHub>>();

    private readonly DbConnection _connection;
    private readonly DataContext _context;

    private const string CacheDirectory = "C:/kavita/config/cache/";
    private const string CoverImageDirectory = "C:/kavita/config/covers/";
    private const string BackupDirectory = "C:/kavita/config/backups/";
    private const string DataDirectory = "C:/data/";

    public ReaderServiceTests()
    {
        var contextOptions = new DbContextOptionsBuilder().UseSqlite(CreateInMemoryDatabase()).Options;
        _connection = RelationalOptionsExtension.Extract(contextOptions).Connection;

        _context = new DataContext(contextOptions);
        Task.Run(SeedDb).GetAwaiter().GetResult();

        _unitOfWork = new UnitOfWork(_context, Substitute.For<IMapper>(), null);
    }

    #region Setup

    private static DbConnection CreateInMemoryDatabase()
    {
        var connection = new SqliteConnection("Filename=:memory:");

        connection.Open();

        return connection;
    }

    public void Dispose() => _connection.Dispose();

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

    private async Task ResetDB()
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
        await ResetDB();

        _context.Series.Add(new Series()
        {
            Name = "Test",
            Library = new Library() {
                Name = "Test LIb",
                Type = LibraryType.Manga,
            },
            Volumes = new List<Volume>()
            {
                new Volume()
                {
                    Chapters = new List<Chapter>()
                    {
                        new Chapter()
                        {
                            Pages = 1
                        }
                    }
                }
            }
        });

        await _context.SaveChangesAsync();

        var fileSystem = new MockFileSystem();
        var ds = new DirectoryService(Substitute.For<ILogger<DirectoryService>>(), fileSystem);
        var cs = new CacheService(_logger, _unitOfWork, ds, new MockReadingItemServiceForCacheService(ds));
        var readerService = new ReaderService(_unitOfWork, Substitute.For<ILogger<ReaderService>>(), ds, cs);

        Assert.Equal(0, await readerService.CapPageToChapter(1, -1));
        Assert.Equal(1, await readerService.CapPageToChapter(1, 10));
    }

    #endregion

    #region SaveReadingProgress

    [Fact]
    public async Task SaveReadingProgress_ShouldCreateNewEntity()
    {
        await ResetDB();

        _context.Series.Add(new Series()
        {
            Name = "Test",
            Library = new Library() {
                Name = "Test LIb",
                Type = LibraryType.Manga,
            },
            Volumes = new List<Volume>()
            {
                new Volume()
                {
                    Chapters = new List<Chapter>()
                    {
                        new Chapter()
                        {
                            Pages = 1
                        }
                    }
                }
            }
        });

        _context.AppUser.Add(new AppUser()
        {
            UserName = "majora2007"
        });

        await _context.SaveChangesAsync();

        var fileSystem = new MockFileSystem();
        var ds = new DirectoryService(Substitute.For<ILogger<DirectoryService>>(), fileSystem);
        var cs = new CacheService(_logger, _unitOfWork, ds, new MockReadingItemServiceForCacheService(ds));
        var readerService = new ReaderService(_unitOfWork, Substitute.For<ILogger<ReaderService>>(), ds, cs);

        var successful = await readerService.SaveReadingProgress(new ProgressDto()
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
        await ResetDB();

        _context.Series.Add(new Series()
        {
            Name = "Test",
            Library = new Library() {
                Name = "Test LIb",
                Type = LibraryType.Manga,
            },
            Volumes = new List<Volume>()
            {
                new Volume()
                {
                    Chapters = new List<Chapter>()
                    {
                        new Chapter()
                        {
                            Pages = 1
                        }
                    }
                }
            }
        });

        _context.AppUser.Add(new AppUser()
        {
            UserName = "majora2007"
        });

        await _context.SaveChangesAsync();

        var fileSystem = new MockFileSystem();
        var ds = new DirectoryService(Substitute.For<ILogger<DirectoryService>>(), fileSystem);
        var cs = new CacheService(_logger, _unitOfWork, ds, new MockReadingItemServiceForCacheService(ds));
        var readerService = new ReaderService(_unitOfWork, Substitute.For<ILogger<ReaderService>>(), ds, cs);

        var successful = await readerService.SaveReadingProgress(new ProgressDto()
        {
            ChapterId = 1,
            PageNum = 1,
            SeriesId = 1,
            VolumeId = 1,
            BookScrollId = null
        }, 1);

        Assert.True(successful);
        Assert.NotNull(await _unitOfWork.AppUserProgressRepository.GetUserProgressAsync(1, 1));

        Assert.True(await readerService.SaveReadingProgress(new ProgressDto()
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
        await ResetDB();

        _context.Series.Add(new Series()
        {
            Name = "Test",
            Library = new Library() {
                Name = "Test LIb",
                Type = LibraryType.Manga,
            },
            Volumes = new List<Volume>()
            {
                new Volume()
                {
                    Chapters = new List<Chapter>()
                    {
                        new Chapter()
                        {
                            Pages = 1
                        },
                        new Chapter()
                        {
                            Pages = 2
                        }
                    }
                }
            }
        });

        _context.AppUser.Add(new AppUser()
        {
            UserName = "majora2007"
        });

        await _context.SaveChangesAsync();

        var fileSystem = new MockFileSystem();
        var ds = new DirectoryService(Substitute.For<ILogger<DirectoryService>>(), fileSystem);
        var cs = new CacheService(_logger, _unitOfWork, ds, new MockReadingItemServiceForCacheService(ds));
        var readerService = new ReaderService(_unitOfWork, Substitute.For<ILogger<ReaderService>>(), ds, cs);

        var volumes = await _unitOfWork.VolumeRepository.GetVolumes(1);
        readerService.MarkChaptersAsRead(await _unitOfWork.UserRepository.GetUserByIdAsync(1, AppUserIncludes.Progress), 1, volumes.First().Chapters);
        await _context.SaveChangesAsync();

        Assert.Equal(2, (await _unitOfWork.UserRepository.GetUserByIdAsync(1, AppUserIncludes.Progress)).Progresses.Count);
    }
    #endregion

    #region MarkChapterAsUnread

    [Fact]
    public async Task MarkChapterAsUnreadTest()
    {
        await ResetDB();

        _context.Series.Add(new Series()
        {
            Name = "Test",
            Library = new Library() {
                Name = "Test LIb",
                Type = LibraryType.Manga,
            },
            Volumes = new List<Volume>()
            {
                new Volume()
                {
                    Chapters = new List<Chapter>()
                    {
                        new Chapter()
                        {
                            Pages = 1
                        },
                        new Chapter()
                        {
                            Pages = 2
                        }
                    }
                }
            }
        });

        _context.AppUser.Add(new AppUser()
        {
            UserName = "majora2007"
        });

        await _context.SaveChangesAsync();

        var fileSystem = new MockFileSystem();
        var ds = new DirectoryService(Substitute.For<ILogger<DirectoryService>>(), fileSystem);
        var cs = new CacheService(_logger, _unitOfWork, ds, new MockReadingItemServiceForCacheService(ds));
        var readerService = new ReaderService(_unitOfWork, Substitute.For<ILogger<ReaderService>>(), ds, cs);

        var volumes = (await _unitOfWork.VolumeRepository.GetVolumes(1)).ToList();
        readerService.MarkChaptersAsRead(await _unitOfWork.UserRepository.GetUserByIdAsync(1, AppUserIncludes.Progress), 1, volumes.First().Chapters);

        await _context.SaveChangesAsync();
        Assert.Equal(2, (await _unitOfWork.UserRepository.GetUserByIdAsync(1, AppUserIncludes.Progress)).Progresses.Count);

        readerService.MarkChaptersAsUnread(await _unitOfWork.UserRepository.GetUserByIdAsync(1, AppUserIncludes.Progress), 1, volumes.First().Chapters);
        await _context.SaveChangesAsync();

        var progresses = (await _unitOfWork.UserRepository.GetUserByIdAsync(1, AppUserIncludes.Progress)).Progresses;
        Assert.Equal(0, progresses.Max(p => p.PagesRead));
        Assert.Equal(2, progresses.Count);
    }

    #endregion

    // #region GetNumberOfPages
    //
    // [Fact]
    // public void GetNumberOfPages_EPUB()
    // {
    // const string testDirectory = "/manga/";
    // var fileSystem = new MockFileSystem();
    //
    // var actualFile = Path.Join(Path.Join(Directory.GetCurrentDirectory(), "../../../Services/Test Data/BookService/EPUB"), "The Golden Harpoon; Or, Lost Among the Floes A Story of the Whaling Grounds.epub")
    // fileSystem.File.WriteAllBytes("${testDirectory}test.epub", File.ReadAllBytes(actualFile));
    //
    // fileSystem.AddDirectory(CacheDirectory);
    //
    // var ds = new DirectoryService(Substitute.For<ILogger<DirectoryService>>(), fileSystem);
    // var cs = new CacheService(_logger, _unitOfWork, ds, new MockReadingItemServiceForCacheService(ds));
    // var readerService = new ReaderService(_unitOfWork, Substitute.For<ILogger<ReaderService>>(), ds, cs);
    //
    //
    // }
    //
    //
    // #endregion

}
