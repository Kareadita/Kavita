﻿using System;
using System.Collections.Generic;
using System.Data.Common;
using System.IO;
using System.IO.Abstractions.TestingHelpers;
using System.Linq;
using System.Threading.Tasks;
using API.Data;
using API.Data.Repositories;
using API.DTOs.ReadingLists;
using API.DTOs.ReadingLists.CBL;
using API.Entities;
using API.Entities.Enums;
using API.Extensions;
using API.Helpers;
using API.Helpers.Builders;
using API.Services;
using API.SignalR;
using API.Tests.Helpers;
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
        _unitOfWork = new UnitOfWork(_context, mapper, null!);

        _readingListService = new ReadingListService(_unitOfWork, Substitute.For<ILogger<ReadingListService>>(), Substitute.For<IEventHub>());
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
        _context.Library.RemoveRange(_context.Library);
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

    #region AddChaptersToReadingList
    [Fact]
    public async Task AddChaptersToReadingList_ShouldAddFirstItem_AsOrderZero()
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
                        new SeriesBuilder("Test")
                            .WithMetadata(DbFactory.SeriesMetadata(new List<CollectionTag>()))
                            .WithVolumes(new List<Volume>()
                            {
                                new VolumeBuilder("0")
                                    .WithChapter(new ChapterBuilder("1")
                                        .WithAgeRating(AgeRating.Everyone)
                                        .Build()
                                    )
                                    .WithChapter(new ChapterBuilder("2")
                                        .WithAgeRating(AgeRating.X18Plus)
                                        .Build()
                                    )
                                    .WithChapter(new ChapterBuilder("3")
                                        .WithAgeRating(AgeRating.X18Plus)
                                        .Build()
                                    )
                                    .Build()
                            })
                            .Build(),
                    }
                },
            }
        });

        await _context.SaveChangesAsync();

        var user = await _unitOfWork.UserRepository.GetUserByUsernameAsync("majora2007", AppUserIncludes.ReadingLists);
        var readingList = DbFactory.ReadingList("test");
        user!.ReadingLists = new List<ReadingList>()
        {
            readingList
        };

        await _readingListService.AddChaptersToReadingList(1, new List<int>() {1}, readingList);
        await _unitOfWork.CommitAsync();

        Assert.Equal(1, readingList.Items.Count);
        Assert.Equal(0, readingList.Items.First().Order);
    }

    [Fact]
    public async Task AddChaptersToReadingList_ShouldNewItems_AfterLastOrder()
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
                        new SeriesBuilder("Test")
                            .WithMetadata(DbFactory.SeriesMetadata(new List<CollectionTag>()))
                            .WithVolumes(new List<Volume>()
                            {
                                new VolumeBuilder("0")
                                    .WithChapter(new ChapterBuilder("1")
                                        .WithAgeRating(AgeRating.Everyone)
                                        .Build()
                                    )
                                    .WithChapter(new ChapterBuilder("2")
                                        .WithAgeRating(AgeRating.X18Plus)
                                        .Build()
                                    )
                                    .WithChapter(new ChapterBuilder("3")
                                        .WithAgeRating(AgeRating.X18Plus)
                                        .Build()
                                    )
                                    .Build()
                            })
                            .Build()
                    }
                },
            }
        });

        await _context.SaveChangesAsync();

        var user = await _unitOfWork.UserRepository.GetUserByUsernameAsync("majora2007", AppUserIncludes.ReadingLists);
        var readingList = DbFactory.ReadingList("test");
        user!.ReadingLists = new List<ReadingList>()
        {
            readingList
        };

        await _readingListService.AddChaptersToReadingList(1, new List<int>() {1}, readingList);
        await _unitOfWork.CommitAsync();
        await _readingListService.AddChaptersToReadingList(1, new List<int>() {2}, readingList);
        await _unitOfWork.CommitAsync();

        Assert.Equal(2, readingList.Items.Count);
        Assert.Equal(0, readingList.Items.First().Order);
        Assert.Equal(1, readingList.Items.ElementAt(1).Order);
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
                        new SeriesBuilder("Test")
                            .WithMetadata(DbFactory.SeriesMetadata(new List<CollectionTag>()))
                            .WithVolumes(new List<Volume>()
                            {
                                new VolumeBuilder("0")
                                    .WithChapter(new ChapterBuilder("1")
                                        .WithAgeRating(AgeRating.Everyone)
                                        .Build()
                                    )
                                    .WithChapter(new ChapterBuilder("2")
                                        .WithAgeRating(AgeRating.X18Plus)
                                        .Build()
                                    )
                                    .WithChapter(new ChapterBuilder("3")
                                        .WithAgeRating(AgeRating.X18Plus)
                                        .Build()
                                    )
                                    .Build()
                            })
                            .Build()
                    }
                },
            }
        });

        await _context.SaveChangesAsync();

        var user = await _unitOfWork.UserRepository.GetUserByUsernameAsync("majora2007", AppUserIncludes.ReadingLists);
        var readingList = DbFactory.ReadingList("Test");
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
                        new SeriesBuilder("Test")
                            .WithMetadata(DbFactory.SeriesMetadata(new List<CollectionTag>()))
                            .WithVolumes(new List<Volume>()
                            {
                                new VolumeBuilder("0")
                                    .WithChapter(new ChapterBuilder("1")
                                        .WithAgeRating(AgeRating.Everyone)
                                        .Build()
                                    )
                                    .WithChapter(new ChapterBuilder("2")
                                        .WithAgeRating(AgeRating.X18Plus)
                                        .Build()
                                    )
                                    .WithChapter(new ChapterBuilder("3")
                                        .WithAgeRating(AgeRating.X18Plus)
                                        .Build()
                                    )
                                    .Build()
                            })
                            .Build()
                    }
                },
            }
        });

        await _context.SaveChangesAsync();

        var user = await _unitOfWork.UserRepository.GetUserByUsernameAsync("majora2007", AppUserIncludes.ReadingLists);
        var readingList = DbFactory.ReadingList("test");
        user!.ReadingLists = new List<ReadingList>()
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
                        new SeriesBuilder("Test")
                            .WithMetadata(DbFactory.SeriesMetadata(new List<CollectionTag>()))
                            .WithVolumes(new List<Volume>()
                            {
                                new VolumeBuilder("0")
                                    .WithChapter(new ChapterBuilder("1")
                                        .WithAgeRating(AgeRating.Everyone)
                                        .Build()
                                    )
                                    .WithChapter(new ChapterBuilder("2")
                                        .WithAgeRating(AgeRating.X18Plus)
                                        .Build()
                                    )
                                    .Build()
                            })
                            .Build(),
                    }
                },
            }
        });

        await _context.SaveChangesAsync();

        var user = await _unitOfWork.UserRepository.GetUserByUsernameAsync("majora2007", AppUserIncludes.ReadingLists);
        var readingList = DbFactory.ReadingList("Test");
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
                        new SeriesBuilder("Test")
                            .WithMetadata(DbFactory.SeriesMetadata(new List<CollectionTag>()))
                            .WithVolumes(new List<Volume>()
                            {
                                new VolumeBuilder("0")
                                    .WithChapter(new ChapterBuilder("1")
                                        .WithAgeRating(AgeRating.Everyone)
                                        .Build()
                                    )
                                    .WithChapter(new ChapterBuilder("2")
                                        .WithAgeRating(AgeRating.X18Plus)
                                        .Build()
                                    )
                                    .WithChapter(new ChapterBuilder("3")
                                        .WithAgeRating(AgeRating.X18Plus)
                                        .Build()
                                    )
                                    .Build()
                            })
                            .Build()
                    }
                },
            }
        });

        await _context.SaveChangesAsync();

        var user = await _unitOfWork.UserRepository.GetUserByUsernameAsync("majora2007", AppUserIncludes.ReadingLists | AppUserIncludes.Progress);
        var readingList = DbFactory.ReadingList("Test");
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
                        new SeriesBuilder("Test")
                            .WithMetadata(DbFactory.SeriesMetadata(new List<CollectionTag>()))
                            .WithVolumes(new List<Volume>()
                            {
                                new VolumeBuilder("0")
                                    .WithChapter(new ChapterBuilder("1")
                                        .Build()
                                    )
                                    .WithChapter(new ChapterBuilder("2")
                                        .Build()
                                    )
                                    .Build()
                            })
                            .Build()
                    }
                },
            }
        });

        await _context.SaveChangesAsync();

        var user = await _unitOfWork.UserRepository.GetUserByUsernameAsync("majora2007", AppUserIncludes.ReadingLists);
        var readingList = DbFactory.ReadingList("Test");
        user!.ReadingLists = new List<ReadingList>()
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
        var s = new SeriesBuilder("Test")
            .WithMetadata(DbFactory.SeriesMetadata(new List<CollectionTag>()))
            .WithVolumes(new List<Volume>()
            {
                new VolumeBuilder("0")
                    .WithChapter(new ChapterBuilder("1")
                        .Build()
                    )
                    .WithChapter(new ChapterBuilder("2")
                        .Build()
                    )
                    .Build()
            })
            .Build();
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
                        s
                    }
                },
            }
        });

        s.Metadata.AgeRating = AgeRating.G;

        await _context.SaveChangesAsync();

        var user = await _unitOfWork.UserRepository.GetUserByUsernameAsync("majora2007", AppUserIncludes.ReadingLists);
        var readingList = DbFactory.ReadingList("Test");
        user.ReadingLists = new List<ReadingList>()
        {
            readingList
        };

        await _readingListService.AddChaptersToReadingList(1, new List<int>() {1, 2}, readingList);


        _unitOfWork.UserRepository.Update(user);
        await _unitOfWork.CommitAsync();

        await _readingListService.CalculateReadingListAgeRating(readingList);
        Assert.Equal(AgeRating.G, readingList.AgeRating);
    }

    #endregion

    #region CalculateStartAndEndDates

    [Fact]
    public async Task CalculateStartAndEndDates_ShouldBeNothing_IfNothing()
    {
        await ResetDb();
        var s = new SeriesBuilder("Test")
            .WithMetadata(DbFactory.SeriesMetadata(new List<CollectionTag>()))
            .WithVolumes(new List<Volume>()
            {
                new VolumeBuilder("0")
                    .WithChapter(new ChapterBuilder("1")
                        .Build()
                    )
                    .WithChapter(new ChapterBuilder("2")
                        .Build()
                    )
                    .Build()
            })
            .Build();
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
                        s
                    }
                },
            }
        });

        await _context.SaveChangesAsync();

        var user = await _unitOfWork.UserRepository.GetUserByUsernameAsync("majora2007", AppUserIncludes.ReadingLists);
        var readingList = DbFactory.ReadingList("Test");
        user.ReadingLists = new List<ReadingList>()
        {
            readingList
        };

        await _readingListService.AddChaptersToReadingList(1, new List<int>() {1, 2}, readingList);


        _unitOfWork.UserRepository.Update(user);
        await _unitOfWork.CommitAsync();

        await _readingListService.CalculateStartAndEndDates(readingList);
        Assert.Equal(0, readingList.StartingMonth);
        Assert.Equal(0, readingList.StartingYear);
        Assert.Equal(0, readingList.EndingMonth);
        Assert.Equal(0, readingList.EndingYear);
    }

    [Fact]
    public async Task CalculateStartAndEndDates_ShouldBeSomething_IfChapterHasSet()
    {
        await ResetDb();
        var s = new SeriesBuilder("Test")
            .WithMetadata(DbFactory.SeriesMetadata(new List<CollectionTag>()))
            .WithVolumes(new List<Volume>()
            {
                new VolumeBuilder("0")
                    .WithChapter(new ChapterBuilder("1")
                        .WithReleaseDate(new DateTime(2005, 03, 01))
                        .Build()
                    )
                    .WithChapter(new ChapterBuilder("2")
                        .WithReleaseDate(new DateTime(2002, 03, 01))
                        .Build()
                    )
                    .Build()
            })
            .Build();
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
                        s
                    }
                },
            }
        });

        await _context.SaveChangesAsync();

        var user = await _unitOfWork.UserRepository.GetUserByUsernameAsync("majora2007", AppUserIncludes.ReadingLists);
        var readingList = DbFactory.ReadingList("Test");
        user.ReadingLists = new List<ReadingList>()
        {
            readingList
        };

        await _readingListService.AddChaptersToReadingList(1, new List<int>() {1, 2}, readingList);


        _unitOfWork.UserRepository.Update(user);
        await _unitOfWork.CommitAsync();

        await _readingListService.CalculateStartAndEndDates(readingList);
        Assert.Equal(3, readingList.StartingMonth);
        Assert.Equal(2002, readingList.StartingYear);
        Assert.Equal(3, readingList.EndingMonth);
        Assert.Equal(2005, readingList.EndingYear);
    }

    #endregion

    #region FormatTitle

    [Fact]
    public void FormatTitle_ShouldFormatCorrectly()
    {
        // Manga Library & Archive
        Assert.Equal("Volume 1", ReadingListService.FormatTitle(CreateListItemDto(MangaFormat.Archive, LibraryType.Manga, "1")));
        Assert.Equal("Chapter 1", ReadingListService.FormatTitle(CreateListItemDto(MangaFormat.Archive, LibraryType.Manga, "1", "1")));
        Assert.Equal("Chapter 1", ReadingListService.FormatTitle(CreateListItemDto(MangaFormat.Archive, LibraryType.Manga, "1", "1", "The Title")));
        Assert.Equal("Volume 1", ReadingListService.FormatTitle(CreateListItemDto(MangaFormat.Archive, LibraryType.Manga, "1",  chapterTitleName: "The Title")));
        Assert.Equal("The Title", ReadingListService.FormatTitle(CreateListItemDto(MangaFormat.Archive, LibraryType.Manga, chapterTitleName: "The Title")));

        // Comic Library & Archive
        Assert.Equal("Volume 1", ReadingListService.FormatTitle(CreateListItemDto(MangaFormat.Archive, LibraryType.Comic, "1")));
        Assert.Equal("Issue #1", ReadingListService.FormatTitle(CreateListItemDto(MangaFormat.Archive, LibraryType.Comic, "1", "1")));
        Assert.Equal("Issue #1", ReadingListService.FormatTitle(CreateListItemDto(MangaFormat.Archive, LibraryType.Comic, "1", "1", "The Title")));
        Assert.Equal("Volume 1", ReadingListService.FormatTitle(CreateListItemDto(MangaFormat.Archive, LibraryType.Comic, "1",  chapterTitleName: "The Title")));
        Assert.Equal("The Title", ReadingListService.FormatTitle(CreateListItemDto(MangaFormat.Archive, LibraryType.Comic, chapterTitleName: "The Title")));

        // Book Library & Archive
        Assert.Equal("Volume 1", ReadingListService.FormatTitle(CreateListItemDto(MangaFormat.Archive, LibraryType.Book, "1")));
        Assert.Equal("Book 1", ReadingListService.FormatTitle(CreateListItemDto(MangaFormat.Archive, LibraryType.Book, "1", "1")));
        Assert.Equal("Book 1", ReadingListService.FormatTitle(CreateListItemDto(MangaFormat.Archive, LibraryType.Book, "1", "1", "The Title")));
        Assert.Equal("Volume 1", ReadingListService.FormatTitle(CreateListItemDto(MangaFormat.Archive, LibraryType.Book, "1",  chapterTitleName: "The Title")));
        Assert.Equal("The Title", ReadingListService.FormatTitle(CreateListItemDto(MangaFormat.Archive, LibraryType.Book, chapterTitleName: "The Title")));

        // Manga Library & EPUB
        Assert.Equal("Volume 1", ReadingListService.FormatTitle(CreateListItemDto(MangaFormat.Epub, LibraryType.Manga, "1")));
        Assert.Equal("Volume 1", ReadingListService.FormatTitle(CreateListItemDto(MangaFormat.Epub, LibraryType.Manga, "1", "1")));
        Assert.Equal("Volume 1", ReadingListService.FormatTitle(CreateListItemDto(MangaFormat.Epub, LibraryType.Manga, "1", "1", "The Title")));
        Assert.Equal("The Title", ReadingListService.FormatTitle(CreateListItemDto(MangaFormat.Epub, LibraryType.Manga, "1",  chapterTitleName: "The Title")));
        Assert.Equal("The Title", ReadingListService.FormatTitle(CreateListItemDto(MangaFormat.Epub, LibraryType.Manga, chapterTitleName: "The Title")));

        // Book Library & EPUB
        Assert.Equal("Volume 1", ReadingListService.FormatTitle(CreateListItemDto(MangaFormat.Epub, LibraryType.Book, "1")));
        Assert.Equal("Volume 1", ReadingListService.FormatTitle(CreateListItemDto(MangaFormat.Epub, LibraryType.Book, "1", "1")));
        Assert.Equal("Volume 1", ReadingListService.FormatTitle(CreateListItemDto(MangaFormat.Epub, LibraryType.Book, "1", "1", "The Title")));
        Assert.Equal("The Title", ReadingListService.FormatTitle(CreateListItemDto(MangaFormat.Epub, LibraryType.Book, "1",  chapterTitleName: "The Title")));
        Assert.Equal("The Title", ReadingListService.FormatTitle(CreateListItemDto(MangaFormat.Epub, LibraryType.Book, chapterTitleName: "The Title")));

    }

    private static ReadingListItemDto CreateListItemDto(MangaFormat seriesFormat, LibraryType libraryType,
        string volumeNumber = API.Services.Tasks.Scanner.Parser.Parser.DefaultVolume,
        string chapterNumber = API.Services.Tasks.Scanner.Parser.Parser.DefaultChapter,
        string chapterTitleName = "")
    {
        return new ReadingListItemDto()
        {
            SeriesFormat = seriesFormat,
            LibraryType = libraryType,
            VolumeNumber = volumeNumber,
            ChapterNumber = chapterNumber,
            ChapterTitleName = chapterTitleName
        };
    }

    #endregion

    #region CreateReadingList

    private async Task CreateReadingList_SetupBaseData()
    {
        var fablesSeries = DbFactory.Series("Fables");
        fablesSeries.Volumes.Add(new Volume()
        {
            Number = 1,
            Name = "2002",
            Chapters = new List<Chapter>()
            {
                EntityFactory.CreateChapter("1", false),
            }
        });

        _context.AppUser.Add(new AppUser()
        {
            UserName = "majora2007",
            ReadingLists = new List<ReadingList>(),
            Libraries = new List<Library>()
            {
                new Library()
                {
                    Name = "Test Lib",
                    Type = LibraryType.Book,
                    Series = new List<Series>()
                    {
                        fablesSeries,
                    },
                },
            },
        });
        _context.AppUser.Add(new AppUser()
        {
            UserName = "admin",
            ReadingLists = new List<ReadingList>(),
            Libraries = new List<Library>()
            {
                new Library()
                {
                    Name = "Test Lib 2",
                    Type = LibraryType.Book,
                    Series = new List<Series>()
                    {
                        fablesSeries,
                    },
                },
            }
        });
        await _unitOfWork.CommitAsync();
    }

    [Fact]
    public async Task CreateReadingList_ShouldCreate_WhenNoOtherListsOnUser()
    {
        await ResetDb();
        await CreateReadingList_SetupBaseData();

        var user = await _unitOfWork.UserRepository.GetUserByIdAsync(1, AppUserIncludes.ReadingLists);
        await _readingListService.CreateReadingListForUser(user, "Test List");
        Assert.NotEmpty((await _unitOfWork.UserRepository.GetUserByIdAsync(1, AppUserIncludes.ReadingLists))
            .ReadingLists);
    }

    [Fact]
    public async Task CreateReadingList_ShouldNotCreate_WhenExistingList()
    {
        await ResetDb();
        await CreateReadingList_SetupBaseData();

        var user = await _unitOfWork.UserRepository.GetUserByIdAsync(1, AppUserIncludes.ReadingLists);
        await _readingListService.CreateReadingListForUser(user, "Test List");
        Assert.NotEmpty((await _unitOfWork.UserRepository.GetUserByIdAsync(1, AppUserIncludes.ReadingLists))
            .ReadingLists);
        try
        {
            await _readingListService.CreateReadingListForUser(user, "Test List");
        }
        catch (Exception ex)
        {
            Assert.Equal("A list of this name already exists", ex.Message);
        }
        Assert.Single((await _unitOfWork.UserRepository.GetUserByIdAsync(1, AppUserIncludes.ReadingLists))
            .ReadingLists);
    }

    [Fact]
    public async Task CreateReadingList_ShouldNotCreate_WhenPromotedListExists()
    {
        await ResetDb();
        await CreateReadingList_SetupBaseData();


        var user = await _unitOfWork.UserRepository.GetUserByUsernameAsync("admin", AppUserIncludes.ReadingLists);
        var list = await _readingListService.CreateReadingListForUser(user, "Test List");
        await _readingListService.UpdateReadingList(list,
            new UpdateReadingListDto()
            {
                ReadingListId = list.Id, Promoted = true, Title = list.Title, Summary = list.Summary,
                CoverImageLocked = false
            });

        try
        {
            await _readingListService.CreateReadingListForUser(user, "Test List");
        }
        catch (Exception ex)
        {
            Assert.Equal("A list of this name already exists", ex.Message);
        }
    }

    #endregion

    #region UpdateReadingList
    #endregion

    #region DeleteReadingList
    [Fact]
    public async Task DeleteReadingList_ShouldDelete()
    {
        await ResetDb();
        await CreateReadingList_SetupBaseData();

        var user = await _unitOfWork.UserRepository.GetUserByIdAsync(1, AppUserIncludes.ReadingLists);
        await _readingListService.CreateReadingListForUser(user, "Test List");
        Assert.NotEmpty((await _unitOfWork.UserRepository.GetUserByIdAsync(1, AppUserIncludes.ReadingLists))
            .ReadingLists);
        try
        {
            await _readingListService.CreateReadingListForUser(user, "Test List");
        }
        catch (Exception ex)
        {
            Assert.Equal("A list of this name already exists", ex.Message);
        }
        Assert.Single((await _unitOfWork.UserRepository.GetUserByIdAsync(1, AppUserIncludes.ReadingLists))
            .ReadingLists);

        await _readingListService.DeleteReadingList(1, user);
        Assert.Empty((await _unitOfWork.UserRepository.GetUserByIdAsync(1, AppUserIncludes.ReadingLists))
            .ReadingLists);
    }
    #endregion

    #region UserHasReadingListAccess
    // TODO: UserHasReadingListAccess tests are unavailable because I can't mock UserManager<AppUser>
    public async Task UserHasReadingListAccess_ShouldWorkIfTheirList()
    {
        await ResetDb();
        await CreateReadingList_SetupBaseData();

        var user = await _unitOfWork.UserRepository.GetUserByIdAsync(1, AppUserIncludes.ReadingLists);
        await _readingListService.CreateReadingListForUser(user, "Test List");

        var userWithList = await _readingListService.UserHasReadingListAccess(1, "majora2007");
        Assert.NotNull(userWithList);
        Assert.Single(userWithList.ReadingLists);
    }


    public async Task UserHasReadingListAccess_ShouldNotWork_IfNotTheirList()
    {
        await ResetDb();
        await CreateReadingList_SetupBaseData();

        var user = await _unitOfWork.UserRepository.GetUserByIdAsync(2, AppUserIncludes.ReadingLists);
        await _readingListService.CreateReadingListForUser(user, "Test List");

        var userWithList = await _readingListService.UserHasReadingListAccess(1, "majora2007");
        Assert.Null(userWithList);
    }


    public async Task UserHasReadingListAccess_ShouldWork_IfNotTheirList_ButUserIsAdmin()
    {
        await ResetDb();
        await CreateReadingList_SetupBaseData();


        var user = await _unitOfWork.UserRepository.GetUserByIdAsync(1, AppUserIncludes.ReadingLists);
        await _readingListService.CreateReadingListForUser(user, "Test List");

        //var admin = await _unitOfWork.UserRepository.GetUserByIdAsync(2, AppUserIncludes.ReadingLists);
        //_userManager.When(x => x.IsInRoleAsync(user, PolicyConstants.AdminRole)).Returns((info => true), null);

        //_userManager.IsInRoleAsync(admin, PolicyConstants.AdminRole).ReturnsForAnyArgs(true);

        var userWithList = await _readingListService.UserHasReadingListAccess(1, "majora2007");
        Assert.NotNull(userWithList);
        Assert.Single(userWithList.ReadingLists);
    }
    #endregion

    #region ValidateCBL

    [Fact]
    public async Task ValidateCblFile_ShouldFail_UserHasAccessToNoSeries()
    {
        await ResetDb();
        var cblReadingList = LoadCblFromPath("Fables.cbl");

        // Mock up our series
        var fablesSeries = DbFactory.Series("Fables");
        var fables2Series = DbFactory.Series("Fables: The Last Castle");

        fablesSeries.Volumes.Add(new Volume()
        {
            Number = 1,
            Name = "2002",
            Chapters = new List<Chapter>()
            {
                EntityFactory.CreateChapter("1", false),
                EntityFactory.CreateChapter("2", false),
                EntityFactory.CreateChapter("3", false),

            }
        });
        fables2Series.Volumes.Add(new Volume()
        {
            Number = 1,
            Name = "2003",
            Chapters = new List<Chapter>()
            {
                EntityFactory.CreateChapter("1", false),
                EntityFactory.CreateChapter("2", false),
                EntityFactory.CreateChapter("3", false),

            }
        });

        _context.AppUser.Add(new AppUser()
        {
            UserName = "majora2007",
            ReadingLists = new List<ReadingList>(),
            Libraries = new List<Library>(),
        });

        _context.Library.Add(new Library()
        {
            Name = "Test Lib 2",
            Type = LibraryType.Book,
            Series = new List<Series>()
            {
                fablesSeries,
                fables2Series,
            },
        });

        await _unitOfWork.CommitAsync();

        var importSummary = await _readingListService.ValidateCblFile(1, cblReadingList);

        Assert.Equal(CblImportResult.Fail, importSummary.Success);
        Assert.NotEmpty(importSummary.Results);
    }

    [Fact]
    public async Task ValidateCblFile_ShouldFail_ServerHasNoSeries()
    {
        await ResetDb();
        var cblReadingList = LoadCblFromPath("Fables.cbl");

        // Mock up our series
        var fablesSeries = DbFactory.Series("Fablesa");
        var fables2Series = DbFactory.Series("Fablesa: The Last Castle");

        fablesSeries.Volumes.Add(new Volume()
        {
            Number = 1,
            Name = "2002",
            Chapters = new List<Chapter>()
            {
                EntityFactory.CreateChapter("1", false),
                EntityFactory.CreateChapter("2", false),
                EntityFactory.CreateChapter("3", false),

            }
        });
        fables2Series.Volumes.Add(new Volume()
        {
            Number = 1,
            Name = "2003",
            Chapters = new List<Chapter>()
            {
                EntityFactory.CreateChapter("1", false),
                EntityFactory.CreateChapter("2", false),
                EntityFactory.CreateChapter("3", false),

            }
        });

        _context.AppUser.Add(new AppUser()
        {
            UserName = "majora2007",
            ReadingLists = new List<ReadingList>(),
            Libraries = new List<Library>(),
        });

        _context.Library.Add(new Library()
        {
            Name = "Test Lib 2",
            Type = LibraryType.Book,
            Series = new List<Series>()
            {
                fablesSeries,
                fables2Series,
            },
        });

        await _unitOfWork.CommitAsync();

        var importSummary = await _readingListService.ValidateCblFile(1, cblReadingList);

        Assert.Equal(CblImportResult.Fail, importSummary.Success);
        Assert.NotEmpty(importSummary.Results);
    }

    #endregion

    #region CreateReadingListFromCBL

    private static CblReadingList LoadCblFromPath(string path)
    {
        var testDirectory = Path.Join(Directory.GetCurrentDirectory(), "../../../Services/Test Data/ReadingListService/");

        var reader = new System.Xml.Serialization.XmlSerializer(typeof(CblReadingList));
        using var file = new StreamReader(Path.Join(testDirectory, path));
        var cblReadingList = (CblReadingList) reader.Deserialize(file);
        file.Close();
        return cblReadingList;
    }

    [Fact]
    public async Task CreateReadingListFromCBL_ShouldCreateList()
    {
        await ResetDb();
        var cblReadingList = LoadCblFromPath("Fables.cbl");

        // Mock up our series
        // var fablesSeries = DbFactory.Series("Fables");
        // var fables2Series = DbFactory.Series("Fables: The Last Castle");

        var fablesSeries = new SeriesBuilder("Fables")
            .WithVolume(new VolumeBuilder("2002")
                .WithNumber(1)
                .WithChapter(new ChapterBuilder("1").Build())
                .WithChapter(new ChapterBuilder("2").Build())
                .WithChapter(new ChapterBuilder("3").Build())
                .Build())
            .Build();

        var fables2Series = new SeriesBuilder("Fables: The Last Castle")
            .WithVolume(new VolumeBuilder("2003")
                .WithNumber(1)
                .WithChapter(new ChapterBuilder("1").Build())
                .WithChapter(new ChapterBuilder("2").Build())
                .WithChapter(new ChapterBuilder("3").Build())
                .Build())
            .Build();



        // fablesSeries.Volumes.Add(new Volume()
        // {
        //     Number = 1,
        //     Name = "2002",
        //     Chapters = new List<Chapter>()
        //     {
        //         EntityFactory.CreateChapter("1", false),
        //         EntityFactory.CreateChapter("2", false),
        //         EntityFactory.CreateChapter("3", false),
        //
        //     }
        // });
        // fables2Series.Volumes.Add(new Volume()
        // {
        //     Number = 1,
        //     Name = "2003",
        //     Chapters = new List<Chapter>()
        //     {
        //         EntityFactory.CreateChapter("1", false),
        //         EntityFactory.CreateChapter("2", false),
        //         EntityFactory.CreateChapter("3", false),
        //
        //     }
        // });

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
                        fablesSeries,
                        fables2Series
                    },
                },
            },
        });
        await _unitOfWork.CommitAsync();

        var importSummary = await _readingListService.CreateReadingListFromCbl(1, cblReadingList);

        Assert.Equal(CblImportResult.Partial, importSummary.Success);
        Assert.NotEmpty(importSummary.Results);

        var createdList = await _unitOfWork.ReadingListRepository.GetReadingListByIdAsync(1);

        Assert.NotNull(createdList);
        Assert.Equal("Fables", createdList.Title);

        Assert.Equal(4, createdList.Items.Count);
        Assert.Equal(1, createdList.Items.First(item => item.Order == 0).ChapterId);
        Assert.Equal(2, createdList.Items.First(item => item.Order == 1).ChapterId);
        Assert.Equal(3, createdList.Items.First(item => item.Order == 2).ChapterId);
        Assert.Equal(4, createdList.Items.First(item => item.Order == 3).ChapterId);
    }

    [Fact]
    public async Task CreateReadingListFromCBL_ShouldCreateList_ButOnlyIncludeSeriesThatUserHasAccessTo()
    {
        await ResetDb();
        var cblReadingList = LoadCblFromPath("Fables.cbl");

        // Mock up our series
        var fablesSeries = DbFactory.Series("Fables");
        var fables2Series = DbFactory.Series("Fables: The Last Castle");

        fablesSeries.Volumes.Add(new Volume()
        {
            Number = 1,
            Name = "2002",
            Chapters = new List<Chapter>()
            {
                EntityFactory.CreateChapter("1", false),
                EntityFactory.CreateChapter("2", false),
                EntityFactory.CreateChapter("3", false),

            }
        });
        fables2Series.Volumes.Add(new Volume()
        {
            Number = 1,
            Name = "2003",
            Chapters = new List<Chapter>()
            {
                EntityFactory.CreateChapter("1", false),
                EntityFactory.CreateChapter("2", false),
                EntityFactory.CreateChapter("3", false),

            }
        });

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
                        fablesSeries,
                    },
                },
            },
        });

        _context.Library.Add(new Library()
        {
            Name = "Test Lib 2",
            Type = LibraryType.Book,
            Series = new List<Series>()
            {
                fables2Series,
            },
        });

        await _unitOfWork.CommitAsync();

        var importSummary = await _readingListService.CreateReadingListFromCbl(1, cblReadingList);

        Assert.Equal(CblImportResult.Partial, importSummary.Success);
        Assert.NotEmpty(importSummary.Results);

        var createdList = await _unitOfWork.ReadingListRepository.GetReadingListByIdAsync(1);

        Assert.NotNull(createdList);
        Assert.Equal("Fables", createdList.Title);

        Assert.Equal(3, createdList.Items.Count);
        Assert.Equal(1, createdList.Items.First(item => item.Order == 0).ChapterId);
        Assert.Equal(2, createdList.Items.First(item => item.Order == 1).ChapterId);
        Assert.Equal(3, createdList.Items.First(item => item.Order == 2).ChapterId);
        Assert.NotNull(importSummary.Results.SingleOrDefault(r => r.Series == "Fables: The Last Castle"
                                                                  && r.Reason == CblImportReason.SeriesMissing));
    }

    [Fact]
    public async Task CreateReadingListFromCBL_ShouldUpdateAnExistingList()
    {
        await ResetDb();
        var cblReadingList = LoadCblFromPath("Fables.cbl");

        // Mock up our series
        var fablesSeries = DbFactory.Series("Fables");
        var fables2Series = DbFactory.Series("Fables: The Last Castle");

        fablesSeries.Volumes.Add(new Volume()
        {
            Number = 1,
            Name = "2002",
            Chapters = new List<Chapter>()
            {
                EntityFactory.CreateChapter("1", false),
                EntityFactory.CreateChapter("2", false),
                EntityFactory.CreateChapter("3", false),

            }
        });
        fables2Series.Volumes.Add(new Volume()
        {
            Number = 1,
            Name = "2003",
            Chapters = new List<Chapter>()
            {
                EntityFactory.CreateChapter("1", false),
                EntityFactory.CreateChapter("2", false),
                EntityFactory.CreateChapter("3", false),

            }
        });

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
                        fablesSeries,
                        fables2Series
                    },
                },
            },
        });

        await _unitOfWork.CommitAsync();

        // Create a reading list named Fables and add 2 chapters to it
        var user = await _unitOfWork.UserRepository.GetUserByIdAsync(1, AppUserIncludes.ReadingLists);
        var readingList = await _readingListService.CreateReadingListForUser(user, "Fables");
        Assert.True(await _readingListService.AddChaptersToReadingList(1, new List<int>() {1, 3}, readingList));
        Assert.Equal(2, readingList.Items.Count);

        // Attempt to import a Cbl with same reading list name
        var importSummary = await _readingListService.CreateReadingListFromCbl(1, cblReadingList);

        Assert.Equal(CblImportResult.Partial, importSummary.Success);
        Assert.NotEmpty(importSummary.Results);

        var createdList = await _unitOfWork.ReadingListRepository.GetReadingListByIdAsync(1);

        Assert.NotNull(createdList);
        Assert.Equal("Fables", createdList.Title);

        Assert.Equal(4, createdList.Items.Count);
        Assert.Equal(4, importSummary.SuccessfulInserts.Count);

        Assert.Equal(1, createdList.Items.First(item => item.Order == 0).ChapterId);
        Assert.Equal(3, createdList.Items.First(item => item.Order == 1).ChapterId); // we inserted 3 first
        Assert.Equal(2, createdList.Items.First(item => item.Order == 2).ChapterId);
        Assert.Equal(4, createdList.Items.First(item => item.Order == 3).ChapterId);
    }
    #endregion

}
