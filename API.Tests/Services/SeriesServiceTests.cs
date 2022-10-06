using System.Collections.Generic;
using System.Data.Common;
using System.IO.Abstractions.TestingHelpers;
using System.Linq;
using System.Threading.Tasks;
using API.Data;
using API.Data.Repositories;
using API.DTOs;
using API.DTOs.CollectionTags;
using API.DTOs.Metadata;
using API.DTOs.Reader;
using API.Entities;
using API.Entities.Enums;
using API.Extensions;
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
using NSubstitute.Extensions;
using NSubstitute.ReceivedExtensions;
using Xunit;
using Xunit.Sdk;

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

        // var lib = new Library()
        // {
        //     Name = "Manga", Folders = new List<FolderPath>() {new FolderPath() {Path = "C:/data/"}}
        // };
        //
        // _context.AppUser.Add(new AppUser()
        // {
        //     UserName = "majora2007",
        //     Libraries = new List<Library>()
        //     {
        //         lib
        //     }
        // });

        return await _context.SaveChangesAsync() > 0;
    }

    private async Task ResetDb()
    {
        _context.Series.RemoveRange(_context.Series.ToList());
        _context.AppUserRating.RemoveRange(_context.AppUserRating.ToList());
        _context.Genre.RemoveRange(_context.Genre.ToList());
        _context.CollectionTag.RemoveRange(_context.CollectionTag.ToList());
        _context.Person.RemoveRange(_context.Person.ToList());
        _context.Library.RemoveRange(_context.Library.ToList());

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

        _context.Library.Add(new Library()
        {
            AppUsers = new List<AppUser>()
            {
                new AppUser()
                {
                    UserName = "majora2007"
                }
            },
            Name = "Test LIb",
            Type = LibraryType.Book,
            Series = new List<Series>()
            {
                new Series()
                {
                    Name = "Test",
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
                }
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

        _context.Library.Add(new Library()
        {
            AppUsers = new List<AppUser>()
            {
                new AppUser()
                {
                    UserName = "majora2007"
                }
            },
            Name = "Test LIb",
            Type = LibraryType.Manga,
            Series = new List<Series>()
            {
                new Series()
                {
                    Name = "Test",
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
                }
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

        _context.Library.Add(new Library()
        {
            AppUsers = new List<AppUser>()
            {
                new AppUser()
                {
                    UserName = "majora2007"
                }
            },
            Name = "Test LIb",
            Type = LibraryType.Manga,
            Series = new List<Series>()
            {
                new Series()
                {
                    Name = "Test",
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
                }
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
    public async Task SeriesDetail_ShouldReturnCorrectNaming_VolumeTitle()
    {
        await ResetDb();

        _context.Library.Add(new Library()
        {
            AppUsers = new List<AppUser>()
            {
                new AppUser()
                {
                    UserName = "majora2007"
                }
            },
            Name = "Test LIb",
            Type = LibraryType.Manga,
            Series = new List<Series>()
            {
                new Series()
                {
                    Name = "Test",
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
                }
            }
        });

        await _context.SaveChangesAsync();

        var detail = await _seriesService.GetSeriesDetail(1, 1);
        Assert.NotEmpty(detail.Chapters);
        // volume 2 has a 0 chapter aka a single chapter that is represented as a volume. We don't show in Chapters area
        Assert.Equal(3, detail.Chapters.Count());

        Assert.NotEmpty(detail.Volumes);
        Assert.Equal(2, detail.Volumes.Count());

        Assert.Equal(string.Empty, detail.Chapters.First().VolumeTitle); // loose leaf chapter
        Assert.Equal("Volume 3", detail.Chapters.Last().VolumeTitle); // volume based chapter
    }

    [Fact]
    public async Task SeriesDetail_ShouldReturnChaptersOnly_WhenBookLibrary()
    {
        await ResetDb();

        _context.Library.Add(new Library()
        {
            AppUsers = new List<AppUser>()
            {
                new AppUser()
                {
                    UserName = "majora2007"
                }
            },
            Name = "Test LIb",
            Type = LibraryType.Book,
            Series = new List<Series>()
            {
                new Series()
                {
                    Name = "Test",
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
                }
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

        _context.Library.Add(new Library()
        {
            AppUsers = new List<AppUser>()
            {
                new AppUser()
                {
                    UserName = "majora2007"
                }
            },
            Name = "Test LIb",
            Type = LibraryType.Book,
            Series = new List<Series>()
            {
                new Series()
                {
                    Name = "Test",
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
                }
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

        _context.Library.Add(new Library()
        {
            AppUsers = new List<AppUser>()
            {
                new AppUser()
                {
                    UserName = "majora2007"
                }
            },
            Name = "Test LIb",
            Type = LibraryType.Manga,
            Series = new List<Series>()
            {
                new Series()
                {
                    Name = "Test",
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
                }
            }
        });


        await _context.SaveChangesAsync();

        var detail = await _seriesService.GetSeriesDetail(1, 1);
        Assert.Equal("Volume 1", detail.Volumes.ElementAt(0).Name);
        Assert.Equal("Volume 1.2", detail.Volumes.ElementAt(1).Name);
        Assert.Equal("Volume 2", detail.Volumes.ElementAt(2).Name);
    }


    #endregion


    #region UpdateRating

    [Fact]
    public async Task UpdateRating_ShouldSetRating()
    {
        await ResetDb();

        _context.Library.Add(new Library()
        {
            AppUsers = new List<AppUser>()
            {
                new AppUser()
                {
                    UserName = "majora2007"
                }
            },
            Name = "Test LIb",
            Type = LibraryType.Manga,
            Series = new List<Series>()
            {
                new Series()
                {
                    Name = "Test",
                    Volumes = new List<Volume>()
                    {
                        EntityFactory.CreateVolume("1", new List<Chapter>()
                        {
                            EntityFactory.CreateChapter("1", false, new List<MangaFile>(), 1),
                        }),
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

        _context.Library.Add(new Library()
        {
            AppUsers = new List<AppUser>()
            {
                new AppUser()
                {
                    UserName = "majora2007"
                }
            },
            Name = "Test LIb",
            Type = LibraryType.Manga,
            Series = new List<Series>()
            {
                new Series()
                {
                    Name = "Test",
                    Volumes = new List<Volume>()
                    {
                        EntityFactory.CreateVolume("1", new List<Chapter>()
                        {
                            EntityFactory.CreateChapter("1", false, new List<MangaFile>(), 1),
                        }),
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

        _context.Library.Add(new Library()
        {
            AppUsers = new List<AppUser>()
            {
                new AppUser()
                {
                    UserName = "majora2007"
                }
            },
            Name = "Test LIb",
            Type = LibraryType.Manga,
            Series = new List<Series>()
            {
                new Series()
                {
                    Name = "Test",
                    Volumes = new List<Volume>()
                    {
                        EntityFactory.CreateVolume("1", new List<Chapter>()
                        {
                            EntityFactory.CreateChapter("1", false, new List<MangaFile>(), 1),
                        }),
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

        _context.Library.Add(new Library()
        {
            AppUsers = new List<AppUser>()
            {
                new AppUser()
                {
                    UserName = "majora2007"
                }
            },
            Name = "Test LIb",
            Type = LibraryType.Manga,
            Series = new List<Series>()
            {
                new Series()
                {
                    Name = "Test",
                    Volumes = new List<Volume>()
                    {
                        EntityFactory.CreateVolume("1", new List<Chapter>()
                        {
                            EntityFactory.CreateChapter("1", false, new List<MangaFile>(), 1),
                        }),
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

    #region UpdateSeriesMetadata

    [Fact]
    public async Task UpdateSeriesMetadata_ShouldCreateEmptyMetadata_IfDoesntExist()
    {
        await ResetDb();
        _context.Series.Add(new Series()
        {
            Name = "Test",
            Library = new Library() {
                Name = "Test LIb",
                Type = LibraryType.Book,
            }
        });
        await _context.SaveChangesAsync();

        var success = await _seriesService.UpdateSeriesMetadata(new UpdateSeriesMetadataDto()
        {
            SeriesMetadata = new SeriesMetadataDto()
            {
                SeriesId = 1,
                Genres = new List<GenreTagDto>() {new GenreTagDto() {Id = 0, Title = "New Genre"}}
            },
            CollectionTags = new List<CollectionTagDto>()
        });

        Assert.True(success);

        var series = await _unitOfWork.SeriesRepository.GetSeriesByIdAsync(1);
        Assert.NotNull(series.Metadata);
        Assert.True(series.Metadata.Genres.Select(g => g.Title).Contains("New Genre".SentenceCase()));

    }

    [Fact]
    public async Task UpdateSeriesMetadata_ShouldCreateNewTags_IfNoneExist()
    {
        await ResetDb();
        _context.Series.Add(new Series()
        {
            Name = "Test",
            Library = new Library() {
                Name = "Test LIb",
                Type = LibraryType.Book,
            }
        });
        await _context.SaveChangesAsync();

        var success = await _seriesService.UpdateSeriesMetadata(new UpdateSeriesMetadataDto()
        {
            SeriesMetadata = new SeriesMetadataDto()
            {
                SeriesId = 1,
                Genres = new List<GenreTagDto>() {new GenreTagDto() {Id = 0, Title = "New Genre"}},
                Tags = new List<TagDto>() {new TagDto() {Id = 0, Title = "New Tag"}},
                Characters = new List<PersonDto>() {new PersonDto() {Id = 0, Name = "Joe Shmo", Role = PersonRole.Character}},
                Colorists = new List<PersonDto>() {new PersonDto() {Id = 0, Name = "Joe Shmo", Role = PersonRole.Colorist}},
                Pencillers = new List<PersonDto>() {new PersonDto() {Id = 0, Name = "Joe Shmo 2", Role = PersonRole.Penciller}},
            },
            CollectionTags = new List<CollectionTagDto>()
            {
                new CollectionTagDto() {Id = 0, Promoted = false, Summary = string.Empty, CoverImageLocked = false, Title = "New Collection"}
            }
        });

        Assert.True(success);

        var series = await _unitOfWork.SeriesRepository.GetSeriesByIdAsync(1);
        Assert.NotNull(series.Metadata);
        Assert.True(series.Metadata.Genres.Select(g => g.Title).Contains("New Genre".SentenceCase()));
        Assert.True(series.Metadata.People.All(g => g.Name is "Joe Shmo" or "Joe Shmo 2"));
        Assert.True(series.Metadata.Tags.Select(g => g.Title).Contains("New Tag".SentenceCase()));
        Assert.True(series.Metadata.CollectionTags.Select(g => g.Title).Contains("New Collection"));

    }

    [Fact]
    public async Task UpdateSeriesMetadata_ShouldRemoveExistingTags()
    {
        await ResetDb();
        var s = new Series()
        {
            Name = "Test",
            Library = new Library()
            {
                Name = "Test LIb",
                Type = LibraryType.Book,
            },
            Metadata = DbFactory.SeriesMetadata(new List<CollectionTag>())
        };
        var g = DbFactory.Genre("Existing Genre", false);
        s.Metadata.Genres = new List<Genre>() {g};
        _context.Series.Add(s);

        _context.Genre.Add(g);
        await _context.SaveChangesAsync();

        var success = await _seriesService.UpdateSeriesMetadata(new UpdateSeriesMetadataDto()
        {
            SeriesMetadata = new SeriesMetadataDto()
            {
                SeriesId = 1,
                Genres = new List<GenreTagDto>() {new () {Id = 0, Title = "New Genre"}},
            },
            CollectionTags = new List<CollectionTagDto>()
        });

        Assert.True(success);

        var series = await _unitOfWork.SeriesRepository.GetSeriesByIdAsync(1);
        Assert.NotNull(series.Metadata);
        Assert.True(series.Metadata.Genres.Select(g1 => g1.Title).All(g2 => g2 == "New Genre".SentenceCase()));
        Assert.False(series.Metadata.GenresLocked); // GenreLocked is false unless the UI Explicitly says it should be locked
    }

    [Fact]
    public async Task UpdateSeriesMetadata_ShouldAddNewPerson_NoExistingPeople()
    {
        await ResetDb();
        var s = new Series()
        {
            Name = "Test",
            Library = new Library()
            {
                Name = "Test LIb",
                Type = LibraryType.Book,
            },
            Metadata = DbFactory.SeriesMetadata(new List<CollectionTag>())
        };
        var g = DbFactory.Person("Existing Person", PersonRole.Publisher);
        _context.Series.Add(s);

        _context.Person.Add(g);
        await _context.SaveChangesAsync();

        var success = await _seriesService.UpdateSeriesMetadata(new UpdateSeriesMetadataDto()
        {
            SeriesMetadata = new SeriesMetadataDto()
            {
                SeriesId = 1,
                Publishers = new List<PersonDto>() {new () {Id = 0, Name = "Existing Person", Role = PersonRole.Publisher}},
            },
            CollectionTags = new List<CollectionTagDto>()
        });

        Assert.True(success);

        var series = await _unitOfWork.SeriesRepository.GetSeriesByIdAsync(1);
        Assert.NotNull(series.Metadata);
        Assert.True(series.Metadata.People.Select(g => g.Name).All(g => g == "Existing Person"));
        Assert.False(series.Metadata.PublisherLocked); // PublisherLocked is false unless the UI Explicitly says it should be locked
    }

    [Fact]
    public async Task UpdateSeriesMetadata_ShouldAddNewPerson_ExistingPeople()
    {
        await ResetDb();
        var s = new Series()
        {
            Name = "Test",
            Library = new Library()
            {
                Name = "Test LIb",
                Type = LibraryType.Book,
            },
            Metadata = DbFactory.SeriesMetadata(new List<CollectionTag>())
        };
        var g = DbFactory.Person("Existing Person", PersonRole.Publisher);
        s.Metadata.People = new List<Person>() {DbFactory.Person("Existing Writer", PersonRole.Writer),
            DbFactory.Person("Existing Translator", PersonRole.Translator), DbFactory.Person("Existing Publisher 2", PersonRole.Publisher)};
        _context.Series.Add(s);

        _context.Person.Add(g);
        await _context.SaveChangesAsync();

        var success = await _seriesService.UpdateSeriesMetadata(new UpdateSeriesMetadataDto()
        {
            SeriesMetadata = new SeriesMetadataDto()
            {
                SeriesId = 1,
                Publishers = new List<PersonDto>() {new () {Id = 0, Name = "Existing Person", Role = PersonRole.Publisher}},
                PublishersLocked = true
            },
            CollectionTags = new List<CollectionTagDto>()
        });

        Assert.True(success);

        var series = await _unitOfWork.SeriesRepository.GetSeriesByIdAsync(1);
        Assert.NotNull(series.Metadata);
        Assert.True(series.Metadata.People.Select(g => g.Name).All(g => g == "Existing Person"));
        Assert.True(series.Metadata.PublisherLocked);
    }


    [Fact]
    public async Task UpdateSeriesMetadata_ShouldRemoveExistingPerson()
    {
        await ResetDb();
        var s = new Series()
        {
            Name = "Test",
            Library = new Library()
            {
                Name = "Test LIb",
                Type = LibraryType.Book,
            },
            Metadata = DbFactory.SeriesMetadata(new List<CollectionTag>())
        };
        var g = DbFactory.Person("Existing Person", PersonRole.Publisher);
        _context.Series.Add(s);

        _context.Person.Add(g);
        await _context.SaveChangesAsync();

        var success = await _seriesService.UpdateSeriesMetadata(new UpdateSeriesMetadataDto()
        {
            SeriesMetadata = new SeriesMetadataDto()
            {
                SeriesId = 1,
                Publishers = new List<PersonDto>() {},
            },
            CollectionTags = new List<CollectionTagDto>()
        });

        Assert.True(success);

        var series = await _unitOfWork.SeriesRepository.GetSeriesByIdAsync(1);
        Assert.NotNull(series.Metadata);
        Assert.False(series.Metadata.People.Any());
    }

    [Fact]
    public async Task UpdateSeriesMetadata_ShouldLockIfTold()
    {
        await ResetDb();
        var s = new Series()
        {
            Name = "Test",
            Library = new Library()
            {
                Name = "Test LIb",
                Type = LibraryType.Book,
            },
            Metadata = DbFactory.SeriesMetadata(new List<CollectionTag>())
        };
        var g = DbFactory.Genre("Existing Genre", false);
        s.Metadata.Genres = new List<Genre>() {g};
        s.Metadata.GenresLocked = true;
        _context.Series.Add(s);

        _context.Genre.Add(g);
        await _context.SaveChangesAsync();

        var success = await _seriesService.UpdateSeriesMetadata(new UpdateSeriesMetadataDto()
        {
            SeriesMetadata = new SeriesMetadataDto()
            {
                SeriesId = 1,
                Genres = new List<GenreTagDto>() {new () {Id = 1, Title = "Existing Genre"}},
                GenresLocked = true
            },
            CollectionTags = new List<CollectionTagDto>()
        });

        Assert.True(success);

        var series = await _unitOfWork.SeriesRepository.GetSeriesByIdAsync(1);
        Assert.NotNull(series.Metadata);
        Assert.True(series.Metadata.Genres.Select(g => g.Title).All(g => g == "Existing Genre".SentenceCase()));
        Assert.True(series.Metadata.GenresLocked);
    }

    [Fact]
    public async Task UpdateSeriesMetadata_ShouldNotUpdateReleaseYear_IfLessThan1000()
    {
        await ResetDb();
        var s = new Series()
        {
            Name = "Test",
            Library = new Library()
            {
                Name = "Test LIb",
                Type = LibraryType.Book,
            },
            Metadata = DbFactory.SeriesMetadata(new List<CollectionTag>())
        };
        _context.Series.Add(s);
        await _context.SaveChangesAsync();

        var success = await _seriesService.UpdateSeriesMetadata(new UpdateSeriesMetadataDto()
        {
            SeriesMetadata = new SeriesMetadataDto()
            {
                SeriesId = 1,
                ReleaseYear = 100,
            },
            CollectionTags = new List<CollectionTagDto>()
        });

        Assert.True(success);

        var series = await _unitOfWork.SeriesRepository.GetSeriesByIdAsync(1);
        Assert.NotNull(series.Metadata);
        Assert.Equal(0, series.Metadata.ReleaseYear);
        Assert.False(series.Metadata.ReleaseYearLocked);
    }

    #endregion

    #region GetFirstChapterForMetadata

    private static Series CreateSeriesMock()
    {
        var files = new List<MangaFile>()
        {
            EntityFactory.CreateMangaFile("Test.cbz", MangaFormat.Archive, 1)
        };
        return new Series()
        {
            Name = "Test",
            Library = new Library()
            {
                Name = "Test LIb",
                Type = LibraryType.Manga,
            },
            Volumes = new List<Volume>()
            {
                EntityFactory.CreateVolume("0", new List<Chapter>()
                {
                    EntityFactory.CreateChapter("95", false, files, 1),
                    EntityFactory.CreateChapter("96", false, files, 1),
                    EntityFactory.CreateChapter("A Special Case", true, files, 1),
                }),
                EntityFactory.CreateVolume("1", new List<Chapter>()
                {
                    EntityFactory.CreateChapter("1", false, files, 1),
                    EntityFactory.CreateChapter("2", false, files, 1),
                }),
                EntityFactory.CreateVolume("2", new List<Chapter>()
                {
                    EntityFactory.CreateChapter("21", false, files, 1),
                    EntityFactory.CreateChapter("22", false, files, 1),
                }),
                EntityFactory.CreateVolume("3", new List<Chapter>()
                {
                    EntityFactory.CreateChapter("31", false, files, 1),
                    EntityFactory.CreateChapter("32", false, files, 1),
                }),
            }
        };
    }

    [Fact]
    public void GetFirstChapterForMetadata_Book_Test()
    {
        var series = CreateSeriesMock();

        var firstChapter = SeriesService.GetFirstChapterForMetadata(series, true);
        Assert.Same("1", firstChapter.Range);
    }

    [Fact]
    public void GetFirstChapterForMetadata_NonBook_ShouldReturnVolume1()
    {
        var series = CreateSeriesMock();

        var firstChapter = SeriesService.GetFirstChapterForMetadata(series, false);
        Assert.Same("1", firstChapter.Range);
    }

    [Fact]
    public void GetFirstChapterForMetadata_NonBook_ShouldReturnVolume1_WhenFirstChapterIsFloat()
    {
        var series = CreateSeriesMock();
        var files = new List<MangaFile>()
        {
            EntityFactory.CreateMangaFile("Test.cbz", MangaFormat.Archive, 1)
        };
        series.Volumes[1].Chapters = new List<Chapter>()
        {
            EntityFactory.CreateChapter("2", false, files, 1),
            EntityFactory.CreateChapter("1.1", false, files, 1),
            EntityFactory.CreateChapter("1.2", false, files, 1),
        };

        var firstChapter = SeriesService.GetFirstChapterForMetadata(series, false);
        Assert.Same("1.1", firstChapter.Range);
    }

    #endregion
}
