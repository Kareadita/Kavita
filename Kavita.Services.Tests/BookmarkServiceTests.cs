using System.IO.Abstractions.TestingHelpers;
using Kavita.API.Database;
using Kavita.API.Repositories;
using Kavita.API.Services;
using Kavita.API.Services.SignalR;
using Kavita.Database;
using Kavita.Database.Tests;
using Kavita.Models.Builders;
using Kavita.Models.DTOs.Reader;
using Kavita.Models.DTOs.SignalR;
using Kavita.Models.DTOs.SignalR.Bodies;
using Kavita.Models.Entities;
using Kavita.Models.Entities.Enums;
using Kavita.Models.Entities.User;
using Kavita.Models.Images;
using Kavita.Services.Builders;
using Kavita.Services.Images;
using Kavita.Services.Scanner;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using NSubstitute;
using Xunit.Abstractions;

namespace Kavita.Services.Tests;

public class BookmarkServiceTests(ITestOutputHelper outputHelper): AbstractDbTest(outputHelper)
{
    private readonly MockFileSystem _fileSystem = CreateFileSystem();
    private readonly IImagePipeline _imagePipeline = Substitute.For<IImagePipeline>();

    #region BookmarkPage

    [Fact]
    public async Task BookmarkPage_WritesPageInTargetFormat()
    {
        var (unitOfWork, context, _) = await CreateDatabase();
        await AddSeriesAndUser(context);
        var page = AddCachedPage("0001.jpg");
        var bookmarkService = Create(unitOfWork);

        var result = await bookmarkService.BookmarkPage(await GetUser(unitOfWork), NewDto(page: 1), page);

        Assert.True(result);
        _imagePipeline.DidNotReceiveWithAnyArgs().Copy(default!, default, default!, default!);
        var bookmark = await context.AppUserBookmark.Include(b => b.ImageFile).SingleAsync();
        Assert.NotNull(bookmark.ImageFile);
        Assert.Equal(ImageKind.Bookmark, bookmark.ImageFile.Kind);
        Assert.Equal(EncodeFormat.PNG, bookmark.ImageFile.Format);
        Assert.Equal("1/1/1/0001.png", Parser.NormalizePath(bookmark.ImageFile.FileName));
        Assert.True(_fileSystem.File.Exists(BookmarkDirectory + "1/1/1/0001.png"));
    }

    [Fact]
    public async Task BookmarkPage_PageAlreadyInTargetFormat_Copied()
    {
        var (unitOfWork, context, _) = await CreateDatabase();
        await AddSeriesAndUser(context);
        var page = AddCachedPage("0001.png");
        var bookmarkService = Create(unitOfWork);

        Assert.True(await bookmarkService.BookmarkPage(await GetUser(unitOfWork), NewDto(page: 1), page));

        _imagePipeline.DidNotReceiveWithAnyArgs().Write(default!, default, default!, default!, default!);
        Assert.True(_fileSystem.File.Exists(BookmarkDirectory + "1/1/1/0001.png"));
    }

    [Fact]
    public async Task BookmarkPage_CommitFails_WrittenFileDeleted()
    {
        var (unitOfWork, context, _) = await CreateDatabase();
        await AddSeriesAndUser(context);
        var page = AddCachedPage("0001.jpg");
        var bookmarkService = Create(unitOfWork);

        // No chapter 999, so the foreign key fails the commit after the file is written
        var dto = NewDto(page: 1);
        dto.ChapterId = 999;
        var result = await bookmarkService.BookmarkPage(await GetUser(unitOfWork), dto, page);

        Assert.False(result);
        Assert.True(_fileSystem.File.Exists(page));
        Assert.Empty(_fileSystem.Directory.GetFiles(BookmarkDirectory, "*", SearchOption.AllDirectories));
        context.ChangeTracker.Clear();
        Assert.Empty(await context.ImageFile.ToListAsync());
    }

    #endregion

    #region RemoveBookmarks

    [Fact]
    public async Task RemoveBookmarkPage_DeletesRowImageRowAndFile()
    {
        var (unitOfWork, context, _) = await CreateDatabase();
        await AddSeriesAndUser(context);
        await AddBookmark(context, 1, "1/1/1/0001.png");
        await AddBookmark(context, 2, "1/1/1/0002.png");
        var bookmarkService = Create(unitOfWork);

        var result = await bookmarkService.RemoveBookmarkPage(await GetUser(unitOfWork), NewDto(page: 1));

        Assert.True(result);
        context.ChangeTracker.Clear();
        var left = await context.AppUserBookmark.Include(b => b.ImageFile).SingleAsync();
        Assert.Equal(2, left.Page);
        Assert.Equal("1/1/1/0002.png", Parser.NormalizePath(Assert.Single(await context.ImageFile.ToListAsync()).FileName));
        Assert.False(_fileSystem.File.Exists(BookmarkDirectory + "1/1/1/0001.png"));
        Assert.True(_fileSystem.File.Exists(BookmarkDirectory + "1/1/1/0002.png"));
    }

    [Fact]
    public async Task RemoveBookmarks_DeletesOnlyFoldersLeftEmpty()
    {
        var (unitOfWork, context, _) = await CreateDatabase();
        await AddSeriesAndUser(context);
        await AddBookmark(context, 1, "1/1/1/0001.png");
        await AddBookmark(context, 2, "1/1/1/0002.png");
        await AddBookmark(context, 3, "1/2/5/0003.png");
        var bookmarkService = Create(unitOfWork);

        var user = await GetUser(unitOfWork);
        var result = await bookmarkService.RemoveBookmarksAsync(user.Bookmarks.Where(b => b.Page != 2).ToList());

        Assert.True(result);
        Assert.True(_fileSystem.File.Exists(BookmarkDirectory + "1/1/1/0002.png"));
        Assert.False(_fileSystem.Directory.Exists(BookmarkDirectory + "1/2"));
        Assert.True(_fileSystem.Directory.Exists(BookmarkDirectory));
        context.ChangeTracker.Clear();
        Assert.Single(await context.AppUserBookmark.ToListAsync());
        Assert.Single(await context.ImageFile.ToListAsync());
    }

    [Fact]
    public async Task RemoveBookmarks_LastBookmark_KeepsBookmarkFolder()
    {
        var (unitOfWork, context, _) = await CreateDatabase();
        await AddSeriesAndUser(context);
        await AddBookmark(context, 1, "1/1/1/0001.png");
        var bookmarkService = Create(unitOfWork);

        var user = await GetUser(unitOfWork);
        Assert.True(await bookmarkService.RemoveBookmarksAsync(user.Bookmarks.ToList()));

        Assert.True(_fileSystem.Directory.Exists(BookmarkDirectory));
        Assert.Empty(_fileSystem.Directory.GetFileSystemEntries(BookmarkDirectory));
    }

    #endregion

    #region GetBookmarkFilesById

    [Fact]
    public async Task GetBookmarkFilesById_ReturnsImageFilePaths()
    {
        var (unitOfWork, context, _) = await CreateDatabase();
        await AddSeriesAndUser(context);
        var first = await AddBookmark(context, 1, "1/1/1/0001.png");
        await AddBookmark(context, 2, "1/1/1/0002.png");
        var bookmarkService = Create(unitOfWork);

        var files = await bookmarkService.GetBookmarkFilesById(1, 1, [first.Id]);

        Assert.Equal([Parser.NormalizePath(BookmarkDirectory + "1/1/1/0001.png")], files.ToList());
    }

    #endregion

    #region MoveBookmarkDirectory

    private static readonly string NewBookmarkDirectory = Root + "new/bookmarks/";

    [Fact]
    public async Task MoveBookmarkDirectory_MovesFilesThenSavesSetting()
    {
        var (unitOfWork, _, _) = await CreateDatabase();
        _fileSystem.AddFile(BookmarkDirectory + "1/1/1/0001.png", new MockFileData("one"));
        _fileSystem.AddFile(BookmarkDirectory + "4/2/7/0003.png", new MockFileData("three"));
        var bookmarkService = Create(unitOfWork);

        await bookmarkService.MoveBookmarkDirectory(NewBookmarkDirectory);

        Assert.Equal("one", _fileSystem.File.ReadAllText(NewBookmarkDirectory + "1/1/1/0001.png"));
        Assert.Equal("three", _fileSystem.File.ReadAllText(NewBookmarkDirectory + "4/2/7/0003.png"));
        Assert.False(_fileSystem.Directory.Exists(BookmarkDirectory));
        await AssertBookmarkSetting(unitOfWork, NewBookmarkDirectory);
    }

    [Fact]
    public async Task MoveBookmarkDirectory_NameTaken_LeavesFileInOldFolder()
    {
        var (unitOfWork, _, _) = await CreateDatabase();
        _fileSystem.AddFile(BookmarkDirectory + "1/1/1/0001.png", new MockFileData("old"));
        _fileSystem.AddFile(BookmarkDirectory + "1/1/1/0002.png", new MockFileData("two"));
        _fileSystem.AddFile(NewBookmarkDirectory + "1/1/1/0001.png", new MockFileData("already there"));
        var eventHub = Substitute.For<IEventHub>();
        var bookmarkService = Create(unitOfWork, eventHub);

        await bookmarkService.MoveBookmarkDirectory(NewBookmarkDirectory);

        Assert.Equal("already there", _fileSystem.File.ReadAllText(NewBookmarkDirectory + "1/1/1/0001.png"));
        Assert.Equal("old", _fileSystem.File.ReadAllText(BookmarkDirectory + "1/1/1/0001.png"));
        Assert.Equal("two", _fileSystem.File.ReadAllText(NewBookmarkDirectory + "1/1/1/0002.png"));
        Assert.False(_fileSystem.File.Exists(BookmarkDirectory + "1/1/1/0002.png"));

        var summary = MoveSummaryOf(eventHub);
        Assert.NotNull(summary);
        Assert.Equal(1, summary.Moved);
        Assert.Equal(1, summary.Skipped);
    }

    [Fact]
    public async Task MoveBookmarkDirectory_SourceMissing_SavesSetting()
    {
        var (unitOfWork, _, _) = await CreateDatabase();
        _fileSystem.Directory.Delete(BookmarkDirectory, true);
        var bookmarkService = Create(unitOfWork);

        await bookmarkService.MoveBookmarkDirectory(NewBookmarkDirectory);

        Assert.True(_fileSystem.Directory.Exists(NewBookmarkDirectory));
        await AssertBookmarkSetting(unitOfWork, NewBookmarkDirectory);
    }

    private static async Task AssertBookmarkSetting(IUnitOfWork unitOfWork, string expected)
    {
        var value = (await unitOfWork.SettingsRepository.GetSettingAsync(ServerSettingKey.BookmarkDirectory)).Value;
        Assert.Equal(Parser.NormalizePath(expected).TrimEnd('/'), Parser.NormalizePath(value).TrimEnd('/'));
    }

    private static BookmarkDirectoryMovedEventBodyDto? MoveSummaryOf(IEventHub eventHub) =>
        eventHub.ReceivedCalls()
            .SelectMany(c => c.GetArguments())
            .OfType<SignalRMessageDto>()
            .Where(m => m.Code == MessageEventCode.BookmarkDirectoryMoved)
            .Select(m => (BookmarkDirectoryMovedEventBodyDto) m.Body!)
            .SingleOrDefault();

    #endregion

    private BookmarkService Create(IUnitOfWork unitOfWork, IEventHub? eventHub = null)
    {
        _imagePipeline.Write(Arg.Any<ImageSource>(), Arg.Any<ImageKind>(), Arg.Any<ImagePolicy>(), Arg.Any<string>(), Arg.Any<string>())
            .Returns(ci => AddWrittenFile(ci.ArgAt<string>(3), ci.ArgAt<string>(4)));
        _imagePipeline.Copy(Arg.Any<string>(), Arg.Any<ImageKind>(), Arg.Any<string>(), Arg.Any<string>())
            .Returns(ci => AddWrittenFile(ci.ArgAt<string>(2), ci.ArgAt<string>(3)));

        var ds = new DirectoryService(Substitute.For<ILogger<DirectoryService>>(), _fileSystem);
        var imageFileService = new ImageFileService(unitOfWork, _imagePipeline, ds,
            Substitute.For<IEventHub>(), Substitute.For<ILogger<ImageFileService>>());
        return new BookmarkService(Substitute.For<ILogger<BookmarkService>>(), unitOfWork, ds, imageFileService,
            eventHub ?? Substitute.For<IEventHub>());
    }

    private ImageWriteResult AddWrittenFile(string directory, string fileName)
    {
        _fileSystem.AddFile(Path.Join(directory, fileName), new MockFileData("page"));
        return new ImageWriteResult(fileName, 800, 1200, 800, 1200);
    }

    private string AddCachedPage(string fileName)
    {
        var path = CacheDirectory + "1/" + fileName;
        _fileSystem.AddFile(path, new MockFileData("page"));
        return path;
    }

    private async Task<AppUserBookmark> AddBookmark(DataContext context, int page, string fileName)
    {
        _fileSystem.AddFile(BookmarkDirectory + fileName, new MockFileData("page"));

        var bookmark = new AppUserBookmark
        {
            Page = page,
            ChapterId = 1,
            SeriesId = 1,
            VolumeId = 1,
            AppUserId = 1,
            ImageFile = new ImageFile { Kind = ImageKind.Bookmark, FileName = fileName, Format = EncodeFormat.PNG },
        };
        context.AppUserBookmark.Add(bookmark);
        await context.SaveChangesAsync();
        context.ChangeTracker.Clear();

        return bookmark;
    }

    private static async Task AddSeriesAndUser(DataContext context)
    {
        var series = new SeriesBuilder("Test")
            .WithVolume(new VolumeBuilder("1")
                .WithChapter(new ChapterBuilder("1").Build())
                .Build())
            .Build();
        series.Library = new LibraryBuilder("Test Lib").Build();
        context.Series.Add(series);
        context.AppUser.Add(new AppUser { UserName = "Joe" });

        await context.SaveChangesAsync();
        context.ChangeTracker.Clear();
    }

    private static async Task<AppUser> GetUser(IUnitOfWork unitOfWork)
    {
        return (await unitOfWork.UserRepository.GetUserByIdAsync(1, AppUserIncludes.Bookmarks))!;
    }

    private static BookmarkDto NewDto(int page)
    {
        return new BookmarkDto { ChapterId = 1, Page = page, SeriesId = 1, VolumeId = 1 };
    }
}
