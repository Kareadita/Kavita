using System.Collections.Generic;
using System.Data.Common;
using System.IO;
using System.IO.Abstractions.TestingHelpers;
using System.Linq;
using System.Threading.Tasks;
using API.Data;
using API.Data.Repositories;
using API.DTOs.Reader;
using API.Entities;
using API.Entities.Enums;
using API.Extensions;
using API.Helpers;
using API.Services;
using API.SignalR;
using AutoMapper;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.Extensions.Logging;
using NSubstitute;
using Xunit;

namespace API.Tests.Services;

public class BookmarkServiceTests
{
    private readonly IUnitOfWork _unitOfWork;
    private readonly DbConnection _connection;
    private readonly DataContext _context;

    private const string CacheDirectory = "C:/kavita/config/cache/";
    private const string CoverImageDirectory = "C:/kavita/config/covers/";
    private const string BackupDirectory = "C:/kavita/config/backups/";
    private const string BookmarkDirectory = "C:/kavita/config/bookmarks/";


    public BookmarkServiceTests()
    {
        var contextOptions = new DbContextOptionsBuilder()
            .UseSqlite(CreateInMemoryDatabase())
            .Options;
        _connection = RelationalOptionsExtension.Extract(contextOptions).Connection;

        _context = new DataContext(contextOptions);
        Task.Run(SeedDb).GetAwaiter().GetResult();

        var config = new MapperConfiguration(cfg => cfg.AddProfile<AutoMapperProfiles>());
        var mapper = config.CreateMapper();
        _unitOfWork = new UnitOfWork(_context, mapper, null);
    }

    private BookmarkService Create(IDirectoryService ds)
    {
        return new BookmarkService(Substitute.For<ILogger<BookmarkService>>(), _unitOfWork, ds,
            Substitute.For<IImageService>(), Substitute.For<IEventHub>());
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

        await Seed.SeedSettings(_context, new DirectoryService(Substitute.For<ILogger<DirectoryService>>(), filesystem));

        var setting = await _context.ServerSetting.Where(s => s.Key == ServerSettingKey.CacheDirectory).SingleAsync();
        setting.Value = CacheDirectory;

        setting = await _context.ServerSetting.Where(s => s.Key == ServerSettingKey.BackupDirectory).SingleAsync();
        setting.Value = BackupDirectory;

        setting = await _context.ServerSetting.Where(s => s.Key == ServerSettingKey.BookmarkDirectory).SingleAsync();
        setting.Value = BookmarkDirectory;

        _context.ServerSetting.Update(setting);

        _context.Library.Add(new Library()
        {
            Name = "Manga",
            Folders = new List<FolderPath>()
            {
                new FolderPath()
                {
                    Path = "C:/data/"
                }
            }
        });
        return await _context.SaveChangesAsync() > 0;
    }

    private async Task ResetDB()
    {
        _context.Series.RemoveRange(_context.Series.ToList());
        _context.Users.RemoveRange(_context.Users.ToList());
        _context.AppUserBookmark.RemoveRange(_context.AppUserBookmark.ToList());

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
        fileSystem.AddDirectory(BookmarkDirectory);
        fileSystem.AddDirectory("C:/data/");

        return fileSystem;
    }

    #endregion

    #region BookmarkPage

    [Fact]
    public async Task BookmarkPage_ShouldCopyTheFileAndUpdateDB()
    {
        var filesystem = CreateFileSystem();
        var file = $"{CacheDirectory}1/0001.jpg";
        filesystem.AddFile(file, new MockFileData("123"));

        // Delete all Series to reset state
        await ResetDB();

        _context.Series.Add(new Series()
        {
            Name = "Test",
            NormalizedName = "Test".ToNormalized(),
            Library = new Library() {
                Name = "Test LIb",
                Type = LibraryType.Manga,
            },
            Volumes = new List<Volume>()
            {
                new Volume()
                {
                    Name = "0",
                    Number = 0,
                    Chapters = new List<Chapter>()
                    {
                        new Chapter()
                        {
                            Number = "1",
                            Range = "1",
                        }
                    }
                }
            }
        });

        _context.AppUser.Add(new AppUser()
        {
            UserName = "Joe"
        });

        await _context.SaveChangesAsync();


        var ds = new DirectoryService(Substitute.For<ILogger<DirectoryService>>(), filesystem);
        var bookmarkService = Create(ds);
        var user = await _unitOfWork.UserRepository.GetUserByIdAsync(1, AppUserIncludes.Bookmarks);

        var result = await bookmarkService.BookmarkPage(user, new BookmarkDto()
        {
            ChapterId = 1,
            Page = 1,
            SeriesId = 1,
            VolumeId = 1
        }, file);


        Assert.True(result);
        Assert.Equal(1, ds.GetFiles(BookmarkDirectory, searchOption:SearchOption.AllDirectories).Count());
        Assert.NotNull(await _unitOfWork.UserRepository.GetBookmarkAsync(1));
    }

    [Fact]
    public async Task BookmarkPage_ShouldDeleteFileOnUnbookmark()
    {
        var filesystem = CreateFileSystem();
        filesystem.AddFile($"{CacheDirectory}1/0001.jpg", new MockFileData("123"));
        filesystem.AddFile($"{BookmarkDirectory}1/1/0001.jpg", new MockFileData("123"));

        // Delete all Series to reset state
        await ResetDB();

        _context.Series.Add(new Series()
        {
            Name = "Test",
            NormalizedName = "Test".ToNormalized(),
            Library = new Library() {
                Name = "Test LIb",
                Type = LibraryType.Manga,
            },
            Volumes = new List<Volume>()
            {
                new Volume()
                {
                    Name = "1",
                    Number = 1,
                    Chapters = new List<Chapter>()
                    {
                        new Chapter()
                        {
                            Number = "0",
                            Range = "0",
                        }
                    }
                }
            }
        });


        _context.AppUser.Add(new AppUser()
        {
            UserName = "Joe",
            Bookmarks = new List<AppUserBookmark>()
            {
                new AppUserBookmark()
                {
                    Page = 1,
                    ChapterId = 1,
                    FileName = $"1/1/0001.jpg",
                    SeriesId = 1,
                    VolumeId = 1
                }
            }
        });

        await _context.SaveChangesAsync();


        var ds = new DirectoryService(Substitute.For<ILogger<DirectoryService>>(), filesystem);
        var bookmarkService = Create(ds);
        var user = await _unitOfWork.UserRepository.GetUserByIdAsync(1, AppUserIncludes.Bookmarks);

        var result = await bookmarkService.RemoveBookmarkPage(user, new BookmarkDto()
        {
            ChapterId = 1,
            Page = 1,
            SeriesId = 1,
            VolumeId = 1
        });


        Assert.True(result);
        Assert.Equal(0, ds.GetFiles(BookmarkDirectory, searchOption:SearchOption.AllDirectories).Count());
        Assert.Null(await _unitOfWork.UserRepository.GetBookmarkAsync(1));
    }

    #endregion

    #region DeleteBookmarkFiles

    [Fact]
    public async Task DeleteBookmarkFiles_ShouldDeleteOnlyPassedFiles()
    {
        var filesystem = CreateFileSystem();
        filesystem.AddFile($"{CacheDirectory}1/0001.jpg", new MockFileData("123"));
        filesystem.AddFile($"{BookmarkDirectory}1/1/1/0001.jpg", new MockFileData("123"));
        filesystem.AddFile($"{BookmarkDirectory}1/2/1/0002.jpg", new MockFileData("123"));
        filesystem.AddFile($"{BookmarkDirectory}1/2/1/0001.jpg", new MockFileData("123"));

        // Delete all Series to reset state
        await ResetDB();

        _context.Series.Add(new Series()
        {
            Name = "Test",
            NormalizedName = "Test".ToNormalized(),
            Library = new Library() {
                Name = "Test LIb",
                Type = LibraryType.Manga,
            },
            Volumes = new List<Volume>()
            {
                new Volume()
                {
                    Name = "1",
                    Number = 1,
                    Chapters = new List<Chapter>()
                    {
                        new Chapter()
                        {
                            Number = "1",
                            Range = "1",
                        }
                    }
                }
            }
        });


        _context.AppUser.Add(new AppUser()
        {
            UserName = "Joe",
            Bookmarks = new List<AppUserBookmark>()
            {
                new AppUserBookmark()
                {
                    Page = 1,
                    ChapterId = 1,
                    FileName = $"1/1/1/0001.jpg",
                    SeriesId = 1,
                    VolumeId = 1
                },
                new AppUserBookmark()
                {
                    Page = 2,
                    ChapterId = 1,
                    FileName = $"1/2/1/0002.jpg",
                    SeriesId = 2,
                    VolumeId = 1
                },
                new AppUserBookmark()
                {
                    Page = 1,
                    ChapterId = 2,
                    FileName = $"1/2/1/0001.jpg",
                    SeriesId = 2,
                    VolumeId = 1
                }
            }
        });

        await _context.SaveChangesAsync();


        var ds = new DirectoryService(Substitute.For<ILogger<DirectoryService>>(), filesystem);
        var bookmarkService = Create(ds);

        await bookmarkService.DeleteBookmarkFiles(new [] {new AppUserBookmark()
        {
            Page = 1,
            ChapterId = 1,
            FileName = $"1/1/1/0001.jpg",
            SeriesId = 1,
            VolumeId = 1
        }});


        Assert.Equal(2, ds.GetFiles(BookmarkDirectory, searchOption:SearchOption.AllDirectories).Count());
        Assert.False(ds.FileSystem.FileInfo.New(Path.Join(BookmarkDirectory, "1/1/1/0001.jpg")).Exists);
    }
    #endregion

    #region GetBookmarkFilesById

    [Fact]
    public async Task GetBookmarkFilesById_ShouldMatchActualFiles()
    {
        var filesystem = CreateFileSystem();
        filesystem.AddFile($"{CacheDirectory}1/0001.jpg", new MockFileData("123"));

        // Delete all Series to reset state
        await ResetDB();

        _context.Series.Add(new Series()
        {
            Name = "Test",
            NormalizedName = "Test".ToNormalized(),
            Library = new Library() {
                Name = "Test LIb",
                Type = LibraryType.Manga,
            },
            Volumes = new List<Volume>()
            {
                new Volume()
                {
                    Name = "1",
                    Number = 1,
                    Chapters = new List<Chapter>()
                    {
                        new Chapter()
                        {
                            Number = "1",
                            Range = "1",
                        }
                    }
                }
            }
        });

        _context.AppUser.Add(new AppUser()
        {
            UserName = "Joe"
        });

        await _context.SaveChangesAsync();


        var ds = new DirectoryService(Substitute.For<ILogger<DirectoryService>>(), filesystem);
        var bookmarkService = Create(ds);
        var user = await _unitOfWork.UserRepository.GetUserByIdAsync(1, AppUserIncludes.Bookmarks);

        await bookmarkService.BookmarkPage(user, new BookmarkDto()
        {
            ChapterId = 1,
            Page = 1,
            SeriesId = 1,
            VolumeId = 1
        }, $"{CacheDirectory}1/0001.jpg");

        var files = await bookmarkService.GetBookmarkFilesById(new[] {1});
        var actualFiles = ds.GetFiles(BookmarkDirectory, searchOption: SearchOption.AllDirectories);
        Assert.Equal(files.Select(API.Services.Tasks.Scanner.Parser.Parser.NormalizePath).ToList(), actualFiles.Select(API.Services.Tasks.Scanner.Parser.Parser.NormalizePath).ToList());
    }


    #endregion

    #region Misc

    [Fact]
    public async Task ShouldNotDeleteBookmark_OnChapterDeletion()
    {
        var filesystem = CreateFileSystem();
        filesystem.AddFile($"{CacheDirectory}1/0001.jpg", new MockFileData("123"));
        filesystem.AddFile($"{BookmarkDirectory}1/1/0001.jpg", new MockFileData("123"));

        // Delete all Series to reset state
        await ResetDB();

        _context.Series.Add(new Series()
        {
            Name = "Test",
            NormalizedName = "Test".ToNormalized(),
            Library = new Library() {
                Name = "Test LIb",
                Type = LibraryType.Manga,
            },
            Volumes = new List<Volume>()
            {
                new Volume()
                {
                    Name = "1",
                    Number = 1,
                    Chapters = new List<Chapter>()
                    {
                        new Chapter()
                        {
                            Number = "1",
                            Range = "1",
                        }
                    }
                }
            }
        });


        _context.AppUser.Add(new AppUser()
        {
            UserName = "Joe",
            Bookmarks = new List<AppUserBookmark>()
            {
                new AppUserBookmark()
                {
                    Page = 1,
                    ChapterId = 1,
                    FileName = $"1/1/0001.jpg",
                    SeriesId = 1,
                    VolumeId = 1
                }
            }
        });

        await _context.SaveChangesAsync();


        var ds = new DirectoryService(Substitute.For<ILogger<DirectoryService>>(), filesystem);

        var vol = await _unitOfWork.VolumeRepository.GetVolumeAsync(1);
        vol.Chapters = new List<Chapter>();
        _unitOfWork.VolumeRepository.Update(vol);
        await _unitOfWork.CommitAsync();


        Assert.Equal(1, ds.GetFiles(BookmarkDirectory, searchOption:SearchOption.AllDirectories).Count());
        Assert.NotNull(await _unitOfWork.UserRepository.GetBookmarkAsync(1));
    }


    [Fact]
    public async Task ShouldNotDeleteBookmark_OnVolumeDeletion()
    {
        var filesystem = CreateFileSystem();
        filesystem.AddFile($"{CacheDirectory}1/0001.jpg", new MockFileData("123"));
        filesystem.AddFile($"{BookmarkDirectory}1/1/0001.jpg", new MockFileData("123"));

        // Delete all Series to reset state
        await ResetDB();
        var series = new Series()
        {
            Name = "Test",
            NormalizedName = "Test".ToNormalized(),
            Library = new Library()
            {
                Name = "Test LIb",
                Type = LibraryType.Manga,
            },
            Volumes = new List<Volume>()
            {
                new Volume()
                {
                    Name = "1",
                    Number = 1,
                    Chapters = new List<Chapter>()
                    {
                        new Chapter()
                        {
                            Number = "1",
                            Range = "1",
                        }
                    }
                }
            }
        };

        _context.Series.Add(series);


        _context.AppUser.Add(new AppUser()
        {
            UserName = "Joe",
            Bookmarks = new List<AppUserBookmark>()
            {
                new AppUserBookmark()
                {
                    Page = 1,
                    ChapterId = 1,
                    FileName = $"1/1/0001.jpg",
                    SeriesId = 1,
                    VolumeId = 1
                }
            }
        });

        await _context.SaveChangesAsync();

        var user = await _unitOfWork.UserRepository.GetUserByIdAsync(1, AppUserIncludes.Bookmarks);
        Assert.NotEmpty(user.Bookmarks);

        series.Volumes = new List<Volume>();
        _unitOfWork.SeriesRepository.Update(series);
        await _unitOfWork.CommitAsync();


        var ds = new DirectoryService(Substitute.For<ILogger<DirectoryService>>(), filesystem);
        Assert.Single(ds.GetFiles(BookmarkDirectory, searchOption:SearchOption.AllDirectories));
        Assert.NotNull(await _unitOfWork.UserRepository.GetBookmarkAsync(1));
    }

    #endregion
}
