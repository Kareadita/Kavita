using System.Collections.Generic;
using System.IO;
using System.IO.Abstractions.TestingHelpers;
using System.Linq;
using System.Threading.Tasks;
using API.Data;
using API.Data.Repositories;
using API.DTOs.Reader;
using API.Entities;
using API.Entities.Enums;
using API.Helpers.Builders;
using API.Services;
using Microsoft.Extensions.Logging;
using NSubstitute;
using Xunit;
using Xunit.Abstractions;

namespace API.Tests.Services;

public class BookmarkServiceTests(ITestOutputHelper outputHelper): AbstractDbTest(outputHelper)
{
    private BookmarkService Create(IDirectoryService ds, IUnitOfWork unitOfWork)
    {
        return new BookmarkService(Substitute.For<ILogger<BookmarkService>>(), unitOfWork, ds,
Substitute.For<IMediaConversionService>());
    }

    #region BookmarkPage

    [Fact]
    public async Task BookmarkPage_ShouldCopyTheFileAndUpdateDB()
    {
        var (unitOfWork, context, _) = await CreateDatabase();

        var filesystem = CreateFileSystem();
        var file = $"{CacheDirectory}1/0001.jpg";
        filesystem.AddFile(file, new MockFileData("123"));

        var series = new SeriesBuilder("Test")
            .WithFormat(MangaFormat.Epub)
            .WithVolume(new VolumeBuilder(API.Services.Tasks.Scanner.Parser.Parser.LooseLeafVolume)
                .WithChapter(new ChapterBuilder("1")
                    .Build())
                .Build())
            .Build();
        series.Library = new LibraryBuilder("Test LIb").Build();
        context.Series.Add(series);


        context.AppUser.Add(new AppUser()
        {
            UserName = "Joe"
        });

        await context.SaveChangesAsync();


        var ds = new DirectoryService(Substitute.For<ILogger<DirectoryService>>(), filesystem);
        var bookmarkService = Create(ds, unitOfWork);
        var user = await unitOfWork.UserRepository.GetUserByIdAsync(1, AppUserIncludes.Bookmarks);

        var result = await bookmarkService.BookmarkPage(user, new BookmarkDto()
        {
            ChapterId = 1,
            Page = 1,
            SeriesId = 1,
            VolumeId = 1
        }, file);


        Assert.True(result);
        Assert.Single(ds.GetFiles(BookmarkDirectory, searchOption:SearchOption.AllDirectories));
        Assert.NotNull(await unitOfWork.UserRepository.GetBookmarkAsync(1));
    }

    [Fact]
    public async Task BookmarkPage_ShouldDeleteFileOnUnbookmark()
    {
        var (unitOfWork, context, _) = await CreateDatabase();

        var filesystem = CreateFileSystem();
        filesystem.AddFile($"{CacheDirectory}1/0001.jpg", new MockFileData("123"));
        filesystem.AddFile($"{BookmarkDirectory}1/1/0001.jpg", new MockFileData("123"));

        var series = new SeriesBuilder("Test")
            .WithFormat(MangaFormat.Epub)
            .WithVolume(new VolumeBuilder("1")
                .WithMinNumber(1)
                .WithChapter(new ChapterBuilder(API.Services.Tasks.Scanner.Parser.Parser.DefaultChapter)
                    .Build())
                .Build())
            .Build();
        series.Library = new LibraryBuilder("Test LIb").Build();

        context.Series.Add(series);


        context.AppUser.Add(new AppUser()
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

        await context.SaveChangesAsync();


        var ds = new DirectoryService(Substitute.For<ILogger<DirectoryService>>(), filesystem);
        var bookmarkService = Create(ds, unitOfWork);
        var user = await unitOfWork.UserRepository.GetUserByIdAsync(1, AppUserIncludes.Bookmarks);

        var result = await bookmarkService.RemoveBookmarkPage(user, new BookmarkDto()
        {
            ChapterId = 1,
            Page = 1,
            SeriesId = 1,
            VolumeId = 1
        });


        Assert.True(result);
        Assert.Empty(ds.GetFiles(BookmarkDirectory, searchOption:SearchOption.AllDirectories));
        Assert.Null(await unitOfWork.UserRepository.GetBookmarkAsync(1));
    }

    #endregion

    #region DeleteBookmarkFiles

    [Fact]
    public async Task DeleteBookmarkFiles_ShouldDeleteOnlyPassedFiles()
    {
        var (unitOfWork, context, _) = await CreateDatabase();

        var filesystem = CreateFileSystem();
        filesystem.AddFile($"{CacheDirectory}1/0001.jpg", new MockFileData("123"));
        filesystem.AddFile($"{BookmarkDirectory}1/1/1/0001.jpg", new MockFileData("123"));
        filesystem.AddFile($"{BookmarkDirectory}1/2/1/0002.jpg", new MockFileData("123"));
        filesystem.AddFile($"{BookmarkDirectory}1/2/1/0001.jpg", new MockFileData("123"));

        var series = new SeriesBuilder("Test")
            .WithFormat(MangaFormat.Epub)
            .WithVolume(new VolumeBuilder("1")
                .WithMinNumber(1)
                .WithChapter(new ChapterBuilder("1")
                    .Build())
                .Build())
            .Build();
        series.Library = new LibraryBuilder("Test LIb").Build();

        context.Series.Add(series);

        context.AppUser.Add(new AppUser()
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

        await context.SaveChangesAsync();


        var ds = new DirectoryService(Substitute.For<ILogger<DirectoryService>>(), filesystem);
        var bookmarkService = Create(ds, unitOfWork);

        await bookmarkService.DeleteBookmarkFiles([
            new AppUserBookmark
            {
            Page = 1,
            ChapterId = 1,
            FileName = $"1/1/1/0001.jpg",
            SeriesId = 1,
            VolumeId = 1
        }
        ]);


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

        var (unitOfWork, context, _) = await CreateDatabase();

        var series = new SeriesBuilder("Test")
            .WithFormat(MangaFormat.Epub)
            .WithVolume(new VolumeBuilder("1")
                .WithMinNumber(1)
                .WithChapter(new ChapterBuilder("1")
                    .Build())
                .Build())
            .Build();
        series.Library = new LibraryBuilder("Test LIb").Build();

        context.Series.Add(series);


        context.AppUser.Add(new AppUser()
        {
            UserName = "Joe"
        });

        await context.SaveChangesAsync();


        var ds = new DirectoryService(Substitute.For<ILogger<DirectoryService>>(), filesystem);
        var bookmarkService = Create(ds, unitOfWork);
        var user = await unitOfWork.UserRepository.GetUserByIdAsync(1, AppUserIncludes.Bookmarks);

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

        var (unitOfWork, context, _) = await CreateDatabase();

        var series = new SeriesBuilder("Test")
            .WithFormat(MangaFormat.Epub)
            .WithVolume(new VolumeBuilder("1")
                .WithMinNumber(1)
                .WithChapter(new ChapterBuilder("1")
                    .Build())
                .Build())
            .Build();
        series.Library = new LibraryBuilder("Test LIb").Build();

        context.Series.Add(series);

        context.AppUser.Add(new AppUser()
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

        await context.SaveChangesAsync();


        var ds = new DirectoryService(Substitute.For<ILogger<DirectoryService>>(), filesystem);

        var vol = await unitOfWork.VolumeRepository.GetVolumeAsync(1);
        vol.Chapters = new List<Chapter>();
        unitOfWork.VolumeRepository.Update(vol);
        await unitOfWork.CommitAsync();


        Assert.Single(ds.GetFiles(BookmarkDirectory, searchOption:SearchOption.AllDirectories));
        Assert.NotNull(await unitOfWork.UserRepository.GetBookmarkAsync(1));
    }


    [Fact]
    public async Task ShouldNotDeleteBookmark_OnVolumeDeletion()
    {
        var filesystem = CreateFileSystem();
        filesystem.AddFile($"{CacheDirectory}1/0001.jpg", new MockFileData("123"));
        filesystem.AddFile($"{BookmarkDirectory}1/1/0001.jpg", new MockFileData("123"));

        var (unitOfWork, context, _) = await CreateDatabase();
        var series = new SeriesBuilder("Test")
            .WithFormat(MangaFormat.Epub)
            .WithVolume(new VolumeBuilder("1")
                .WithMinNumber(1)
                .WithChapter(new ChapterBuilder("1")
                    .Build())
                .Build())
            .Build();
        series.Library = new LibraryBuilder("Test LIb").Build();

        context.Series.Add(series);


        context.AppUser.Add(new AppUser()
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

        await context.SaveChangesAsync();

        var user = await unitOfWork.UserRepository.GetUserByIdAsync(1, AppUserIncludes.Bookmarks);
        Assert.NotEmpty(user!.Bookmarks);

        series.Volumes = new List<Volume>();
        unitOfWork.SeriesRepository.Update(series);
        await unitOfWork.CommitAsync();


        var ds = new DirectoryService(Substitute.For<ILogger<DirectoryService>>(), filesystem);
        Assert.Single(ds.GetFiles(BookmarkDirectory, searchOption:SearchOption.AllDirectories));
        Assert.NotNull(await unitOfWork.UserRepository.GetBookmarkAsync(1));
    }

    #endregion
}
