﻿using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Abstractions.TestingHelpers;
using System.Linq;
using System.Threading.Tasks;
using API.Data;
using API.Data.Repositories;
using API.DTOs.Filtering;
using API.Entities;
using API.Entities.Enums;
using API.Entities.Metadata;
using API.Extensions;
using API.Helpers;
using API.Helpers.Builders;
using API.Services;
using API.Services.Tasks;
using API.SignalR;
using API.Tests.Helpers;
using Microsoft.Extensions.Logging;
using NSubstitute;
using Xunit;

namespace API.Tests.Services;

public class CleanupServiceTests : AbstractDbTest
{
    private readonly ILogger<CleanupService> _logger = Substitute.For<ILogger<CleanupService>>();
    private readonly IEventHub _messageHub = Substitute.For<IEventHub>();


    public CleanupServiceTests() : base()
    {
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
    }

    #region Setup


    protected override async Task ResetDb()
    {
        _context.Series.RemoveRange(_context.Series.ToList());
        _context.Users.RemoveRange(_context.Users.ToList());
        _context.AppUserBookmark.RemoveRange(_context.AppUserBookmark.ToList());

        await _context.SaveChangesAsync();
    }

    #endregion

    #region DeleteSeriesCoverImages

    [Fact]
    public async Task DeleteSeriesCoverImages_ShouldDeleteAll()
    {
        var filesystem = CreateFileSystem();
        filesystem.AddFile($"{CoverImageDirectory}{ImageService.GetSeriesFormat(1)}.jpg", new MockFileData(""));
        filesystem.AddFile($"{CoverImageDirectory}{ImageService.GetSeriesFormat(3)}.jpg", new MockFileData(""));
        filesystem.AddFile($"{CoverImageDirectory}{ImageService.GetSeriesFormat(1000)}.jpg", new MockFileData(""));

        // Delete all Series to reset state
        await ResetDb();

        var s = DbFactory.Series("Test 1");
        s.CoverImage = $"{ImageService.GetSeriesFormat(1)}.jpg";
        s.LibraryId = 1;
        _context.Series.Add(s);
        s = DbFactory.Series("Test 2");
        s.CoverImage = $"{ImageService.GetSeriesFormat(3)}.jpg";
        s.LibraryId = 1;
        _context.Series.Add(s);
        s = DbFactory.Series("Test 3");
        s.CoverImage = $"{ImageService.GetSeriesFormat(1000)}.jpg";
        s.LibraryId = 1;
        _context.Series.Add(s);

        var ds = new DirectoryService(Substitute.For<ILogger<DirectoryService>>(), filesystem);
        var cleanupService = new CleanupService(_logger, _unitOfWork, _messageHub,
            ds);

        await cleanupService.DeleteSeriesCoverImages();

        Assert.Empty(ds.GetFiles(CoverImageDirectory));
    }

    [Fact]
    public async Task DeleteSeriesCoverImages_ShouldNotDeleteLinkedFiles()
    {
        var filesystem = CreateFileSystem();
        filesystem.AddFile($"{CoverImageDirectory}{ImageService.GetSeriesFormat(1)}.jpg", new MockFileData(""));
        filesystem.AddFile($"{CoverImageDirectory}{ImageService.GetSeriesFormat(3)}.jpg", new MockFileData(""));
        filesystem.AddFile($"{CoverImageDirectory}{ImageService.GetSeriesFormat(1000)}.jpg", new MockFileData(""));

        // Delete all Series to reset state
        await ResetDb();

        // Add 2 series with cover images
        var s = DbFactory.Series("Test 1");
        s.CoverImage = $"{ImageService.GetSeriesFormat(1)}.jpg";
        s.LibraryId = 1;
        _context.Series.Add(s);
        s = DbFactory.Series("Test 2");
        s.CoverImage = $"{ImageService.GetSeriesFormat(3)}.jpg";
        s.LibraryId = 1;
        _context.Series.Add(s);


        await _context.SaveChangesAsync();
        var ds = new DirectoryService(Substitute.For<ILogger<DirectoryService>>(), filesystem);
        var cleanupService = new CleanupService(_logger, _unitOfWork, _messageHub,
            ds);

        await cleanupService.DeleteSeriesCoverImages();

        Assert.Equal(2, ds.GetFiles(CoverImageDirectory).Count());
    }
    #endregion

    #region DeleteChapterCoverImages
    [Fact]
    public async Task DeleteChapterCoverImages_ShouldNotDeleteLinkedFiles()
    {
        var filesystem = CreateFileSystem();
        filesystem.AddFile($"{CoverImageDirectory}v01_c01.jpg", new MockFileData(""));
        filesystem.AddFile($"{CoverImageDirectory}v01_c03.jpg", new MockFileData(""));
        filesystem.AddFile($"{CoverImageDirectory}v01_c1000.jpg", new MockFileData(""));

        // Delete all Series to reset state
        await ResetDb();

        // Add 2 series with cover images
        var s = DbFactory.Series("Test 1");
        var v = DbFactory.Volume("1");
        v.Chapters.Add(new Chapter()
        {
            Number = "0",
            Range = "0",
            CoverImage = "v01_c01.jpg"
        });
        v.CoverImage = "v01_c01.jpg";
        s.Volumes.Add(v);
        s.CoverImage = "series_01.jpg";
        s.LibraryId = 1;
        _context.Series.Add(s);

        s = DbFactory.Series("Test 2");
        v = DbFactory.Volume("1");
        v.Chapters.Add(new Chapter()
        {
            Number = "0",
            Range = "0",
            CoverImage = "v01_c03.jpg"
        });
        v.CoverImage = "v01_c03jpg";
        s.Volumes.Add(v);
        s.CoverImage = "series_03.jpg";
        s.LibraryId = 1;
        _context.Series.Add(s);


        await _context.SaveChangesAsync();
        var ds = new DirectoryService(Substitute.For<ILogger<DirectoryService>>(), filesystem);
        var cleanupService = new CleanupService(_logger, _unitOfWork, _messageHub,
            ds);

        await cleanupService.DeleteChapterCoverImages();

        Assert.Equal(2, ds.GetFiles(CoverImageDirectory).Count());
    }
    #endregion

    #region DeleteTagCoverImages

    [Fact]
    public async Task DeleteTagCoverImages_ShouldNotDeleteLinkedFiles()
    {
        var filesystem = CreateFileSystem();
        filesystem.AddFile($"{CoverImageDirectory}{ImageService.GetCollectionTagFormat(1)}.jpg", new MockFileData(""));
        filesystem.AddFile($"{CoverImageDirectory}{ImageService.GetCollectionTagFormat(2)}.jpg", new MockFileData(""));
        filesystem.AddFile($"{CoverImageDirectory}{ImageService.GetCollectionTagFormat(1000)}.jpg", new MockFileData(""));

        // Delete all Series to reset state
        await ResetDb();

        // Add 2 series with cover images
        var s = DbFactory.Series("Test 1");
        s.Metadata.CollectionTags = new List<CollectionTag>();
        s.Metadata.CollectionTags.Add(new CollectionTag()
        {
            Title = "Something",
            NormalizedTitle = "Something".ToNormalized(),
            CoverImage = $"{ImageService.GetCollectionTagFormat(1)}.jpg"
        });
        s.CoverImage = $"{ImageService.GetSeriesFormat(1)}.jpg";
        s.LibraryId = 1;
        _context.Series.Add(s);

        s = DbFactory.Series("Test 2");
        s.Metadata.CollectionTags = new List<CollectionTag>();
        s.Metadata.CollectionTags.Add(new CollectionTag()
        {
            Title = "Something 2",
            NormalizedTitle = "Something 2".ToNormalized(),
            CoverImage = $"{ImageService.GetCollectionTagFormat(2)}.jpg"
        });
        s.CoverImage = $"{ImageService.GetSeriesFormat(3)}.jpg";
        s.LibraryId = 1;
        _context.Series.Add(s);


        await _context.SaveChangesAsync();
        var ds = new DirectoryService(Substitute.For<ILogger<DirectoryService>>(), filesystem);
        var cleanupService = new CleanupService(_logger, _unitOfWork, _messageHub,
            ds);

        await cleanupService.DeleteTagCoverImages();

        Assert.Equal(2, ds.GetFiles(CoverImageDirectory).Count());
    }

    #endregion

    #region DeleteReadingListCoverImages
    [Fact]
    public async Task DeleteReadingListCoverImages_ShouldNotDeleteLinkedFiles()
    {
        var filesystem = CreateFileSystem();
        filesystem.AddFile($"{CoverImageDirectory}{ImageService.GetReadingListFormat(1)}.jpg", new MockFileData(""));
        filesystem.AddFile($"{CoverImageDirectory}{ImageService.GetReadingListFormat(2)}.jpg", new MockFileData(""));
        filesystem.AddFile($"{CoverImageDirectory}{ImageService.GetReadingListFormat(3)}.jpg", new MockFileData(""));

        // Delete all Series to reset state
        await ResetDb();

        _context.Users.Add(new AppUser()
        {
            UserName = "Joe",
            ReadingLists = new List<ReadingList>()
            {
                new ReadingList()
                {
                    Title = "Something",
                    NormalizedTitle = "Something".ToNormalized(),
                    CoverImage = $"{ImageService.GetReadingListFormat(1)}.jpg",
                    AgeRating = AgeRating.Unknown
                },
                new ReadingList()
                {
                    Title = "Something 2",
                    NormalizedTitle = "Something 2".ToNormalized(),
                    CoverImage = $"{ImageService.GetReadingListFormat(2)}.jpg",
                    AgeRating = AgeRating.Unknown
                }
            }
        });

        await _context.SaveChangesAsync();
        var ds = new DirectoryService(Substitute.For<ILogger<DirectoryService>>(), filesystem);
        var cleanupService = new CleanupService(_logger, _unitOfWork, _messageHub,
            ds);

        await cleanupService.DeleteReadingListCoverImages();

        Assert.Equal(2, ds.GetFiles(CoverImageDirectory).Count());
    }
    #endregion

    #region CleanupCacheDirectory

    [Fact]
    public void CleanupCacheDirectory_ClearAllFiles()
    {
        var filesystem = CreateFileSystem();
        filesystem.AddFile($"{CacheDirectory}01.jpg", new MockFileData(""));
        filesystem.AddFile($"{CacheDirectory}02.jpg", new MockFileData(""));

        var ds = new DirectoryService(Substitute.For<ILogger<DirectoryService>>(), filesystem);
        var cleanupService = new CleanupService(_logger, _unitOfWork, _messageHub,
            ds);
        cleanupService.CleanupCacheAndTempDirectories();
        Assert.Empty(ds.GetFiles(CacheDirectory, searchOption: SearchOption.AllDirectories));
    }

    [Fact]
    public void CleanupCacheDirectory_ClearAllFilesInSubDirectory()
    {
        var filesystem = CreateFileSystem();
        filesystem.AddFile($"{CacheDirectory}01.jpg", new MockFileData(""));
        filesystem.AddFile($"{CacheDirectory}subdir/02.jpg", new MockFileData(""));

        var ds = new DirectoryService(Substitute.For<ILogger<DirectoryService>>(), filesystem);
        var cleanupService = new CleanupService(_logger, _unitOfWork, _messageHub,
            ds);
        cleanupService.CleanupCacheAndTempDirectories();
        Assert.Empty(ds.GetFiles(CacheDirectory, searchOption: SearchOption.AllDirectories));
    }

    #endregion

    #region CleanupBackups

    [Fact]
    public async Task CleanupBackups_LeaveOneFile_SinceAllAreExpired()
    {
        var filesystem = CreateFileSystem();
        var filesystemFile = new MockFileData("")
        {
            CreationTime = DateTimeOffset.Now.Subtract(TimeSpan.FromDays(31))
        };
        filesystem.AddFile($"{BackupDirectory}kavita_backup_11_29_2021_12_00_13 AM.zip", filesystemFile);
        filesystem.AddFile($"{BackupDirectory}kavita_backup_12_3_2021_9_27_58 AM.zip", filesystemFile);
        filesystem.AddFile($"{BackupDirectory}randomfile.zip", filesystemFile);

        var ds = new DirectoryService(Substitute.For<ILogger<DirectoryService>>(), filesystem);
        var cleanupService = new CleanupService(_logger, _unitOfWork, _messageHub,
            ds);
        await cleanupService.CleanupBackups();
        Assert.Single(ds.GetFiles(BackupDirectory, searchOption: SearchOption.AllDirectories));
    }

    [Fact]
    public async Task CleanupBackups_LeaveLestExpired()
    {
        var filesystem = CreateFileSystem();
        var filesystemFile = new MockFileData("")
        {
            CreationTime = DateTimeOffset.Now.Subtract(TimeSpan.FromDays(31))
        };
        filesystem.AddFile($"{BackupDirectory}kavita_backup_11_29_2021_12_00_13 AM.zip", filesystemFile);
        filesystem.AddFile($"{BackupDirectory}kavita_backup_12_3_2021_9_27_58 AM.zip", filesystemFile);
        filesystem.AddFile($"{BackupDirectory}randomfile.zip", new MockFileData("")
        {
            CreationTime = DateTimeOffset.Now.Subtract(TimeSpan.FromDays(14))
        });

        var ds = new DirectoryService(Substitute.For<ILogger<DirectoryService>>(), filesystem);
        var cleanupService = new CleanupService(_logger, _unitOfWork, _messageHub,
            ds);
        await cleanupService.CleanupBackups();
        Assert.True(filesystem.File.Exists($"{BackupDirectory}randomfile.zip"));
    }

    #endregion

    #region CleanupLogs

    [Fact]
    public async Task CleanupLogs_LeaveOneFile_SinceAllAreExpired()
    {
        var filesystem = CreateFileSystem();
        foreach (var i in Enumerable.Range(1, 10))
        {
            var day = API.Services.Tasks.Scanner.Parser.Parser.PadZeros($"{i}");
            filesystem.AddFile($"{LogDirectory}kavita202009{day}.log", new MockFileData("")
            {
                CreationTime = DateTimeOffset.Now.Subtract(TimeSpan.FromDays(31))
            });
        }

        var ds = new DirectoryService(Substitute.For<ILogger<DirectoryService>>(), filesystem);
        var cleanupService = new CleanupService(_logger, _unitOfWork, _messageHub,
            ds);
        await cleanupService.CleanupLogs();
        Assert.Single(ds.GetFiles(LogDirectory, searchOption: SearchOption.AllDirectories));
    }

    [Fact]
    public async Task CleanupLogs_LeaveLestExpired()
    {
        var filesystem = CreateFileSystem();
        foreach (var i in Enumerable.Range(1, 9))
        {
            var day = API.Services.Tasks.Scanner.Parser.Parser.PadZeros($"{i}");
            filesystem.AddFile($"{LogDirectory}kavita202009{day}.log", new MockFileData("")
            {
                CreationTime = DateTimeOffset.Now.Subtract(TimeSpan.FromDays(31 - i))
            });
        }
        filesystem.AddFile($"{LogDirectory}kavita20200910.log", new MockFileData("")
        {
            CreationTime = DateTimeOffset.Now.Subtract(TimeSpan.FromDays(31 - 10))
        });
        filesystem.AddFile($"{LogDirectory}kavita20200911.log", new MockFileData("")
        {
            CreationTime = DateTimeOffset.Now.Subtract(TimeSpan.FromDays(31 - 11))
        });


        var ds = new DirectoryService(Substitute.For<ILogger<DirectoryService>>(), filesystem);
        var cleanupService = new CleanupService(_logger, _unitOfWork, _messageHub,
            ds);
        await cleanupService.CleanupLogs();
        Assert.True(filesystem.File.Exists($"{LogDirectory}kavita20200911.log"));
    }

    #endregion

    #region CleanupDbEntries

    [Fact]
    public async Task CleanupDbEntries_CleanupAbandonedChapters()
    {
        var c = new ChapterBuilder("0")
            .WithPages(1)
            .Build();
        var series = new SeriesBuilder("Test")
            .WithFormat(MangaFormat.Epub)
            .WithMetadata(new SeriesMetadata())
            .WithVolume(new VolumeBuilder("0")
                .WithNumber(1)
                .WithChapter(c)
                .Build())
            .Build();
        series.Library = new Library()
        {
            Name = "Test LIb",
            Type = LibraryType.Manga,
        };

        _context.Series.Add(series);


        _context.AppUser.Add(new AppUser()
        {
            UserName = "majora2007"
        });

        await _context.SaveChangesAsync();

        var readerService = new ReaderService(_unitOfWork, Substitute.For<ILogger<ReaderService>>(), Substitute.For<IEventHub>());

        var user = await _unitOfWork.UserRepository.GetUserByUsernameAsync("majora2007", AppUserIncludes.Progress);
        await readerService.MarkChaptersUntilAsRead(user, 1, 5);
        await _context.SaveChangesAsync();

        // Validate correct chapters have read status
        Assert.Equal(1, (await _unitOfWork.AppUserProgressRepository.GetUserProgressAsync(1, 1)).PagesRead);

        var cleanupService = new CleanupService(Substitute.For<ILogger<CleanupService>>(), _unitOfWork,
            Substitute.For<IEventHub>(),
            new DirectoryService(Substitute.For<ILogger<DirectoryService>>(), new MockFileSystem()));

        // Delete the Chapter
        _context.Chapter.Remove(c);
        await _unitOfWork.CommitAsync();
        Assert.Empty(await _unitOfWork.AppUserProgressRepository.GetUserProgressForSeriesAsync(1, 1));

        // NOTE: This may not be needed, the underlying DB structure seems fixed as of v0.7
        await cleanupService.CleanupDbEntries();

        Assert.Empty(await _unitOfWork.AppUserProgressRepository.GetUserProgressForSeriesAsync(1, 1));
    }

    [Fact]
    public async Task CleanupDbEntries_RemoveTagsWithoutSeries()
    {
        var c = new CollectionTag()
        {
            Title = "Test Tag",
            NormalizedTitle = "Test Tag".ToNormalized(),
        };
        var s = new SeriesBuilder("Test")
            .WithFormat(MangaFormat.Epub)
            .WithMetadata(new SeriesMetadataBuilder().WithCollectionTag(c).Build())
            .Build();
        s.Library = new Library()
        {
            Name = "Test LIb",
            Type = LibraryType.Manga,
        };

        _context.Series.Add(s);

        _context.AppUser.Add(new AppUser()
        {
            UserName = "majora2007"
        });

        await _context.SaveChangesAsync();

        var cleanupService = new CleanupService(Substitute.For<ILogger<CleanupService>>(), _unitOfWork,
            Substitute.For<IEventHub>(),
            new DirectoryService(Substitute.For<ILogger<DirectoryService>>(), new MockFileSystem()));

        // Delete the Chapter
        _context.Series.Remove(s);
        await _unitOfWork.CommitAsync();

        await cleanupService.CleanupDbEntries();

        Assert.Empty(await _unitOfWork.CollectionTagRepository.GetAllTagsAsync());
    }

    #endregion

    #region CleanupWantToRead

    [Fact]
    public async Task CleanupWantToRead_ShouldRemoveFullyReadSeries()
    {
        await ResetDb();

        var s = new SeriesBuilder("Test CleanupWantToRead_ShouldRemoveFullyReadSeries")
            .WithMetadata(new SeriesMetadataBuilder().WithPublicationStatus(PublicationStatus.Completed).Build())
            .Build();

        s.Library = new Library()
        {
            Name = "Test LIb",
            Type = LibraryType.Manga,
        };
        _context.Series.Add(s);

        var user = new AppUser()
        {
            UserName = "CleanupWantToRead_ShouldRemoveFullyReadSeries",
            WantToRead = new List<Series>()
            {
                s
            }
        };
        _context.AppUser.Add(user);

        await _unitOfWork.CommitAsync();

        var readerService = new ReaderService(_unitOfWork, Substitute.For<ILogger<ReaderService>>(),
            Substitute.For<IEventHub>());

        await readerService.MarkSeriesAsRead(user, s.Id);
        await _unitOfWork.CommitAsync();

        var cleanupService = new CleanupService(Substitute.For<ILogger<CleanupService>>(), _unitOfWork,
            Substitute.For<IEventHub>(),
            new DirectoryService(Substitute.For<ILogger<DirectoryService>>(), new MockFileSystem()));


        await cleanupService.CleanupWantToRead();

        var wantToRead =
            await _unitOfWork.SeriesRepository.GetWantToReadForUserAsync(user.Id, new UserParams(), new FilterDto());

        Assert.Equal(0, wantToRead.TotalCount);
    }
    #endregion

    // #region CleanupBookmarks
    //
    // [Fact]
    // public async Task CleanupBookmarks_LeaveAllFiles()
    // {
    //     var filesystem = CreateFileSystem();
    //     filesystem.AddFile($"{BookmarkDirectory}1/1/1/0001.jpg", new MockFileData(""));
    //     filesystem.AddFile($"{BookmarkDirectory}1/1/1/0002.jpg", new MockFileData(""));
    //
    //     // Delete all Series to reset state
    //     await ResetDb();
    //
    //     _context.Series.Add(new Series()
    //     {
    //         Name = "Test",
    //         Library = new Library() {
    //             Name = "Test LIb",
    //             Type = LibraryType.Manga,
    //         },
    //         Volumes = new List<Volume>()
    //         {
    //             new Volume()
    //             {
    //                 Chapters = new List<Chapter>()
    //                 {
    //                     new Chapter()
    //                     {
    //
    //                     }
    //                 }
    //             }
    //         }
    //     });
    //
    //     await _context.SaveChangesAsync();
    //
    //     _context.AppUser.Add(new AppUser()
    //     {
    //         Bookmarks = new List<AppUserBookmark>()
    //         {
    //             new AppUserBookmark()
    //             {
    //                 AppUserId = 1,
    //                 ChapterId = 1,
    //                 Page = 1,
    //                 FileName = "1/1/1/0001.jpg",
    //                 SeriesId = 1,
    //                 VolumeId = 1
    //             },
    //             new AppUserBookmark()
    //             {
    //                 AppUserId = 1,
    //                 ChapterId = 1,
    //                 Page = 2,
    //                 FileName = "1/1/1/0002.jpg",
    //                 SeriesId = 1,
    //                 VolumeId = 1
    //             }
    //         }
    //     });
    //
    //     await _context.SaveChangesAsync();
    //
    //
    //     var ds = new DirectoryService(Substitute.For<ILogger<DirectoryService>>(), filesystem);
    //     var cleanupService = new CleanupService(_logger, _unitOfWork, _messageHub,
    //         ds);
    //
    //     await cleanupService.CleanupBookmarks();
    //
    //     Assert.Equal(2, ds.GetFiles(BookmarkDirectory, searchOption:SearchOption.AllDirectories).Count());
    //
    // }
    //
    // [Fact]
    // public async Task CleanupBookmarks_LeavesOneFiles()
    // {
    //     var filesystem = CreateFileSystem();
    //     filesystem.AddFile($"{BookmarkDirectory}1/1/1/0001.jpg", new MockFileData(""));
    //     filesystem.AddFile($"{BookmarkDirectory}1/1/2/0002.jpg", new MockFileData(""));
    //
    //     // Delete all Series to reset state
    //     await ResetDb();
    //
    //     _context.Series.Add(new Series()
    //     {
    //         Name = "Test",
    //         Library = new Library() {
    //             Name = "Test LIb",
    //             Type = LibraryType.Manga,
    //         },
    //         Volumes = new List<Volume>()
    //         {
    //             new Volume()
    //             {
    //                 Chapters = new List<Chapter>()
    //                 {
    //                     new Chapter()
    //                     {
    //
    //                     }
    //                 }
    //             }
    //         }
    //     });
    //
    //     await _context.SaveChangesAsync();
    //
    //     _context.AppUser.Add(new AppUser()
    //     {
    //         Bookmarks = new List<AppUserBookmark>()
    //         {
    //             new AppUserBookmark()
    //             {
    //                 AppUserId = 1,
    //                 ChapterId = 1,
    //                 Page = 1,
    //                 FileName = "1/1/1/0001.jpg",
    //                 SeriesId = 1,
    //                 VolumeId = 1
    //             }
    //         }
    //     });
    //
    //     await _context.SaveChangesAsync();
    //
    //
    //     var ds = new DirectoryService(Substitute.For<ILogger<DirectoryService>>(), filesystem);
    //     var cleanupService = new CleanupService(_logger, _unitOfWork, _messageHub,
    //         ds);
    //
    //     await cleanupService.CleanupBookmarks();
    //
    //     Assert.Equal(1, ds.GetFiles(BookmarkDirectory, searchOption:SearchOption.AllDirectories).Count());
    //     Assert.Equal(1, ds.FileSystem.Directory.GetDirectories($"{BookmarkDirectory}1/1/").Length);
    // }
    //
    // #endregion
}
