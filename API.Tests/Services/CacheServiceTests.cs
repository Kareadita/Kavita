using System.IO;
using System.IO.Abstractions.TestingHelpers;
using System.Linq;
using System.Threading.Tasks;
using API.Data.Metadata;
using API.Entities.Enums;
using API.Helpers.Builders;
using API.Services;
using API.Services.Tasks.Scanner.Parser;
using Microsoft.Extensions.Logging;
using NSubstitute;
using Xunit;
using Xunit.Abstractions;

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

    public ParserInfo Parse(string path, string rootPath, string libraryRoot, LibraryType type, bool enableMetadata = true)
    {
        throw new System.NotImplementedException();
    }

    public ParserInfo ParseFile(string path, string rootPath, string libraryRoot, LibraryType type, bool enableMetadata = true)
    {
        throw new System.NotImplementedException();
    }
}
public class CacheServiceTests(ITestOutputHelper outputHelper): AbstractDbTest(outputHelper)
{
    private readonly ILogger<CacheService> _logger = Substitute.For<ILogger<CacheService>>();

    #region Ensure

    [Fact]
    public async Task Ensure_DirectoryAlreadyExists_DontExtractAnything()
    {
        var (unitOfWork, context, _) = await CreateDatabase();

        var filesystem = CreateFileSystem();
        filesystem.AddFile($"{DataDirectory}Test v1.zip", new MockFileData(""));
        filesystem.AddDirectory($"{CacheDirectory}1/");
        var ds = new DirectoryService(Substitute.For<ILogger<DirectoryService>>(), filesystem);
        var cleanupService = new CacheService(_logger, unitOfWork, ds,
            new ReadingItemService(Substitute.For<IArchiveService>(),
                Substitute.For<IBookService>(),
                Substitute.For<IImageService>(), ds, Substitute.For<ILogger<ReadingItemService>>()),
            Substitute.For<IBookmarkService>());

        var s = new SeriesBuilder("Test").Build();
        var v = new VolumeBuilder("1").Build();
        var c = new ChapterBuilder("1")
                .WithFile(new MangaFileBuilder($"{DataDirectory}Test v1.zip", MangaFormat.Archive).Build())
                .Build();
        v.Chapters.Add(c);
        s.Volumes.Add(v);
        s.LibraryId = 1;
        context.Series.Add(s);

        await context.SaveChangesAsync();

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
    public async Task CleanupChapters_AllFilesShouldBeDeleted()
    {
        var (unitOfWork, context, _) = await CreateDatabase();

        var filesystem = CreateFileSystem();
        filesystem.AddDirectory($"{CacheDirectory}1/");
        filesystem.AddFile($"{CacheDirectory}1/001.jpg", new MockFileData(""));
        filesystem.AddFile($"{CacheDirectory}1/002.jpg", new MockFileData(""));
        filesystem.AddFile($"{CacheDirectory}3/003.jpg", new MockFileData(""));
        var ds = new DirectoryService(Substitute.For<ILogger<DirectoryService>>(), filesystem);
        var cleanupService = new CacheService(_logger, unitOfWork, ds,
            new ReadingItemService(Substitute.For<IArchiveService>(),
                Substitute.For<IBookService>(), Substitute.For<IImageService>(), ds, Substitute.For<ILogger<ReadingItemService>>()),
            Substitute.For<IBookmarkService>());

        cleanupService.CleanupChapters(new []{1, 3});
        Assert.Empty(ds.GetFiles(CacheDirectory, searchOption:SearchOption.AllDirectories));
    }


    #endregion

    #region GetCachedEpubFile

    [Fact]
    public async Task GetCachedEpubFile_ShouldReturnFirstEpub()
    {
        var (unitOfWork, context, _) = await CreateDatabase();

        var filesystem = CreateFileSystem();
        filesystem.AddDirectory($"{CacheDirectory}1/");
        filesystem.AddFile($"{DataDirectory}1.epub", new MockFileData(""));
        filesystem.AddFile($"{DataDirectory}2.epub", new MockFileData(""));
        var ds = new DirectoryService(Substitute.For<ILogger<DirectoryService>>(), filesystem);
        var cs = new CacheService(_logger, unitOfWork, ds,
            new ReadingItemService(Substitute.For<IArchiveService>(),
                Substitute.For<IBookService>(), Substitute.For<IImageService>(), ds, Substitute.For<ILogger<ReadingItemService>>()),
            Substitute.For<IBookmarkService>());

        var c = new ChapterBuilder("1")
            .WithFile(new MangaFileBuilder($"{DataDirectory}1.epub", MangaFormat.Epub).Build())
            .WithFile(new MangaFileBuilder($"{DataDirectory}2.epub", MangaFormat.Epub).Build())
            .Build();
        cs.GetCachedFile(c);
        Assert.Equal($"{DataDirectory}1.epub", cs.GetCachedFile(c));
    }

    #endregion

    #region GetCachedPagePath

    [Fact]
    public async Task GetCachedPagePath_ReturnNullIfNoFiles()
    {
        var (unitOfWork, context, _) = await CreateDatabase();

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
        var cs = new CacheService(_logger, unitOfWork, ds,
            new ReadingItemService(Substitute.For<IArchiveService>(),
                Substitute.For<IBookService>(), Substitute.For<IImageService>(), ds, Substitute.For<ILogger<ReadingItemService>>()),
            Substitute.For<IBookmarkService>());

        // Flatten to prepare for how GetFullPath expects
        ds.Flatten($"{CacheDirectory}1/");

        var path = cs.GetCachedPagePath(c.Id, 11);
        Assert.Equal(string.Empty, path);
    }

    [Fact]
    public async Task GetCachedPagePath_GetFileFromFirstFile()
    {
        var (unitOfWork, context, _) = await CreateDatabase();

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
        var cs = new CacheService(_logger, unitOfWork, ds,
            new ReadingItemService(Substitute.For<IArchiveService>(),
                Substitute.For<IBookService>(), Substitute.For<IImageService>(), ds, Substitute.For<ILogger<ReadingItemService>>()),
            Substitute.For<IBookmarkService>());

        // Flatten to prepare for how GetFullPath expects
        ds.Flatten($"{CacheDirectory}1/");

        Assert.Equal(ds.FileSystem.Path.GetFullPath($"{CacheDirectory}/1/000_001.jpg"), ds.FileSystem.Path.GetFullPath(cs.GetCachedPagePath(c.Id, 0)));

    }


    [Fact]
    public async Task GetCachedPagePath_GetLastPageFromSingleFile()
    {
        var (unitOfWork, context, _) = await CreateDatabase();

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
        var cs = new CacheService(_logger, unitOfWork, ds,
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
    public async Task GetCachedPagePath_GetFileFromSecondFile()
    {
        var (unitOfWork, context, _) = await CreateDatabase();

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
        var cs = new CacheService(_logger, unitOfWork, ds,
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
