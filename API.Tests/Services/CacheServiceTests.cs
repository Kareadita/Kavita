using System.Collections.Generic;
using System.Data.Common;
using System.IO;
using System.IO.Abstractions.TestingHelpers;
using System.Linq;
using System.Threading.Tasks;
using API.Data;
using API.Data.Metadata;
using API.Entities;
using API.Entities.Enums;
using API.Helpers.Builders;
using API.Services;
using API.Services.Tasks.Scanner.Parser;
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

internal class MockReadingItemServiceForCacheService : IReadingItemService
{
    private readonly DirectoryService _directoryService;

    public MockReadingItemServiceForCacheService(DirectoryService directoryService)
    {
        _directoryService = directoryService;
    }

    public ComicInfo GetComicInfo(string filePath)
    {
        return null;
    }

    public int GetNumberOfPages(string filePath, MangaFormat format)
    {
        return 1;
    }

    public string GetCoverImage(string fileFilePath, string fileName, MangaFormat format, EncodeFormat encodeFormat, CoverImageSize size = CoverImageSize.Default)
    {
        return string.Empty;
    }

    public void Extract(string fileFilePath, string targetDirectory, MangaFormat format, int imageCount = 1)
    {
        throw new System.NotImplementedException();
    }

    public ParserInfo Parse(string path, string rootPath, string libraryRoot, LibraryType type)
    {
        throw new System.NotImplementedException();
    }

    public ParserInfo ParseFile(string path, string rootPath, string libraryRoot, LibraryType type)
    {
        throw new System.NotImplementedException();
    }
}
public class CacheServiceTests
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

    public CacheServiceTests()
    {
        var contextOptions = new DbContextOptionsBuilder()
            .UseSqlite(CreateInMemoryDatabase())
            .Options;
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

        await Seed.SeedSettings(_context, new DirectoryService(Substitute.For<ILogger<DirectoryService>>(), filesystem));

        var setting = await _context.ServerSetting.Where(s => s.Key == ServerSettingKey.CacheDirectory).SingleAsync();
        setting.Value = CacheDirectory;

        setting = await _context.ServerSetting.Where(s => s.Key == ServerSettingKey.BackupDirectory).SingleAsync();
        setting.Value = BackupDirectory;

        _context.ServerSetting.Update(setting);

        _context.Library.Add(new LibraryBuilder("Manga")
            .WithFolderPath(new FolderPathBuilder("C:/data/").Build())
            .Build());
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

    #region Ensure

    [Fact]
    public async Task Ensure_DirectoryAlreadyExists_DontExtractAnything()
    {
        var filesystem = CreateFileSystem();
        filesystem.AddFile($"{DataDirectory}Test v1.zip", new MockFileData(""));
        filesystem.AddDirectory($"{CacheDirectory}1/");
        var ds = new DirectoryService(Substitute.For<ILogger<DirectoryService>>(), filesystem);
        var cleanupService = new CacheService(_logger, _unitOfWork, ds,
            new ReadingItemService(Substitute.For<IArchiveService>(),
                Substitute.For<IBookService>(),
                Substitute.For<IImageService>(), ds, Substitute.For<ILogger<ReadingItemService>>()),
            Substitute.For<IBookmarkService>());

        await ResetDB();
        var s = new SeriesBuilder("Test").Build();
        var v = new VolumeBuilder("1").Build();
        var c = new ChapterBuilder("1")
                .WithFile(new MangaFileBuilder($"{DataDirectory}Test v1.zip", MangaFormat.Archive).Build())
                .Build();
        v.Chapters.Add(c);
        s.Volumes.Add(v);
        s.LibraryId = 1;
        _context.Series.Add(s);

        await _context.SaveChangesAsync();

        await cleanupService.Ensure(1);
        Assert.Empty(ds.GetFiles(filesystem.Path.Join(CacheDirectory, "1"), searchOption:SearchOption.AllDirectories));
    }

    // [Fact]
    // public async Task Ensure_DirectoryAlreadyExists_ExtractsImages()
    // {
    //     // TODO: Figure out a way to test this
    //     var filesystem = CreateFileSystem();
    //     filesystem.AddFile($"{DataDirectory}Test v1.zip", new MockFileData(""));
    //     filesystem.AddDirectory($"{CacheDirectory}1/");
    //     var ds = new DirectoryService(Substitute.For<ILogger<DirectoryService>>(), filesystem);
    //     var archiveService = Substitute.For<IArchiveService>();
    //     archiveService.ExtractArchive($"{DataDirectory}Test v1.zip",
    //         filesystem.Path.Join(CacheDirectory, "1"));
    //     var cleanupService = new CacheService(_logger, _unitOfWork, ds,
    //         new ReadingItemService(archiveService, Substitute.For<IBookService>(), Substitute.For<IImageService>(), ds));
    //
    //     await ResetDB();
    //     var s = new SeriesBuilder("Test").Build();
    //     var v = new VolumeBuilder("1").Build();
    //     var c = new Chapter()
    //     {
    //         Number = "1",
    //         Files = new List<MangaFile>()
    //         {
    //             new MangaFile()
    //             {
    //                 Format = MangaFormat.Archive,
    //                 FilePath = $"{DataDirectory}Test v1.zip",
    //             }
    //         }
    //     };
    //     v.Chapters.Add(c);
    //     s.Volumes.Add(v);
    //     s.LibraryId = 1;
    //     _context.Series.Add(s);
    //
    //     await _context.SaveChangesAsync();
    //
    //     await cleanupService.Ensure(1);
    //     Assert.Empty(ds.GetFiles(filesystem.Path.Join(CacheDirectory, "1"), searchOption:SearchOption.AllDirectories));
    // }


    #endregion

    #region CleanupChapters

    [Fact]
    public void CleanupChapters_AllFilesShouldBeDeleted()
    {
        var filesystem = CreateFileSystem();
        filesystem.AddDirectory($"{CacheDirectory}1/");
        filesystem.AddFile($"{CacheDirectory}1/001.jpg", new MockFileData(""));
        filesystem.AddFile($"{CacheDirectory}1/002.jpg", new MockFileData(""));
        filesystem.AddFile($"{CacheDirectory}3/003.jpg", new MockFileData(""));
        var ds = new DirectoryService(Substitute.For<ILogger<DirectoryService>>(), filesystem);
        var cleanupService = new CacheService(_logger, _unitOfWork, ds,
            new ReadingItemService(Substitute.For<IArchiveService>(),
                Substitute.For<IBookService>(), Substitute.For<IImageService>(), ds, Substitute.For<ILogger<ReadingItemService>>()),
            Substitute.For<IBookmarkService>());

        cleanupService.CleanupChapters(new []{1, 3});
        Assert.Empty(ds.GetFiles(CacheDirectory, searchOption:SearchOption.AllDirectories));
    }


    #endregion

    #region GetCachedEpubFile

    [Fact]
    public void GetCachedEpubFile_ShouldReturnFirstEpub()
    {
        var filesystem = CreateFileSystem();
        filesystem.AddDirectory($"{CacheDirectory}1/");
        filesystem.AddFile($"{DataDirectory}1.epub", new MockFileData(""));
        filesystem.AddFile($"{DataDirectory}2.epub", new MockFileData(""));
        var ds = new DirectoryService(Substitute.For<ILogger<DirectoryService>>(), filesystem);
        var cs = new CacheService(_logger, _unitOfWork, ds,
            new ReadingItemService(Substitute.For<IArchiveService>(),
                Substitute.For<IBookService>(), Substitute.For<IImageService>(), ds, Substitute.For<ILogger<ReadingItemService>>()),
            Substitute.For<IBookmarkService>());

        var c = new ChapterBuilder("1")
            .WithFile(new MangaFileBuilder($"{DataDirectory}1.epub", MangaFormat.Epub).Build())
            .WithFile(new MangaFileBuilder($"{DataDirectory}2.epub", MangaFormat.Epub).Build())
            .Build();
        cs.GetCachedFile(c);
        Assert.Same($"{DataDirectory}1.epub", cs.GetCachedFile(c));
    }

    #endregion

    #region GetCachedPagePath

    [Fact]
    public void GetCachedPagePath_ReturnNullIfNoFiles()
    {
        var filesystem = CreateFileSystem();
        filesystem.AddDirectory($"{CacheDirectory}1/");
        filesystem.AddFile($"{DataDirectory}1.zip", new MockFileData(""));
        filesystem.AddFile($"{DataDirectory}2.zip", new MockFileData(""));

        var c = new ChapterBuilder("1")
            .WithId(1)
            .Build();

        var fileIndex = 0;
        foreach (var file in c.Files)
        {
            for (var i = 0; i < file.Pages - 1; i++)
            {
                filesystem.AddFile($"{CacheDirectory}1/{fileIndex}/{i+1}.jpg", new MockFileData(""));
            }

            fileIndex++;
        }

        var ds = new DirectoryService(Substitute.For<ILogger<DirectoryService>>(), filesystem);
        var cs = new CacheService(_logger, _unitOfWork, ds,
            new ReadingItemService(Substitute.For<IArchiveService>(),
                Substitute.For<IBookService>(), Substitute.For<IImageService>(), ds, Substitute.For<ILogger<ReadingItemService>>()),
            Substitute.For<IBookmarkService>());

        // Flatten to prepare for how GetFullPath expects
        ds.Flatten($"{CacheDirectory}1/");

        var path = cs.GetCachedPagePath(c.Id, 11);
        Assert.Equal(string.Empty, path);
    }

    [Fact]
    public void GetCachedPagePath_GetFileFromFirstFile()
    {
        var filesystem = CreateFileSystem();
        filesystem.AddDirectory($"{CacheDirectory}1/");
        filesystem.AddFile($"{DataDirectory}1.zip", new MockFileData(""));
        filesystem.AddFile($"{DataDirectory}2.zip", new MockFileData(""));

        var c = new ChapterBuilder("1")
            .WithId(1)
            .WithFile(new MangaFileBuilder($"{DataDirectory}1.zip", MangaFormat.Archive)
                .WithPages(10)
                .WithId(1)
                .Build())
            .WithFile(new MangaFileBuilder($"{DataDirectory}2.zip", MangaFormat.Archive)
                .WithPages(5)
                .WithId(2)
                .Build())
            .Build();

        var fileIndex = 0;
        foreach (var file in c.Files)
        {
            for (var i = 0; i < file.Pages; i++)
            {
                filesystem.AddFile($"{CacheDirectory}1/00{fileIndex}_00{i+1}.jpg", new MockFileData(""));
            }

            fileIndex++;
        }

        var ds = new DirectoryService(Substitute.For<ILogger<DirectoryService>>(), filesystem);
        var cs = new CacheService(_logger, _unitOfWork, ds,
            new ReadingItemService(Substitute.For<IArchiveService>(),
                Substitute.For<IBookService>(), Substitute.For<IImageService>(), ds, Substitute.For<ILogger<ReadingItemService>>()),
            Substitute.For<IBookmarkService>());

        // Flatten to prepare for how GetFullPath expects
        ds.Flatten($"{CacheDirectory}1/");

        Assert.Equal(ds.FileSystem.Path.GetFullPath($"{CacheDirectory}/1/000_001.jpg"), ds.FileSystem.Path.GetFullPath(cs.GetCachedPagePath(c.Id, 0)));

    }


    [Fact]
    public void GetCachedPagePath_GetLastPageFromSingleFile()
    {
        var filesystem = CreateFileSystem();
        filesystem.AddDirectory($"{CacheDirectory}1/");
        filesystem.AddFile($"{DataDirectory}1.zip", new MockFileData(""));

        var c = new ChapterBuilder("1")
            .WithId(1)
            .WithFile(new MangaFileBuilder($"{DataDirectory}1.zip", MangaFormat.Archive)
                .WithPages(10)
                .WithId(1)
                .Build())
            .Build();
        c.Pages = c.Files.Sum(f => f.Pages);

        var fileIndex = 0;
        foreach (var file in c.Files)
        {
            for (var i = 0; i < file.Pages; i++)
            {
                filesystem.AddFile($"{CacheDirectory}1/{fileIndex}/{i+1}.jpg", new MockFileData(""));
            }

            fileIndex++;
        }

        var ds = new DirectoryService(Substitute.For<ILogger<DirectoryService>>(), filesystem);
        var cs = new CacheService(_logger, _unitOfWork, ds,
            new ReadingItemService(Substitute.For<IArchiveService>(),
                Substitute.For<IBookService>(), Substitute.For<IImageService>(), ds, Substitute.For<ILogger<ReadingItemService>>()),
            Substitute.For<IBookmarkService>());

        // Flatten to prepare for how GetFullPath expects
        ds.Flatten($"{CacheDirectory}1/");

        // Remember that we start at 0, so this is the 10th file
        var path = cs.GetCachedPagePath(c.Id, c.Pages);
        Assert.Equal(ds.FileSystem.Path.GetFullPath($"{CacheDirectory}/1/000_0{c.Pages}.jpg"), ds.FileSystem.Path.GetFullPath(path));
    }

    [Fact]
    public void GetCachedPagePath_GetFileFromSecondFile()
    {
        var filesystem = CreateFileSystem();
        filesystem.AddDirectory($"{CacheDirectory}1/");
        filesystem.AddFile($"{DataDirectory}1.zip", new MockFileData(""));
        filesystem.AddFile($"{DataDirectory}2.zip", new MockFileData(""));

        var c = new ChapterBuilder("1")
            .WithId(1)
            .WithFile(new MangaFileBuilder($"{DataDirectory}1.zip", MangaFormat.Archive)
                .WithPages(10)
                .WithId(1)
                .Build())
            .WithFile(new MangaFileBuilder($"{DataDirectory}2.zip", MangaFormat.Archive)
                .WithPages(5)
                .WithId(2)
                .Build())
            .Build();

        var fileIndex = 0;
        foreach (var file in c.Files)
        {
            for (var i = 0; i < file.Pages; i++)
            {
                filesystem.AddFile($"{CacheDirectory}1/{fileIndex}/{i+1}.jpg", new MockFileData(""));
            }

            fileIndex++;
        }

        var ds = new DirectoryService(Substitute.For<ILogger<DirectoryService>>(), filesystem);
        var cs = new CacheService(_logger, _unitOfWork, ds,
            new ReadingItemService(Substitute.For<IArchiveService>(),
                Substitute.For<IBookService>(), Substitute.For<IImageService>(), ds, Substitute.For<ILogger<ReadingItemService>>()),
            Substitute.For<IBookmarkService>());

        // Flatten to prepare for how GetFullPath expects
        ds.Flatten($"{CacheDirectory}1/");

        // Remember that we start at 0, so this is the page + 1 file
        var path = cs.GetCachedPagePath(c.Id, 10);
        Assert.Equal(ds.FileSystem.Path.GetFullPath($"{CacheDirectory}/1/001_001.jpg"), ds.FileSystem.Path.GetFullPath(path));
    }

    #endregion

    #region ExtractChapterFiles

    // [Fact]
    // public void ExtractChapterFiles_ShouldExtractOnlyImages()
    // {
    // const string testDirectory = "/manga/";
    // var fileSystem = new MockFileSystem();
    // for (var i = 0; i < 10; i++)
    // {
    //     fileSystem.AddFile($"{testDirectory}file_{i}.zip", new MockFileData(""));
    // }
    //
    // fileSystem.AddDirectory(CacheDirectory);
    //
    // var ds = new DirectoryService(Substitute.For<ILogger<DirectoryService>>(), fileSystem);
    // var cs = new CacheService(_logger, _unitOfWork, ds,
    //     new MockReadingItemServiceForCacheService(ds));
    //
    //
    //     cs.ExtractChapterFiles(CacheDirectory, new List<MangaFile>()
    //     {
    //         new MangaFile()
    //         {
    //             ChapterId = 1,
    //             Format = MangaFormat.Archive,
    //             Pages = 2,
    //             FilePath =
    //         }
    //     })
    // }

    #endregion
}
