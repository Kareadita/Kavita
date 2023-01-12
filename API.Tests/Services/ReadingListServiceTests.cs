using System.Collections.Generic;
using System.Data.Common;
using System.IO.Abstractions.TestingHelpers;
using System.Linq;
using System.Threading.Tasks;
using API.Data;
using API.Data.Repositories;
using API.DTOs.ReadingLists;
using API.Entities;
using API.Entities.Enums;
using API.Helpers;
using API.Services;
using API.SignalR;
using AutoMapper;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using NSubstitute;
using Xunit;

namespace API.Tests.Services;

public class ReadingListServiceTests
{
    private readonly IUnitOfWork _unitOfWork;
    private readonly IReadingListService _readingListService;

    private readonly DataContext _context;

    private const string CacheDirectory = "C:/kavita/config/cache/";
    private const string CoverImageDirectory = "C:/kavita/config/covers/";
    private const string BackupDirectory = "C:/kavita/config/backups/";
    private const string DataDirectory = "C:/data/";

    public ReadingListServiceTests()
    {
        var contextOptions = new DbContextOptionsBuilder().UseSqlite(CreateInMemoryDatabase()).Options;

        _context = new DataContext(contextOptions);
        Task.Run(SeedDb).GetAwaiter().GetResult();

        var config = new MapperConfiguration(cfg => cfg.AddProfile<AutoMapperProfiles>());
        var mapper = config.CreateMapper();
        _unitOfWork = new UnitOfWork(_context, mapper, null);

        _readingListService = new ReadingListService(_unitOfWork, Substitute.For<ILogger<ReadingListService>>());
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
        _context.AppUser.RemoveRange(_context.AppUser);
        _context.Series.RemoveRange(_context.Series);
        _context.ReadingList.RemoveRange(_context.ReadingList);
        await _unitOfWork.CommitAsync();
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

    #region UpdateReadingListItemPosition

    [Fact]
    public async Task UpdateReadingListItemPosition_MoveLastToFirst_TwoItemsShouldShift()
    {
        await ResetDb();
        _context.AppUser.Add(new AppUser()
        {
            UserName = "majora2007",
            ReadingLists = new List<ReadingList>(),
            Libraries = new List<Library>()
            {
                new Library()
                {
                    Name = "Test LIb",
                    Type = LibraryType.Book,
                    Series = new List<Series>()
                    {
                        new Series()
                        {
                            Name = "Test",
                            Metadata = DbFactory.SeriesMetadata(new List<CollectionTag>()),
                            Volumes = new List<Volume>()
                            {
                                new Volume()
                                {
                                    Name = "0",
                                    Chapters = new List<Chapter>()
                                    {
                                        new Chapter()
                                        {
                                            Number = "1",
                                            AgeRating = AgeRating.Everyone,
                                        },
                                        new Chapter()
                                        {
                                            Number = "2",
                                            AgeRating = AgeRating.X18Plus
                                        },
                                        new Chapter()
                                        {
                                            Number = "3",
                                            AgeRating = AgeRating.X18Plus
                                        }
                                    }
                                }
                            }
                        }
                    }
                },
            }
        });

        await _context.SaveChangesAsync();

        var user = await _unitOfWork.UserRepository.GetUserByUsernameAsync("majora2007", AppUserIncludes.ReadingLists);
        var readingList = new ReadingList();
        user.ReadingLists = new List<ReadingList>()
        {
            readingList
        };

        await _readingListService.AddChaptersToReadingList(1, new List<int>() {1, 2, 3}, readingList);
        await _unitOfWork.CommitAsync();
        Assert.Equal(3, readingList.Items.Count);

        await _readingListService.UpdateReadingListItemPosition(new UpdateReadingListPosition()
        {
            FromPosition = 2, ToPosition = 0, ReadingListId = 1, ReadingListItemId = 3
        });


        Assert.Equal(3, readingList.Items.Count);
        Assert.Equal(0, readingList.Items.Single(i => i.ChapterId == 3).Order);
        Assert.Equal(1, readingList.Items.Single(i => i.ChapterId == 1).Order);
        Assert.Equal(2, readingList.Items.Single(i => i.ChapterId == 2).Order);
    }

    [Fact]
    public async Task UpdateReadingListItemPosition_MoveLastToFirst_TwoItemsShouldShift_ThenDeleteSecond_OrderShouldBeCorrect()
    {
        await ResetDb();
        _context.AppUser.Add(new AppUser()
        {
            UserName = "majora2007",
            ReadingLists = new List<ReadingList>(),
            Libraries = new List<Library>()
            {
                new Library()
                {
                    Name = "Test LIb",
                    Type = LibraryType.Book,
                    Series = new List<Series>()
                    {
                        new Series()
                        {
                            Name = "Test",
                            Metadata = DbFactory.SeriesMetadata(new List<CollectionTag>()),
                            Volumes = new List<Volume>()
                            {
                                new Volume()
                                {
                                    Name = "0",
                                    Chapters = new List<Chapter>()
                                    {
                                        new Chapter()
                                        {
                                            Number = "1",
                                            AgeRating = AgeRating.Everyone,
                                        },
                                        new Chapter()
                                        {
                                            Number = "2",
                                            AgeRating = AgeRating.X18Plus
                                        },
                                        new Chapter()
                                        {
                                            Number = "3",
                                            AgeRating = AgeRating.X18Plus
                                        }
                                    }
                                }
                            }
                        }
                    }
                },
            }
        });

        await _context.SaveChangesAsync();

        var user = await _unitOfWork.UserRepository.GetUserByUsernameAsync("majora2007", AppUserIncludes.ReadingLists);
        var readingList = new ReadingList();
        user.ReadingLists = new List<ReadingList>()
        {
            readingList
        };

        // Existing (order, chapterId): (0, 1), (1, 2), (2, 3)
        await _readingListService.AddChaptersToReadingList(1, new List<int>() {1, 2, 3}, readingList);
        await _unitOfWork.CommitAsync();
        Assert.Equal(3, readingList.Items.Count);

        // From 3 to 1
        // New (order, chapterId): (0, 3), (1, 2), (2, 1)
        await _readingListService.UpdateReadingListItemPosition(new UpdateReadingListPosition()
        {
            FromPosition = 2, ToPosition = 0, ReadingListId = 1, ReadingListItemId = 3
        });



        Assert.Equal(3, readingList.Items.Count);
        Assert.Equal(0, readingList.Items.Single(i => i.ChapterId == 3).Order);
        Assert.Equal(1, readingList.Items.Single(i => i.ChapterId == 1).Order);
        Assert.Equal(2, readingList.Items.Single(i => i.ChapterId == 2).Order);

        // New (order, chapterId): (0, 3), (2, 1): Delete 2nd item
        await _readingListService.DeleteReadingListItem(new UpdateReadingListPosition()
        {
            ReadingListId = 1, ReadingListItemId = readingList.Items.Single(i => i.ChapterId == 2).Id
        });

        Assert.Equal(2, readingList.Items.Count);
        Assert.Equal(0, readingList.Items.Single(i => i.ChapterId == 3).Order);
        Assert.Equal(1, readingList.Items.Single(i => i.ChapterId == 1).Order);
    }


    #endregion

    #region DeleteReadingListItem

    [Fact]
    public async Task DeleteReadingListItem_DeleteFirstItem_SecondShouldBecomeFirst()
    {
        await ResetDb();
        _context.AppUser.Add(new AppUser()
        {
            UserName = "majora2007",
            ReadingLists = new List<ReadingList>(),
            Libraries = new List<Library>()
            {
                new Library()
                {
                    Name = "Test LIb",
                    Type = LibraryType.Book,
                    Series = new List<Series>()
                    {
                        new Series()
                        {
                            Name = "Test",
                            Metadata = DbFactory.SeriesMetadata(new List<CollectionTag>()),
                            Volumes = new List<Volume>()
                            {
                                new Volume()
                                {
                                    Name = "0",
                                    Chapters = new List<Chapter>()
                                    {
                                        new Chapter()
                                        {
                                            Number = "1",
                                            AgeRating = AgeRating.Everyone
                                        },
                                        new Chapter()
                                        {
                                            Number = "2",
                                            AgeRating = AgeRating.X18Plus
                                        }
                                    }
                                }
                            }
                        }
                    }
                },
            }
        });

        await _context.SaveChangesAsync();

        var user = await _unitOfWork.UserRepository.GetUserByUsernameAsync("majora2007", AppUserIncludes.ReadingLists);
        var readingList = new ReadingList();
        user.ReadingLists = new List<ReadingList>()
        {
            readingList
        };

        await _readingListService.AddChaptersToReadingList(1, new List<int>() {1, 2}, readingList);
        await _unitOfWork.CommitAsync();
        Assert.Equal(2, readingList.Items.Count);

        await _readingListService.DeleteReadingListItem(new UpdateReadingListPosition()
        {
            ReadingListId = 1, ReadingListItemId = 1
        });

        Assert.Equal(1, readingList.Items.Count);
        Assert.Equal(2, readingList.Items.First().ChapterId);
    }

    #endregion

    #region RemoveFullyReadItems

    [Fact]
    public async Task RemoveFullyReadItems_RemovesAllFullyReadItems()
    {
        await ResetDb();
        _context.AppUser.Add(new AppUser()
        {
            UserName = "majora2007",
            ReadingLists = new List<ReadingList>(),
            Libraries = new List<Library>()
            {
                new Library()
                {
                    Name = "Test LIb",
                    Type = LibraryType.Book,
                    Series = new List<Series>()
                    {
                        new Series()
                        {
                            Name = "Test",
                            Metadata = DbFactory.SeriesMetadata(new List<CollectionTag>()),
                            Volumes = new List<Volume>()
                            {
                                new Volume()
                                {
                                    Name = "0",
                                    Chapters = new List<Chapter>()
                                    {
                                        new Chapter()
                                        {
                                            Number = "1",
                                            AgeRating = AgeRating.Everyone,
                                            Pages = 1
                                        },
                                        new Chapter()
                                        {
                                            Number = "2",
                                            AgeRating = AgeRating.X18Plus,
                                            Pages = 1
                                        },
                                        new Chapter()
                                        {
                                            Number = "3",
                                            AgeRating = AgeRating.X18Plus,
                                            Pages = 1
                                        }
                                    }
                                }
                            }
                        }
                    }
                },
            }
        });

        await _context.SaveChangesAsync();

        var user = await _unitOfWork.UserRepository.GetUserByUsernameAsync("majora2007", AppUserIncludes.ReadingLists | AppUserIncludes.Progress);
        var readingList = new ReadingList();
        user.ReadingLists = new List<ReadingList>()
        {
            readingList
        };

        await _readingListService.AddChaptersToReadingList(1, new List<int>() {1, 2, 3}, readingList);
        await _unitOfWork.CommitAsync();
        Assert.Equal(3, readingList.Items.Count);

        var readerService = new ReaderService(_unitOfWork, Substitute.For<ILogger<ReaderService>>(),
            Substitute.For<IEventHub>());
        // Mark 2 as fully read
        await readerService.MarkChaptersAsRead(user, 1,
            (await _unitOfWork.ChapterRepository.GetChaptersByIdsAsync(new List<int>() {2})).ToList());
        await _unitOfWork.CommitAsync();

        await _readingListService.RemoveFullyReadItems(1, user);


        Assert.Equal(2, readingList.Items.Count);
        Assert.DoesNotContain(readingList.Items, i => i.Id == 2);
    }


    #endregion


    #region CalculateAgeRating

    [Fact]
    public async Task CalculateAgeRating_ShouldUpdateToUnknown_IfNoneSet()
    {
        await ResetDb();
        _context.AppUser.Add(new AppUser()
        {
            UserName = "majora2007",
            ReadingLists = new List<ReadingList>(),
            Libraries = new List<Library>()
            {
                new Library()
                {
                    Name = "Test LIb",
                    Type = LibraryType.Book,
                    Series = new List<Series>()
                    {
                        new Series()
                        {
                            Name = "Test",
                            Metadata = DbFactory.SeriesMetadata(new List<CollectionTag>()),
                            Volumes = new List<Volume>()
                            {
                                new Volume()
                                {
                                    Name = "0",
                                    Chapters = new List<Chapter>()
                                    {
                                        new Chapter()
                                        {
                                            Number = "1",
                                        },
                                        new Chapter()
                                        {
                                            Number = "2",
                                        }
                                    }
                                }
                            }
                        }
                    }
                },
            }
        });

        await _context.SaveChangesAsync();

        var user = await _unitOfWork.UserRepository.GetUserByUsernameAsync("majora2007", AppUserIncludes.ReadingLists);
        var readingList = new ReadingList();
        user.ReadingLists = new List<ReadingList>()
        {
            readingList
        };

        await _readingListService.AddChaptersToReadingList(1, new List<int>() {1, 2}, readingList);


        _unitOfWork.UserRepository.Update(user);
        await _unitOfWork.CommitAsync();

        await _readingListService.CalculateReadingListAgeRating(readingList);
        Assert.Equal(AgeRating.Unknown, readingList.AgeRating);
    }

    [Fact]
    public async Task CalculateAgeRating_ShouldUpdateToMax()
    {
        await ResetDb();
        _context.AppUser.Add(new AppUser()
        {
            UserName = "majora2007",
            ReadingLists = new List<ReadingList>(),
            Libraries = new List<Library>()
            {
                new Library()
                {
                    Name = "Test LIb",
                    Type = LibraryType.Book,
                    Series = new List<Series>()
                    {
                        new Series()
                        {
                            Name = "Test",
                            Metadata = DbFactory.SeriesMetadata(new List<CollectionTag>()),
                            Volumes = new List<Volume>()
                            {
                                new Volume()
                                {
                                    Name = "0",
                                    Chapters = new List<Chapter>()
                                    {
                                        new Chapter()
                                        {
                                            Number = "1",
                                        },
                                        new Chapter()
                                        {
                                            Number = "2",
                                        }
                                    }
                                }
                            }
                        }
                    }
                },
            }
        });

        await _context.SaveChangesAsync();

        var user = await _unitOfWork.UserRepository.GetUserByUsernameAsync("majora2007", AppUserIncludes.ReadingLists);
        var readingList = new ReadingList();
        user.ReadingLists = new List<ReadingList>()
        {
            readingList
        };

        await _readingListService.AddChaptersToReadingList(1, new List<int>() {1, 2}, readingList);


        _unitOfWork.UserRepository.Update(user);
        await _unitOfWork.CommitAsync();

        await _readingListService.CalculateReadingListAgeRating(readingList);
        Assert.Equal(AgeRating.Unknown, readingList.AgeRating);
    }

    #endregion
}
