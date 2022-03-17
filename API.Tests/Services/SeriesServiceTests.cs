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
using API.SignalR;
using API.Tests.Helpers;
using AutoMapper;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.Extensions.Logging;
using NSubstitute;
using Xunit;

namespace API.Tests.Services;

public class SeriesServiceTests
{
    private readonly IUnitOfWork _unitOfWork;

    private readonly DbConnection _connection;
    private readonly DataContext _context;

    private readonly ISeriesService _seriesService;

    private const string CacheDirectory = "C:/kavita/config/cache/";
    private const string CoverImageDirectory = "C:/kavita/config/covers/";
    private const string BackupDirectory = "C:/kavita/config/backups/";
    private const string DataDirectory = "C:/data/";

    public SeriesServiceTests()
    {
        var contextOptions = new DbContextOptionsBuilder().UseSqlite(CreateInMemoryDatabase()).Options;
        _connection = RelationalOptionsExtension.Extract(contextOptions).Connection;

        _context = new DataContext(contextOptions);
        Task.Run(SeedDb).GetAwaiter().GetResult();

        var config = new MapperConfiguration(cfg => cfg.AddProfile<AutoMapperProfiles>());
        var mapper = config.CreateMapper();
        _unitOfWork = new UnitOfWork(_context, mapper, null);

        _seriesService = new SeriesService(_unitOfWork, Substitute.For<IEventHub>(),
            Substitute.For<ITaskScheduler>(), Substitute.For<ILogger<SeriesService>>());
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

        var lib = new Library()
        {
            Name = "Manga", Folders = new List<FolderPath>() {new FolderPath() {Path = "C:/data/"}}
        };

        _context.AppUser.Add(new AppUser()
        {
            UserName = "majora2007",
            Libraries = new List<Library>()
            {
                lib
            }
        });

        return await _context.SaveChangesAsync() > 0;
    }

    private async Task ResetDb()
    {
        _context.Series.RemoveRange(_context.Series.ToList());
        _context.AppUserRating.RemoveRange(_context.AppUserRating.ToList());

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

    #region SeriesDetail

    [Fact]
    public async Task SeriesDetail_ShouldReturnSpecials()
    {
        await ResetDb();

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
                    EntityFactory.CreateChapter("Omake", true, new List<MangaFile>()),
                    EntityFactory.CreateChapter("Something SP02", true, new List<MangaFile>()),
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

        await _context.SaveChangesAsync();

        var expectedRanges = new[] {"Omake", "Something SP02"};

        var detail = await _seriesService.GetSeriesDetail(1, 1);
        Assert.NotEmpty(detail.Specials);
        Assert.True(2 == detail.Specials.Count());
        Assert.All(detail.Specials, dto => Assert.Contains(dto.Range, expectedRanges));
    }

    [Fact]
    public async Task SeriesDetail_ShouldReturnVolumesAndChapters()
    {
        await ResetDb();

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

        await _context.SaveChangesAsync();

        var detail = await _seriesService.GetSeriesDetail(1, 1);
        Assert.NotEmpty(detail.Chapters);
        Assert.Equal(6, detail.Chapters.Count());

        Assert.NotEmpty(detail.Volumes);
        Assert.Equal(2, detail.Volumes.Count()); // Volume 0 shouldn't be sent in Volumes
        Assert.All(detail.Volumes, dto => Assert.Contains(dto.Name, new[] {"Volume 2", "Volume 3"})); // Volumes get names mapped
    }

    [Fact]
    public async Task SeriesDetail_ShouldReturnVolumesAndChapters_ButRemove0Chapter()
    {
        await ResetDb();

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
                EntityFactory.CreateVolume("2", new List<Chapter>()
                {
                    EntityFactory.CreateChapter("0", false, new List<MangaFile>()),
                }),
                EntityFactory.CreateVolume("3", new List<Chapter>()
                {
                    EntityFactory.CreateChapter("31", false, new List<MangaFile>()),
                }),
            }
        });

        await _context.SaveChangesAsync();

        var detail = await _seriesService.GetSeriesDetail(1, 1);
        Assert.NotEmpty(detail.Chapters);
        // volume 2 has a 0 chapter aka a single chapter that is represented as a volume. We don't show in Chapters area
        Assert.Equal(3, detail.Chapters.Count());

        Assert.NotEmpty(detail.Volumes);
        Assert.Equal(2, detail.Volumes.Count());
    }

    [Fact]
    public async Task SeriesDetail_ShouldReturnChaptersOnly_WhenBookLibrary()
    {
        await ResetDb();

        _context.Series.Add(new Series()
        {
            Name = "Test",
            Library = new Library() {
                Name = "Test LIb",
                Type = LibraryType.Book,
            },
            Volumes = new List<Volume>()
            {
                EntityFactory.CreateVolume("2", new List<Chapter>()
                {
                    EntityFactory.CreateChapter("0", false, new List<MangaFile>()),
                }),
                EntityFactory.CreateVolume("3", new List<Chapter>()
                {
                    EntityFactory.CreateChapter("0", false, new List<MangaFile>()),
                }),
            }
        });

        await _context.SaveChangesAsync();

        var detail = await _seriesService.GetSeriesDetail(1, 1);
        Assert.NotEmpty(detail.Volumes);

        Assert.Empty(detail.Chapters); // A book library where all books are Volumes, will show no "chapters" on the UI because it doesn't make sense
        Assert.Equal(2, detail.Volumes.Count());
    }

    [Fact]
    public async Task SeriesDetail_WhenBookLibrary_ShouldReturnVolumesAndSpecial()
    {
        await ResetDb();

        _context.Series.Add(new Series()
        {
            Name = "Test",
            Library = new Library() {
                Name = "Test LIb",
                Type = LibraryType.Book,
            },
            Volumes = new List<Volume>()
            {
                EntityFactory.CreateVolume("0", new List<Chapter>()
                {
                    EntityFactory.CreateChapter("Ano Orokamono ni mo Kyakkou wo! - Volume 1.epub", true, new List<MangaFile>()),
                }),
                EntityFactory.CreateVolume("2", new List<Chapter>()
                {
                    EntityFactory.CreateChapter("Ano Orokamono ni mo Kyakkou wo! - Volume 2.epub", false, new List<MangaFile>()),
                }),
            }
        });

        await _context.SaveChangesAsync();

        var detail = await _seriesService.GetSeriesDetail(1, 1);
        Assert.NotEmpty(detail.Volumes);
        Assert.Equal("2 - Ano Orokamono ni mo Kyakkou wo! - Volume 2", detail.Volumes.ElementAt(0).Name);

        Assert.NotEmpty(detail.Specials);
        Assert.Equal("Ano Orokamono ni mo Kyakkou wo! - Volume 1.epub", detail.Specials.ElementAt(0).Range);

        // A book library where all books are Volumes, will show no "chapters" on the UI because it doesn't make sense
        Assert.Empty(detail.Chapters);

        Assert.Equal(1, detail.Volumes.Count());
    }

    [Fact]
    public async Task SeriesDetail_ShouldSortVolumesByName()
    {
        await ResetDb();

        _context.Series.Add(new Series()
        {
            Name = "Test",
            Library = new Library() {
                Name = "Test LIb",
                Type = LibraryType.Book,
            },
            Volumes = new List<Volume>()
            {
                EntityFactory.CreateVolume("2", new List<Chapter>()
                {
                    EntityFactory.CreateChapter("0", false, new List<MangaFile>()),
                }),
                EntityFactory.CreateVolume("1.2", new List<Chapter>()
                {
                    EntityFactory.CreateChapter("0", false, new List<MangaFile>()),
                }),
                EntityFactory.CreateVolume("1", new List<Chapter>()
                {
                    EntityFactory.CreateChapter("0", false, new List<MangaFile>()),
                }),
            }
        });

        await _context.SaveChangesAsync();

        var detail = await _seriesService.GetSeriesDetail(1, 1);
        Assert.Equal("1", detail.Volumes.ElementAt(0).Name);
        Assert.Equal("1.2", detail.Volumes.ElementAt(1).Name);
        Assert.Equal("2", detail.Volumes.ElementAt(2).Name);
    }


    #endregion


    #region UpdateRating

    [Fact]
    public async Task UpdateRating_ShouldSetRating()
    {
        await ResetDb();

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


        var user = await _unitOfWork.UserRepository.GetUserByUsernameAsync("majora2007", AppUserIncludes.Ratings);

        var result = await _seriesService.UpdateRating(user, new UpdateSeriesRatingDto()
        {
            SeriesId = 1,
            UserRating = 3,
            UserReview = "Average"
        });

        Assert.True(result);

        var ratings = (await _unitOfWork.UserRepository.GetUserByUsernameAsync("majora2007", AppUserIncludes.Ratings))
            .Ratings;
        Assert.NotEmpty(ratings);
        Assert.Equal(3, ratings.First().Rating);
        Assert.Equal("Average", ratings.First().Review);
    }

    [Fact]
    public async Task UpdateRating_ShouldUpdateExistingRating()
    {
        await ResetDb();

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

        var user = await _unitOfWork.UserRepository.GetUserByUsernameAsync("majora2007", AppUserIncludes.Ratings);

        var result = await _seriesService.UpdateRating(user, new UpdateSeriesRatingDto()
        {
            SeriesId = 1,
            UserRating = 3,
            UserReview = "Average"
        });

        Assert.True(result);

        var ratings = (await _unitOfWork.UserRepository.GetUserByUsernameAsync("majora2007", AppUserIncludes.Ratings))
            .Ratings;
        Assert.NotEmpty(ratings);
        Assert.Equal(3, ratings.First().Rating);
        Assert.Equal("Average", ratings.First().Review);

        // Update the DB again

        var result2 = await _seriesService.UpdateRating(user, new UpdateSeriesRatingDto()
        {
            SeriesId = 1,
            UserRating = 5,
            UserReview = "Average"
        });

        Assert.True(result2);

        var ratings2 = (await _unitOfWork.UserRepository.GetUserByUsernameAsync("majora2007", AppUserIncludes.Ratings))
            .Ratings;
        Assert.NotEmpty(ratings2);
        Assert.True(ratings2.Count == 1);
        Assert.Equal(5, ratings2.First().Rating);
        Assert.Equal("Average", ratings2.First().Review);
    }

    [Fact]
    public async Task UpdateRating_ShouldClampRatingAt5()
    {
        await ResetDb();

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

        var user = await _unitOfWork.UserRepository.GetUserByUsernameAsync("majora2007", AppUserIncludes.Ratings);

        var result = await _seriesService.UpdateRating(user, new UpdateSeriesRatingDto()
        {
            SeriesId = 1,
            UserRating = 10,
            UserReview = "Average"
        });

        Assert.True(result);

        var ratings = (await _unitOfWork.UserRepository.GetUserByUsernameAsync("majora2007", AppUserIncludes.Ratings))
            .Ratings;
        Assert.NotEmpty(ratings);
        Assert.Equal(5, ratings.First().Rating);
        Assert.Equal("Average", ratings.First().Review);
    }

    [Fact]
    public async Task UpdateRating_ShouldReturnFalseWhenSeriesDoesntExist()
    {
        await ResetDb();

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

        var user = await _unitOfWork.UserRepository.GetUserByUsernameAsync("majora2007", AppUserIncludes.Ratings);

        var result = await _seriesService.UpdateRating(user, new UpdateSeriesRatingDto()
        {
            SeriesId = 2,
            UserRating = 5,
            UserReview = "Average"
        });

        Assert.False(result);

        var ratings = user.Ratings;
        Assert.Empty(ratings);
    }

    #endregion
}
