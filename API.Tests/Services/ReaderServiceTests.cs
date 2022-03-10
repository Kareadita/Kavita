using System;
using System.Collections.Generic;
using System.Data.Common;
using System.IO.Abstractions.TestingHelpers;
using System.Linq;
using System.Threading.Tasks;
using API.Data;
using API.Data.Repositories;
using API.DTOs;
using API.Entities;
using API.Entities.Enums;
using API.Helpers;
using API.Services;
using API.Tests.Helpers;
using AutoMapper;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.Extensions.Logging;
using NSubstitute;
using Xunit;

namespace API.Tests.Services;

public class ReaderServiceTests
{

    private readonly IUnitOfWork _unitOfWork;

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

        var config = new MapperConfiguration(cfg => cfg.AddProfile<AutoMapperProfiles>());
        var mapper = config.CreateMapper();
        _unitOfWork = new UnitOfWork(_context, mapper, null);
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

        var readerService = new ReaderService(_unitOfWork, Substitute.For<ILogger<ReaderService>>());

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

        var readerService = new ReaderService(_unitOfWork, Substitute.For<ILogger<ReaderService>>());

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

        var readerService = new ReaderService(_unitOfWork, Substitute.For<ILogger<ReaderService>>());

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

        var readerService = new ReaderService(_unitOfWork, Substitute.For<ILogger<ReaderService>>());

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

        var readerService = new ReaderService(_unitOfWork, Substitute.For<ILogger<ReaderService>>());

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

    #region GetNextChapterIdAsync

    [Fact]
    public async Task GetNextChapterIdAsync_ShouldGetNextVolume()
    {
        // V1 -> V2
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
                EntityFactory.CreateVolume("1", new List<Chapter>()
                {
                    EntityFactory.CreateChapter("1", false, new List<MangaFile>()),
                    EntityFactory.CreateChapter("2", false, new List<MangaFile>()),
                }),
                EntityFactory.CreateVolume("2", new List<Chapter>()
                {
                    EntityFactory.CreateChapter("21", false, new List<MangaFile>()),
                    EntityFactory.CreateChapter("22", false, new List<MangaFile>()),
                }),
                EntityFactory.CreateVolume("3", new List<Chapter>()
                {
                    EntityFactory.CreateChapter("31", false, new List<MangaFile>()),
                    EntityFactory.CreateChapter("32", false, new List<MangaFile>()),
                }),
            }
        });

        _context.AppUser.Add(new AppUser()
        {
            UserName = "majora2007"
        });

        await _context.SaveChangesAsync();

        var readerService = new ReaderService(_unitOfWork, Substitute.For<ILogger<ReaderService>>());

        var nextChapter = await readerService.GetNextChapterIdAsync(1, 1, 1, 1);
        var actualChapter = await _unitOfWork.ChapterRepository.GetChapterAsync(nextChapter);
        Assert.Equal("2", actualChapter.Range);
    }

    [Fact]
    public async Task GetNextChapterIdAsync_ShouldRollIntoNextVolume()
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
                EntityFactory.CreateVolume("1", new List<Chapter>()
                {
                    EntityFactory.CreateChapter("1", false, new List<MangaFile>()),
                    EntityFactory.CreateChapter("2", false, new List<MangaFile>()),
                }),
                EntityFactory.CreateVolume("2", new List<Chapter>()
                {
                    EntityFactory.CreateChapter("21", false, new List<MangaFile>()),
                    EntityFactory.CreateChapter("22", false, new List<MangaFile>()),
                }),
                EntityFactory.CreateVolume("3", new List<Chapter>()
                {
                    EntityFactory.CreateChapter("31", false, new List<MangaFile>()),
                    EntityFactory.CreateChapter("32", false, new List<MangaFile>()),
                }),
            }
        });

        _context.AppUser.Add(new AppUser()
        {
            UserName = "majora2007"
        });

        await _context.SaveChangesAsync();

        var readerService = new ReaderService(_unitOfWork, Substitute.For<ILogger<ReaderService>>());


        var nextChapter = await readerService.GetNextChapterIdAsync(1, 1, 2, 1);
        var actualChapter = await _unitOfWork.ChapterRepository.GetChapterAsync(nextChapter);
        Assert.Equal("21", actualChapter.Range);
    }

    [Fact]
    public async Task GetNextChapterIdAsync_ShouldRollIntoChaptersFromVolume()
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
                EntityFactory.CreateVolume("0", new List<Chapter>()
                {
                    EntityFactory.CreateChapter("1", false, new List<MangaFile>()),
                    EntityFactory.CreateChapter("2", false, new List<MangaFile>()),
                }),
                EntityFactory.CreateVolume("1", new List<Chapter>()
                {
                    EntityFactory.CreateChapter("21", false, new List<MangaFile>()),
                    EntityFactory.CreateChapter("22", false, new List<MangaFile>()),
                }),
            }
        });

        _context.AppUser.Add(new AppUser()
        {
            UserName = "majora2007"
        });

        await _context.SaveChangesAsync();

        var readerService = new ReaderService(_unitOfWork, Substitute.For<ILogger<ReaderService>>());


        var nextChapter = await readerService.GetNextChapterIdAsync(1, 2, 4, 1);
        Assert.NotEqual(-1, nextChapter);
        var actualChapter = await _unitOfWork.ChapterRepository.GetChapterAsync(nextChapter);
        Assert.Equal("1", actualChapter.Range);
    }

    [Fact]
    public async Task GetNextChapterIdAsync_ShouldFindNoNextChapterFromSpecial()
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
                EntityFactory.CreateVolume("1", new List<Chapter>()
                {
                    EntityFactory.CreateChapter("1", false, new List<MangaFile>()),
                    EntityFactory.CreateChapter("2", false, new List<MangaFile>()),
                }),
                EntityFactory.CreateVolume("0", new List<Chapter>()
                {
                    EntityFactory.CreateChapter("A.cbz", true, new List<MangaFile>()),
                    EntityFactory.CreateChapter("B.cbz", true, new List<MangaFile>()),
                }),
            }
        });

        _context.AppUser.Add(new AppUser()
        {
            UserName = "majora2007"
        });

        await _context.SaveChangesAsync();

        var readerService = new ReaderService(_unitOfWork, Substitute.For<ILogger<ReaderService>>());


        var nextChapter = await readerService.GetNextChapterIdAsync(1, 2, 4, 1);
        Assert.Equal(-1, nextChapter);
    }

    [Fact]
    public async Task GetNextChapterIdAsync_ShouldFindNoNextChapterFromVolume()
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
                EntityFactory.CreateVolume("1", new List<Chapter>()
                {
                    EntityFactory.CreateChapter("1", false, new List<MangaFile>()),
                    EntityFactory.CreateChapter("2", false, new List<MangaFile>()),
                }),
            }
        });

        _context.AppUser.Add(new AppUser()
        {
            UserName = "majora2007"
        });

        await _context.SaveChangesAsync();

        var readerService = new ReaderService(_unitOfWork, Substitute.For<ILogger<ReaderService>>());


        var nextChapter = await readerService.GetNextChapterIdAsync(1, 1, 2, 1);
        Assert.Equal(-1, nextChapter);
    }

    [Fact]
    public async Task GetNextChapterIdAsync_ShouldFindNoNextChapterFromLastChapter()
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
                EntityFactory.CreateVolume("0", new List<Chapter>()
                {
                    EntityFactory.CreateChapter("1", false, new List<MangaFile>()),
                    EntityFactory.CreateChapter("2", false, new List<MangaFile>()),
                }),
                EntityFactory.CreateVolume("1", new List<Chapter>()
                {
                    EntityFactory.CreateChapter("1", false, new List<MangaFile>()),
                    EntityFactory.CreateChapter("2", false, new List<MangaFile>()),
                }),
            }
        });

        _context.AppUser.Add(new AppUser()
        {
            UserName = "majora2007"
        });

        await _context.SaveChangesAsync();

        var readerService = new ReaderService(_unitOfWork, Substitute.For<ILogger<ReaderService>>());


        var nextChapter = await readerService.GetNextChapterIdAsync(1, 1, 2, 1);
        Assert.Equal(-1, nextChapter);
    }

    [Fact]
    public async Task GetNextChapterIdAsync_ShouldMoveFromVolumeToSpecial()
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
                EntityFactory.CreateVolume("1", new List<Chapter>()
                {
                    EntityFactory.CreateChapter("1", false, new List<MangaFile>()),
                    EntityFactory.CreateChapter("2", false, new List<MangaFile>()),
                }),
                EntityFactory.CreateVolume("0", new List<Chapter>()
                {
                    EntityFactory.CreateChapter("A.cbz", true, new List<MangaFile>()),
                    EntityFactory.CreateChapter("B.cbz", true, new List<MangaFile>()),
                }),
            }
        });

        _context.AppUser.Add(new AppUser()
        {
            UserName = "majora2007"
        });

        await _context.SaveChangesAsync();

        var readerService = new ReaderService(_unitOfWork, Substitute.For<ILogger<ReaderService>>());


        var nextChapter = await readerService.GetNextChapterIdAsync(1, 1, 2, 1);
        Assert.NotEqual(-1, nextChapter);
        var actualChapter = await _unitOfWork.ChapterRepository.GetChapterAsync(nextChapter);
        Assert.Equal("A.cbz", actualChapter.Range);
    }

    [Fact]
    public async Task GetNextChapterIdAsync_ShouldMoveFromSpecialToSpecial()
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
                EntityFactory.CreateVolume("1", new List<Chapter>()
                {
                    EntityFactory.CreateChapter("1", false, new List<MangaFile>()),
                    EntityFactory.CreateChapter("2", false, new List<MangaFile>()),
                }),
                EntityFactory.CreateVolume("0", new List<Chapter>()
                {
                    EntityFactory.CreateChapter("A.cbz", true, new List<MangaFile>()),
                    EntityFactory.CreateChapter("B.cbz", true, new List<MangaFile>()),
                }),
            }
        });

        _context.AppUser.Add(new AppUser()
        {
            UserName = "majora2007"
        });

        await _context.SaveChangesAsync();

        var readerService = new ReaderService(_unitOfWork, Substitute.For<ILogger<ReaderService>>());


        var nextChapter = await readerService.GetNextChapterIdAsync(1, 2, 3, 1);
        Assert.NotEqual(-1, nextChapter);
        var actualChapter = await _unitOfWork.ChapterRepository.GetChapterAsync(nextChapter);
        Assert.Equal("B.cbz", actualChapter.Range);
    }

    #endregion

    #region GetPrevChapterIdAsync

    [Fact]
    public async Task GetPrevChapterIdAsync_ShouldGetPrevVolume()
    {
        // V1 -> V2
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
                EntityFactory.CreateVolume("1", new List<Chapter>()
                {
                    EntityFactory.CreateChapter("1", false, new List<MangaFile>()),
                    EntityFactory.CreateChapter("2", false, new List<MangaFile>()),
                }),
                EntityFactory.CreateVolume("2", new List<Chapter>()
                {
                    EntityFactory.CreateChapter("21", false, new List<MangaFile>()),
                    EntityFactory.CreateChapter("22", false, new List<MangaFile>()),
                }),
                EntityFactory.CreateVolume("3", new List<Chapter>()
                {
                    EntityFactory.CreateChapter("31", false, new List<MangaFile>()),
                    EntityFactory.CreateChapter("32", false, new List<MangaFile>()),
                }),
            }
        });

        _context.AppUser.Add(new AppUser()
        {
            UserName = "majora2007"
        });

        await _context.SaveChangesAsync();

        var readerService = new ReaderService(_unitOfWork, Substitute.For<ILogger<ReaderService>>());

        var prevChapter = await readerService.GetPrevChapterIdAsync(1, 1, 2, 1);
        var actualChapter = await _unitOfWork.ChapterRepository.GetChapterAsync(prevChapter);
        Assert.Equal("1", actualChapter.Range);
    }

    [Fact]
    public async Task GetPrevChapterIdAsync_ShouldRollIntoPrevVolume()
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
                EntityFactory.CreateVolume("1", new List<Chapter>()
                {
                    EntityFactory.CreateChapter("1", false, new List<MangaFile>()),
                    EntityFactory.CreateChapter("2", false, new List<MangaFile>()),
                }),
                EntityFactory.CreateVolume("2", new List<Chapter>()
                {
                    EntityFactory.CreateChapter("21", false, new List<MangaFile>()),
                    EntityFactory.CreateChapter("22", false, new List<MangaFile>()),
                }),
                EntityFactory.CreateVolume("3", new List<Chapter>()
                {
                    EntityFactory.CreateChapter("31", false, new List<MangaFile>()),
                    EntityFactory.CreateChapter("32", false, new List<MangaFile>()),
                }),
            }
        });

        _context.AppUser.Add(new AppUser()
        {
            UserName = "majora2007"
        });

        await _context.SaveChangesAsync();

        var readerService = new ReaderService(_unitOfWork, Substitute.For<ILogger<ReaderService>>());


        var prevChapter = await readerService.GetPrevChapterIdAsync(1, 2, 3, 1);
        var actualChapter = await _unitOfWork.ChapterRepository.GetChapterAsync(prevChapter);
        Assert.Equal("2", actualChapter.Range);
    }

    [Fact]
    public async Task GetPrevChapterIdAsync_ShouldMoveFromSpecialToVolume()
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
                EntityFactory.CreateVolume("1", new List<Chapter>()
                {
                    EntityFactory.CreateChapter("1", false, new List<MangaFile>()),
                    EntityFactory.CreateChapter("2", false, new List<MangaFile>()),
                }),
                EntityFactory.CreateVolume("0", new List<Chapter>()
                {
                    EntityFactory.CreateChapter("A.cbz", true, new List<MangaFile>()),
                    EntityFactory.CreateChapter("B.cbz", true, new List<MangaFile>()),
                }),
            }
        });

        _context.AppUser.Add(new AppUser()
        {
            UserName = "majora2007"
        });

        await _context.SaveChangesAsync();

        var readerService = new ReaderService(_unitOfWork, Substitute.For<ILogger<ReaderService>>());


        var prevChapter = await readerService.GetPrevChapterIdAsync(1, 2, 3, 1);
        Assert.Equal(2, prevChapter);
        var actualChapter = await _unitOfWork.ChapterRepository.GetChapterAsync(prevChapter);
        Assert.Equal("2", actualChapter.Range);
    }

    [Fact]
    public async Task GetPrevChapterIdAsync_ShouldFindNoPrevChapterFromVolume()
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
                EntityFactory.CreateVolume("1", new List<Chapter>()
                {
                    EntityFactory.CreateChapter("1", false, new List<MangaFile>()),
                    EntityFactory.CreateChapter("2", false, new List<MangaFile>()),
                }),
            }
        });

        _context.AppUser.Add(new AppUser()
        {
            UserName = "majora2007"
        });

        await _context.SaveChangesAsync();

        var readerService = new ReaderService(_unitOfWork, Substitute.For<ILogger<ReaderService>>());


        var prevChapter = await readerService.GetPrevChapterIdAsync(1, 1, 1, 1);
        Assert.Equal(-1, prevChapter);
    }

    [Fact]
    public async Task GetPrevChapterIdAsync_ShouldFindNoPrevChapterFromVolumeWithZeroChapter()
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
                EntityFactory.CreateVolume("1", new List<Chapter>()
                {
                    EntityFactory.CreateChapter("0", false, new List<MangaFile>()),
                }),
            }
        });

        _context.AppUser.Add(new AppUser()
        {
            UserName = "majora2007"
        });

        await _context.SaveChangesAsync();

        var readerService = new ReaderService(_unitOfWork, Substitute.For<ILogger<ReaderService>>());


        var prevChapter = await readerService.GetPrevChapterIdAsync(1, 1, 1, 1);
        Assert.Equal(-1, prevChapter);
    }

    [Fact]
    public async Task GetPrevChapterIdAsync_ShouldFindNoPrevChapterFromVolumeWithZeroChapterAndHasNormalChapters()
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
                EntityFactory.CreateVolume("0", new List<Chapter>()
                {
                    EntityFactory.CreateChapter("1", false, new List<MangaFile>()),
                    EntityFactory.CreateChapter("2", false, new List<MangaFile>()),
                }),
                EntityFactory.CreateVolume("1", new List<Chapter>()
                {
                    EntityFactory.CreateChapter("0", false, new List<MangaFile>()),
                }),
            }
        });

        _context.AppUser.Add(new AppUser()
        {
            UserName = "majora2007"
        });

        await _context.SaveChangesAsync();

        var readerService = new ReaderService(_unitOfWork, Substitute.For<ILogger<ReaderService>>());


        var prevChapter = await readerService.GetPrevChapterIdAsync(1, 1, 1, 1);
        Assert.Equal(-1, prevChapter);
    }

    [Fact]
    public async Task GetPrevChapterIdAsync_ShouldFindNoPrevChapterFromVolumeWithZeroChapterAndHasNormalChapters2()
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
                EntityFactory.CreateVolume("0", new List<Chapter>()
                {
                    EntityFactory.CreateChapter("5", false, new List<MangaFile>()),
                    EntityFactory.CreateChapter("6", false, new List<MangaFile>()),
                    EntityFactory.CreateChapter("7", false, new List<MangaFile>()),

                }),
                EntityFactory.CreateVolume("1", new List<Chapter>()
                {
                    EntityFactory.CreateChapter("1", false, new List<MangaFile>()),
                    EntityFactory.CreateChapter("2", false, new List<MangaFile>()),
                }),
                EntityFactory.CreateVolume("2", new List<Chapter>()
                {
                    EntityFactory.CreateChapter("3", false, new List<MangaFile>()),
                    EntityFactory.CreateChapter("4", false, new List<MangaFile>()),
                }),
            }
        });

        _context.AppUser.Add(new AppUser()
        {
            UserName = "majora2007"
        });

        await _context.SaveChangesAsync();

        var readerService = new ReaderService(_unitOfWork, Substitute.For<ILogger<ReaderService>>());

        var prevChapter = await readerService.GetPrevChapterIdAsync(1, 2,5, 1);
        var chapterInfoDto = await _unitOfWork.ChapterRepository.GetChapterInfoDtoAsync(prevChapter);
        Assert.Equal(1, float.Parse(chapterInfoDto.ChapterNumber));

        // This is first chapter of first volume
        prevChapter = await readerService.GetPrevChapterIdAsync(1, 2,4, 1);
        Assert.Equal(-1, prevChapter);
        //chapterInfoDto = await _unitOfWork.ChapterRepository.GetChapterInfoDtoAsync(prevChapter);

    }

    [Fact]
    public async Task GetPrevChapterIdAsync_ShouldFindNoPrevChapterFromChapter()
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
                EntityFactory.CreateVolume("0", new List<Chapter>()
                {
                    EntityFactory.CreateChapter("1", false, new List<MangaFile>()),
                    EntityFactory.CreateChapter("2", false, new List<MangaFile>()),
                }),
            }
        });

        _context.AppUser.Add(new AppUser()
        {
            UserName = "majora2007"
        });

        await _context.SaveChangesAsync();

        var readerService = new ReaderService(_unitOfWork, Substitute.For<ILogger<ReaderService>>());


        var prevChapter = await readerService.GetPrevChapterIdAsync(1, 1, 1, 1);
        Assert.Equal(-1, prevChapter);
    }

    [Fact]
    public async Task GetPrevChapterIdAsync_ShouldMoveFromSpecialToSpecial()
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
                EntityFactory.CreateVolume("1", new List<Chapter>()
                {
                    EntityFactory.CreateChapter("1", false, new List<MangaFile>()),
                    EntityFactory.CreateChapter("2", false, new List<MangaFile>()),
                }),
                EntityFactory.CreateVolume("0", new List<Chapter>()
                {
                    EntityFactory.CreateChapter("A.cbz", true, new List<MangaFile>()),
                    EntityFactory.CreateChapter("B.cbz", true, new List<MangaFile>()),
                }),
            }
        });

        _context.AppUser.Add(new AppUser()
        {
            UserName = "majora2007"
        });

        await _context.SaveChangesAsync();

        var readerService = new ReaderService(_unitOfWork, Substitute.For<ILogger<ReaderService>>());


        var prevChapter = await readerService.GetPrevChapterIdAsync(1, 2, 4, 1);
        Assert.NotEqual(-1, prevChapter);
        var actualChapter = await _unitOfWork.ChapterRepository.GetChapterAsync(prevChapter);
        Assert.Equal("A.cbz", actualChapter.Range);
    }

    [Fact]
    public async Task GetPrevChapterIdAsync_ShouldMoveFromChapterToVolume()
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
                EntityFactory.CreateVolume("0", new List<Chapter>()
                {
                    EntityFactory.CreateChapter("1", false, new List<MangaFile>()),
                    EntityFactory.CreateChapter("2", false, new List<MangaFile>()),
                }),
                EntityFactory.CreateVolume("1", new List<Chapter>()
                {
                    EntityFactory.CreateChapter("21", false, new List<MangaFile>()),
                    EntityFactory.CreateChapter("22", false, new List<MangaFile>()),
                }),
            }
        });

        _context.AppUser.Add(new AppUser()
        {
            UserName = "majora2007"
        });

        await _context.SaveChangesAsync();

        var readerService = new ReaderService(_unitOfWork, Substitute.For<ILogger<ReaderService>>());


        var prevChapter = await readerService.GetPrevChapterIdAsync(1, 1, 1, 1);
        Assert.NotEqual(-1, prevChapter);
        var actualChapter = await _unitOfWork.ChapterRepository.GetChapterAsync(prevChapter);
        Assert.Equal("22", actualChapter.Range);
    }
    #endregion

    #region GetContinuePoint

    [Fact]
    public async Task GetContinuePoint_ShouldReturnFirstVolume_NoProgress()
    {
        _context.Series.Add(new Series()
        {
            Name = "Test",
            Library = new Library() {
                Name = "Test LIb",
                Type = LibraryType.Manga,
            },
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
                    EntityFactory.CreateChapter("2", false, new List<MangaFile>(), 1),
                }),
                EntityFactory.CreateVolume("2", new List<Chapter>()
                {
                    EntityFactory.CreateChapter("21", false, new List<MangaFile>(), 1),
                    EntityFactory.CreateChapter("22", false, new List<MangaFile>(), 1),
                }),
                EntityFactory.CreateVolume("3", new List<Chapter>()
                {
                    EntityFactory.CreateChapter("31", false, new List<MangaFile>(), 1),
                    EntityFactory.CreateChapter("32", false, new List<MangaFile>(), 1),
                }),
            }
        });

        _context.AppUser.Add(new AppUser()
        {
            UserName = "majora2007"
        });

        await _context.SaveChangesAsync();


        var readerService = new ReaderService(_unitOfWork, Substitute.For<ILogger<ReaderService>>());

        var nextChapter = await readerService.GetContinuePoint(1, 1);

        Assert.Equal("1", nextChapter.Range);
    }

    [Fact]
    public async Task GetContinuePoint_ShouldReturnFirstNonSpecial()
    {
        _context.Series.Add(new Series()
        {
            Name = "Test",
            Library = new Library() {
                Name = "Test LIb",
                Type = LibraryType.Manga,
            },
            Volumes = new List<Volume>()
            {
                EntityFactory.CreateVolume("1", new List<Chapter>()
                {
                    EntityFactory.CreateChapter("1", false, new List<MangaFile>(), 1),
                    EntityFactory.CreateChapter("2", false, new List<MangaFile>(), 1),
                }),
                EntityFactory.CreateVolume("2", new List<Chapter>()
                {
                    EntityFactory.CreateChapter("21", false, new List<MangaFile>(), 1),
                    EntityFactory.CreateChapter("22", false, new List<MangaFile>(), 1),
                }),
                EntityFactory.CreateVolume("3", new List<Chapter>()
                {
                    EntityFactory.CreateChapter("31", false, new List<MangaFile>(), 1),
                    EntityFactory.CreateChapter("32", false, new List<MangaFile>(), 1),
                }),
            }
        });

        _context.AppUser.Add(new AppUser()
        {
            UserName = "majora2007"
        });

        await _context.SaveChangesAsync();


        var readerService = new ReaderService(_unitOfWork, Substitute.For<ILogger<ReaderService>>());

        // Save progress on first volume chapters and 1st of second volume
        await readerService.SaveReadingProgress(new ProgressDto()
        {
            PageNum = 1,
            ChapterId = 1,
            SeriesId = 1,
            VolumeId = 1
        }, 1);
        await readerService.SaveReadingProgress(new ProgressDto()
        {
            PageNum = 1,
            ChapterId = 2,
            SeriesId = 1,
            VolumeId = 1
        }, 1);
        await readerService.SaveReadingProgress(new ProgressDto()
        {
            PageNum = 1,
            ChapterId = 3,
            SeriesId = 1,
            VolumeId = 2
        }, 1);

        await _context.SaveChangesAsync();

        var nextChapter = await readerService.GetContinuePoint(1, 1);

        Assert.Equal("22", nextChapter.Range);


    }

    [Fact]
    public async Task GetContinuePoint_ShouldReturnFirstSpecial()
    {
        _context.Series.Add(new Series()
        {
            Name = "Test",
            Library = new Library() {
                Name = "Test LIb",
                Type = LibraryType.Manga,
            },
            Volumes = new List<Volume>()
            {
                EntityFactory.CreateVolume("1", new List<Chapter>()
                {
                    EntityFactory.CreateChapter("1", false, new List<MangaFile>(), 1),
                    EntityFactory.CreateChapter("2", false, new List<MangaFile>(), 1),
                }),
                EntityFactory.CreateVolume("2", new List<Chapter>()
                {
                    EntityFactory.CreateChapter("21", false, new List<MangaFile>(), 1),
                }),
                EntityFactory.CreateVolume("0", new List<Chapter>()
                {
                    EntityFactory.CreateChapter("31", false, new List<MangaFile>(), 1),
                    EntityFactory.CreateChapter("32", false, new List<MangaFile>(), 1),
                }),
            }
        });

        _context.AppUser.Add(new AppUser()
        {
            UserName = "majora2007"
        });

        await _context.SaveChangesAsync();


        var readerService = new ReaderService(_unitOfWork, Substitute.For<ILogger<ReaderService>>());

        // Save progress on first volume chapters and 1st of second volume
        await readerService.SaveReadingProgress(new ProgressDto()
        {
            PageNum = 1,
            ChapterId = 1,
            SeriesId = 1,
            VolumeId = 1
        }, 1);
        await readerService.SaveReadingProgress(new ProgressDto()
        {
            PageNum = 1,
            ChapterId = 2,
            SeriesId = 1,
            VolumeId = 1
        }, 1);
        await readerService.SaveReadingProgress(new ProgressDto()
        {
            PageNum = 1,
            ChapterId = 3,
            SeriesId = 1,
            VolumeId = 2
        }, 1);

        await _context.SaveChangesAsync();

        var nextChapter = await readerService.GetContinuePoint(1, 1);

        Assert.Equal("31", nextChapter.Range);
    }

    [Fact]
    public async Task GetContinuePoint_ShouldReturnFirstChapter_WhenNonRead_LooseLeafChaptersAndVolumes()
    {
        _context.Series.Add(new Series()
        {
            Name = "Test",
            Library = new Library() {
                Name = "Test LIb",
                Type = LibraryType.Manga,
            },
            Volumes = new List<Volume>()
            {
                EntityFactory.CreateVolume("0", new List<Chapter>()
                {
                    EntityFactory.CreateChapter("230", false, new List<MangaFile>(), 1),
                    EntityFactory.CreateChapter("231", false, new List<MangaFile>(), 1),
                }),
                EntityFactory.CreateVolume("1", new List<Chapter>()
                {
                    EntityFactory.CreateChapter("1", false, new List<MangaFile>(), 1),
                    EntityFactory.CreateChapter("2", false, new List<MangaFile>(), 1),
                }),
                EntityFactory.CreateVolume("2", new List<Chapter>()
                {
                    EntityFactory.CreateChapter("21", false, new List<MangaFile>(), 1),
                }),
            }
        });

        _context.AppUser.Add(new AppUser()
        {
            UserName = "majora2007"
        });

        await _context.SaveChangesAsync();

        var readerService = new ReaderService(_unitOfWork, Substitute.For<ILogger<ReaderService>>());
        var nextChapter = await readerService.GetContinuePoint(1, 1);

        Assert.Equal("1", nextChapter.Range);
    }

    [Fact]
    public async Task GetContinuePoint_ShouldReturnFirstChapter_WhenAllRead()
    {
        _context.Series.Add(new Series()
        {
            Name = "Test",
            Library = new Library() {
                Name = "Test LIb",
                Type = LibraryType.Manga,
            },
            Volumes = new List<Volume>()
            {
                EntityFactory.CreateVolume("1", new List<Chapter>()
                {
                    EntityFactory.CreateChapter("1", false, new List<MangaFile>(), 1),
                    EntityFactory.CreateChapter("2", false, new List<MangaFile>(), 1),
                }),
                EntityFactory.CreateVolume("2", new List<Chapter>()
                {
                    EntityFactory.CreateChapter("21", false, new List<MangaFile>(), 1),
                }),
            }
        });

        _context.AppUser.Add(new AppUser()
        {
            UserName = "majora2007"
        });

        await _context.SaveChangesAsync();

        var readerService = new ReaderService(_unitOfWork, Substitute.For<ILogger<ReaderService>>());

        // Save progress on first volume chapters and 1st of second volume
        await readerService.SaveReadingProgress(new ProgressDto()
        {
            PageNum = 1,
            ChapterId = 1,
            SeriesId = 1,
            VolumeId = 1
        }, 1);
        await readerService.SaveReadingProgress(new ProgressDto()
        {
            PageNum = 1,
            ChapterId = 2,
            SeriesId = 1,
            VolumeId = 1
        }, 1);
        await readerService.SaveReadingProgress(new ProgressDto()
        {
            PageNum = 1,
            ChapterId = 3,
            SeriesId = 1,
            VolumeId = 2
        }, 1);

        await _context.SaveChangesAsync();

        var nextChapter = await readerService.GetContinuePoint(1, 1);

        Assert.Equal("1", nextChapter.Range);
    }

    [Fact]
    public async Task GetContinuePoint_ShouldReturnFirstChapter_WhenAllReadAndAllChapters()
    {
        _context.Series.Add(new Series()
        {
            Name = "Test",
            Library = new Library() {
                Name = "Test LIb",
                Type = LibraryType.Manga,
            },
            Volumes = new List<Volume>()
            {
                EntityFactory.CreateVolume("0", new List<Chapter>()
                {
                    EntityFactory.CreateChapter("1", false, new List<MangaFile>(), 1),
                    EntityFactory.CreateChapter("2", false, new List<MangaFile>(), 1),
                    EntityFactory.CreateChapter("3", false, new List<MangaFile>(), 1),
                }),
            }
        });

        _context.AppUser.Add(new AppUser()
        {
            UserName = "majora2007"
        });

        await _context.SaveChangesAsync();

        var readerService = new ReaderService(_unitOfWork, Substitute.For<ILogger<ReaderService>>());

        // Save progress on first volume chapters and 1st of second volume
        await readerService.SaveReadingProgress(new ProgressDto()
        {
            PageNum = 1,
            ChapterId = 1,
            SeriesId = 1,
            VolumeId = 1
        }, 1);
        await readerService.SaveReadingProgress(new ProgressDto()
        {
            PageNum = 1,
            ChapterId = 2,
            SeriesId = 1,
            VolumeId = 1
        }, 1);
        await readerService.SaveReadingProgress(new ProgressDto()
        {
            PageNum = 1,
            ChapterId = 3,
            SeriesId = 1,
            VolumeId = 1
        }, 1);

        await _context.SaveChangesAsync();

        var nextChapter = await readerService.GetContinuePoint(1, 1);

        Assert.Equal("1", nextChapter.Range);
    }

    [Fact]
    public async Task GetContinuePoint_ShouldReturnFirstSpecial_WhenAllReadAndAllChapters()
    {
        _context.Series.Add(new Series()
        {
            Name = "Test",
            Library = new Library() {
                Name = "Test LIb",
                Type = LibraryType.Manga,
            },
            Volumes = new List<Volume>()
            {
                EntityFactory.CreateVolume("0", new List<Chapter>()
                {
                    EntityFactory.CreateChapter("1", false, new List<MangaFile>(), 1),
                    EntityFactory.CreateChapter("2", false, new List<MangaFile>(), 1),
                    EntityFactory.CreateChapter("3", false, new List<MangaFile>(), 1),
                    EntityFactory.CreateChapter("Some Special Title", true, new List<MangaFile>(), 1),
                }),
            }
        });

        _context.AppUser.Add(new AppUser()
        {
            UserName = "majora2007"
        });

        await _context.SaveChangesAsync();

        var readerService = new ReaderService(_unitOfWork, Substitute.For<ILogger<ReaderService>>());

        // Save progress on first volume chapters and 1st of second volume
        await readerService.SaveReadingProgress(new ProgressDto()
        {
            PageNum = 1,
            ChapterId = 1,
            SeriesId = 1,
            VolumeId = 1
        }, 1);
        await readerService.SaveReadingProgress(new ProgressDto()
        {
            PageNum = 1,
            ChapterId = 2,
            SeriesId = 1,
            VolumeId = 1
        }, 1);
        await readerService.SaveReadingProgress(new ProgressDto()
        {
            PageNum = 1,
            ChapterId = 3,
            SeriesId = 1,
            VolumeId = 1
        }, 1);

        await _context.SaveChangesAsync();

        var nextChapter = await readerService.GetContinuePoint(1, 1);

        Assert.Equal("Some Special Title", nextChapter.Range);
    }

    #endregion

    #region MarkChaptersUntilAsRead

    [Fact]
    public async Task MarkChaptersUntilAsRead_ShouldMarkAllChaptersAsRead()
    {
        _context.Series.Add(new Series()
        {
            Name = "Test",
            Library = new Library() {
                Name = "Test LIb",
                Type = LibraryType.Manga,
            },
            Volumes = new List<Volume>()
            {
                EntityFactory.CreateVolume("0", new List<Chapter>()
                {
                    EntityFactory.CreateChapter("1", false, new List<MangaFile>(), 1),
                    EntityFactory.CreateChapter("2", false, new List<MangaFile>(), 1),
                    EntityFactory.CreateChapter("3", false, new List<MangaFile>(), 1),
                    EntityFactory.CreateChapter("Some Special Title", true, new List<MangaFile>(), 1),
                }),
            }
        });

        _context.AppUser.Add(new AppUser()
        {
            UserName = "majora2007"
        });

        await _context.SaveChangesAsync();

        var readerService = new ReaderService(_unitOfWork, Substitute.For<ILogger<ReaderService>>());

        var user = await _unitOfWork.UserRepository.GetUserByUsernameAsync("majora2007", AppUserIncludes.Progress);
        await readerService.MarkChaptersUntilAsRead(user, 1, 5);
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
        _context.Series.Add(new Series()
        {
            Name = "Test",
            Library = new Library() {
                Name = "Test LIb",
                Type = LibraryType.Manga,
            },
            Volumes = new List<Volume>()
            {
                EntityFactory.CreateVolume("0", new List<Chapter>()
                {
                    EntityFactory.CreateChapter("1", false, new List<MangaFile>(), 1),
                    EntityFactory.CreateChapter("2", false, new List<MangaFile>(), 1),
                    EntityFactory.CreateChapter("2.5", false, new List<MangaFile>(), 1),
                    EntityFactory.CreateChapter("3", false, new List<MangaFile>(), 1),
                    EntityFactory.CreateChapter("Some Special Title", true, new List<MangaFile>(), 1),
                }),
            }
        });

        _context.AppUser.Add(new AppUser()
        {
            UserName = "majora2007"
        });

        await _context.SaveChangesAsync();

        var readerService = new ReaderService(_unitOfWork, Substitute.For<ILogger<ReaderService>>());

        var user = await _unitOfWork.UserRepository.GetUserByUsernameAsync("majora2007", AppUserIncludes.Progress);
        await readerService.MarkChaptersUntilAsRead(user, 1, 2.5f);
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
        _context.Series.Add(new Series()
        {
            Name = "Test",
            Library = new Library() {
                Name = "Test LIb",
                Type = LibraryType.Manga,
            },
            Volumes = new List<Volume>()
            {
                EntityFactory.CreateVolume("1", new List<Chapter>()
                {
                    EntityFactory.CreateChapter("0", false, new List<MangaFile>(), 1),
                }),
                EntityFactory.CreateVolume("2", new List<Chapter>()
                {
                    EntityFactory.CreateChapter("0", false, new List<MangaFile>(), 1),
                }),
            }
        });

        _context.AppUser.Add(new AppUser()
        {
            UserName = "majora2007"
        });

        await _context.SaveChangesAsync();

        var readerService = new ReaderService(_unitOfWork, Substitute.For<ILogger<ReaderService>>());

        var user = await _unitOfWork.UserRepository.GetUserByUsernameAsync("majora2007", AppUserIncludes.Progress);
        await readerService.MarkChaptersUntilAsRead(user, 1, 2);
        await _context.SaveChangesAsync();

        // Validate correct chapters have read status
        Assert.True(await _unitOfWork.AppUserProgressRepository.UserHasProgress(LibraryType.Manga, 1));
    }

    #endregion



}
