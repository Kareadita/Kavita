using System.Collections.Concurrent;
using System.IO.Abstractions;
using System.IO.Abstractions.TestingHelpers;
using Hangfire;
using Kavita.API.Database;
using Kavita.API.Repositories;
using Kavita.API.Services;
using Kavita.API.Services.SignalR;
using Kavita.Common.Extensions;
using Kavita.Common.Helpers;
using Kavita.Database.Tests;
using Kavita.Models.Builders;
using Kavita.Models.Entities;
using Kavita.Models.Entities.Enums;
using Kavita.Models.Metadata;
using Kavita.Models.Parser;
using Kavita.Services.Scanner;
using Kavita.Services.Tests.Helpers;
using Microsoft.Extensions.Logging;
using NSubstitute;
using Xunit.Abstractions;

namespace Kavita.Services.Tests;

public class MockReadingItemService : IReadingItemService
{
    private readonly BasicParser _basicParser;
    private readonly ComicVineParser _comicVineParser;
    private readonly ImageParser _imageParser;
    private readonly BookParser _bookParser;
    private readonly PdfParser _pdfParser;

    public MockReadingItemService(IDirectoryService directoryService, IBookService bookService)
    {
        _imageParser = new ImageParser(directoryService);
        _basicParser = new BasicParser(directoryService, _imageParser);
        _bookParser = new BookParser(directoryService, bookService, _basicParser);
        _comicVineParser = new ComicVineParser(directoryService);
        _pdfParser = new PdfParser(directoryService);
    }

    public ComicInfo GetComicInfo(string filePath)
    {
        return null;
    }

    public int GetNumberOfPages(string filePath, MangaFormat format)
    {
        return 1;
    }

    public string GetCoverImage(string fileFilePath, string fileName, MangaFormat format, EncodeFormat encodeFormat, CoverImageSize size  = CoverImageSize.Default)
    {
        return string.Empty;
    }

    public void Extract(string fileFilePath, string targetDirectory, MangaFormat format, int imageCount = 1)
    {
        throw new NotImplementedException();
    }

    public ParserInfo? Parse(string path, string rootPath, string libraryRoot, LibraryType type, bool enableMetadata)
    {
        if (_comicVineParser.IsApplicable(path, type))
        {
            return _comicVineParser.Parse(path, rootPath, libraryRoot, type, enableMetadata, GetComicInfo(path)).Info;
        }
        if (_imageParser.IsApplicable(path, type))
        {
            return _imageParser.Parse(path, rootPath, libraryRoot, type, enableMetadata, GetComicInfo(path)).Info;
        }
        if (_bookParser.IsApplicable(path, type))
        {
            return _bookParser.Parse(path, rootPath, libraryRoot, type, enableMetadata, GetComicInfo(path)).Info;
        }
        if (_pdfParser.IsApplicable(path, type))
        {
            return _pdfParser.Parse(path, rootPath, libraryRoot, type, enableMetadata, GetComicInfo(path)).Info;
        }
        if (_basicParser.IsApplicable(path, type))
        {
            return _basicParser.Parse(path, rootPath, libraryRoot, type, enableMetadata, GetComicInfo(path)).Info;
        }

        return null;
    }

    public ParseFileResult ParseFile(string path, string rootPath, string libraryRoot, LibraryType type, bool enableMetadata)
    {
        return new ParseFileResult(Parse(path, rootPath, libraryRoot, type, enableMetadata));
    }
}

public class ParseScannedFilesTests: AbstractDbTest
{
    private readonly ILogger<ParseScannedFiles> _logger = Substitute.For<ILogger<ParseScannedFiles>>();
    private readonly ITestOutputHelper _outputHelper;

    public ParseScannedFilesTests(ITestOutputHelper testOutputHelper): base(testOutputHelper)
    {
        // Since ProcessFile relies on _readingItemService, we can implement our own versions of _readingItemService so we have control over how the calls work
        GlobalConfiguration.Configuration.UseInMemoryStorage();
        _outputHelper = testOutputHelper;
    }

    private Task<ScannerHelper> Setup(IUnitOfWork unitOfWork)
    {
        return Task.FromResult(new ScannerHelper(unitOfWork, _outputHelper));
    }

    #region MergeName

    // NOTE: I don't think I can test MergeName as it relies on Tracking Files, which is more complicated than I need
    // [Fact]
    // public async Task MergeName_ShouldMergeMatchingFormatAndName()
    // {
    //     var fileSystem = new MockFileSystem();
    //     fileSystem.AddDirectory("C:/Data/");
    //     fileSystem.AddFile("C:/Data/Accel World v1.cbz", new MockFileData(string.Empty));
    //     fileSystem.AddFile("C:/Data/Accel World v2.cbz", new MockFileData(string.Empty));
    //     fileSystem.AddFile("C:/Data/Accel World v2.pdf", new MockFileData(string.Empty));
    //
    //     var ds = new DirectoryService(Substitute.For<ILogger<DirectoryService>>(), fileSystem);
    //     var psf = new ParseScannedFiles(Substitute.For<ILogger<ParseScannedFiles>>(), ds,
    //         new MockReadingItemService(new DefaultParser(ds)), Substitute.For<IEventHub>());
    //
    //     var parsedSeries = new Dictionary<ParsedSeries, IList<ParserInfo>>();
    //     var parsedFiles = new ConcurrentDictionary<ParsedSeries, List<ParserInfo>>();
    //
    //     void TrackFiles(Tuple<bool, IList<ParserInfo>> parsedInfo)
    //     {
    //         var skippedScan = parsedInfo.Item1;
    //         var parsedFiles = parsedInfo.Item2;
    //         if (parsedFiles.Count == 0) return;
    //
    //         var foundParsedSeries = new ParsedSeries()
    //         {
    //             Name = parsedFiles.First().Series,
    //             NormalizedName = API.Parser.Parser.Normalize(parsedFiles.First().Series),
    //             Format = parsedFiles.First().Format
    //         };
    //
    //         parsedSeries.Add(foundParsedSeries, parsedFiles);
    //     }
    //
    //     await psf.ScanLibrariesForSeries(LibraryType.Manga, new List<string>() {"C:/Data/"}, "libraryName",
    //         false, await _unitOfWork.SeriesRepository.GetFolderPathMapAsync(1), TrackFiles);
    //
    //     Assert.Equal("Accel World",
    //         psf.MergeName(parsedFiles, ParserInfoFactory.CreateParsedInfo("Accel World", "1", "0", "Accel World v1.cbz", false)));
    //     Assert.Equal("Accel World",
    //         psf.MergeName(parsedFiles, ParserInfoFactory.CreateParsedInfo("accel_world", "1", "0", "Accel World v1.cbz", false)));
    //     Assert.Equal("Accel World",
    //         psf.MergeName(parsedFiles, ParserInfoFactory.CreateParsedInfo("accelworld", "1", "0", "Accel World v1.cbz", false)));
    // }
    //
    // [Fact]
    // public async Task MergeName_ShouldMerge_MismatchedFormatSameName()
    // {
    //     var fileSystem = new MockFileSystem();
    //     fileSystem.AddDirectory("C:/Data/");
    //     fileSystem.AddFile("C:/Data/Accel World v1.cbz", new MockFileData(string.Empty));
    //     fileSystem.AddFile("C:/Data/Accel World v2.cbz", new MockFileData(string.Empty));
    //     fileSystem.AddFile("C:/Data/Accel World v2.pdf", new MockFileData(string.Empty));
    //
    //     var ds = new DirectoryService(Substitute.For<ILogger<DirectoryService>>(), fileSystem);
    //     var psf = new ParseScannedFiles(Substitute.For<ILogger<ParseScannedFiles>>(), ds,
    //         new MockReadingItemService(new DefaultParser(ds)), Substitute.For<IEventHub>());
    //
    //
    //     await psf.ScanLibrariesForSeries(LibraryType.Manga, new List<string>() {"C:/Data/"}, "libraryName");
    //
    //     Assert.Equal("Accel World",
    //         psf.MergeName(ParserInfoFactory.CreateParsedInfo("Accel World", "1", "0", "Accel World v1.epub", false)));
    //     Assert.Equal("Accel World",
    //         psf.MergeName(ParserInfoFactory.CreateParsedInfo("accel_world", "1", "0", "Accel World v1.epub", false)));
    // }

    #endregion

    #region ScanLibrariesForSeries

    /// <summary>
    /// Test that when a folder has 2 series with a localizedSeries, they combine into one final series
    /// </summary>
    // [Fact]
    // public async Task ScanLibrariesForSeries_ShouldCombineSeries()
    // {
    //     // TODO: Implement these unit tests
    // }

    [Fact]
    public async Task ScanLibrariesForSeries_ShouldFindFiles()
    {
        var (unitOfWork, context, mapper) = await CreateDatabase();
        var scannerHelper = await Setup(unitOfWork);

        var fileSystem = new MockFileSystem();
        fileSystem.AddDirectory(Root + "Data/");
        fileSystem.AddFile(Root + "Data/Accel World v1.cbz", new MockFileData(string.Empty));
        fileSystem.AddFile(Root + "Data/Accel World v2.cbz", new MockFileData(string.Empty));
        fileSystem.AddFile(Root + "Data/Accel World v2.pdf", new MockFileData(string.Empty));
        fileSystem.AddFile(Root + "Data/Nothing.pdf", new MockFileData(string.Empty));

        var ds = new DirectoryService(Substitute.For<ILogger<DirectoryService>>(), fileSystem);
        var psf = new ParseScannedFiles(Substitute.For<ILogger<ParseScannedFiles>>(), ds,
            new MockReadingItemService(ds, Substitute.For<IBookService>()), Substitute.For<IEventHub>());


        var library =
            await unitOfWork.LibraryRepository.GetLibraryForIdAsync(1,
                LibraryIncludes.Folders | LibraryIncludes.FileTypes);
        Assert.NotNull(library);

        library.Type = LibraryType.Manga;
        var parsedSeries = await psf.ScanLibrariesForSeries(library, new List<string>() {Root + "Data/"}, false,
            await unitOfWork.SeriesRepository.GetFolderPathMapAsync(1));


        // Assert.Equal(3, parsedSeries.Values.Count);
        // Assert.NotEmpty(parsedSeries.Keys.Where(p => p.Format == MangaFormat.Archive && p.Name.Equals("Accel World")));

        Assert.Equal(3, parsedSeries.Count);
        Assert.Contains(parsedSeries.Select(p => p.ParsedSeries), p => p.Format == MangaFormat.Archive && p.Name.Equals("Accel World"));
    }

    #endregion


    #region ProcessFiles

    private static MockFileSystem CreateTestFilesystem()
    {
        var fileSystem = new MockFileSystem();
        fileSystem.AddDirectory("C:/Data/");
        fileSystem.AddDirectory("C:/Data/Accel World");
        fileSystem.AddDirectory("C:/Data/Accel World/Specials/");
        fileSystem.AddFile("C:/Data/Accel World/Accel World v1.cbz", new MockFileData(string.Empty));
        fileSystem.AddFile("C:/Data/Accel World/Accel World v2.cbz", new MockFileData(string.Empty));
        fileSystem.AddFile("C:/Data/Accel World/Accel World v2.pdf", new MockFileData(string.Empty));
        fileSystem.AddFile("C:/Data/Accel World/Specials/Accel World SP01.cbz", new MockFileData(string.Empty));
        fileSystem.AddFile("C:/Data/Black World/Black World SP01.cbz", new MockFileData(string.Empty));

        return fileSystem;
    }

    [Fact]
    public async Task ProcessFiles_ForLibraryMode_OnlyCallsFolderActionForEachTopLevelFolder()
    {
        var (unitOfWork, context, mapper) = await CreateDatabase();
        var scannerHelper = await Setup(unitOfWork);

        var fileSystem = CreateTestFilesystem();
        var ds = new DirectoryService(Substitute.For<ILogger<DirectoryService>>(), fileSystem);
        var psf = new ParseScannedFiles(Substitute.For<ILogger<ParseScannedFiles>>(), ds,
            new MockReadingItemService(ds, Substitute.For<IBookService>()), Substitute.For<IEventHub>());

        var directoriesSeen = new HashSet<string>();
        var library = await unitOfWork.LibraryRepository.GetLibraryForIdAsync(1,
                LibraryIncludes.Folders | LibraryIncludes.FileTypes);
        var scanResults = await psf.ScanFiles("C:/Data/", true, await unitOfWork.SeriesRepository.GetFolderPathMapAsync(1), library);
        foreach (var scanResult in scanResults)
        {
            directoriesSeen.Add(scanResult.Folder);
        }

        Assert.Equal(2, directoriesSeen.Count);
    }

    [Fact]
    public async Task ProcessFiles_ForNonLibraryMode_CallsFolderActionOnce()
    {
        var (unitOfWork, context, mapper) = await CreateDatabase();
        var scannerHelper = await Setup(unitOfWork);

        var fileSystem = CreateTestFilesystem();
        var ds = new DirectoryService(Substitute.For<ILogger<DirectoryService>>(), fileSystem);
        var psf = new ParseScannedFiles(Substitute.For<ILogger<ParseScannedFiles>>(), ds,
            new MockReadingItemService(ds, Substitute.For<IBookService>()), Substitute.For<IEventHub>());

        var library = await unitOfWork.LibraryRepository.GetLibraryForIdAsync(1,
            LibraryIncludes.Folders | LibraryIncludes.FileTypes);
        Assert.NotNull(library);

        var directoriesSeen = new HashSet<string>();
        var scanResults = await psf.ScanFiles("C:/Data/", false,
            await unitOfWork.SeriesRepository.GetFolderPathMapAsync(1), library);

        foreach (var scanResult in scanResults)
        {
            directoriesSeen.Add(scanResult.Folder);
        }

        Assert.Single(directoriesSeen);
        directoriesSeen.TryGetValue("C:/Data/", out var actual);
        Assert.Equal("C:/Data/", actual);
    }

    [Fact]
    public async Task ProcessFiles_ShouldCallFolderActionTwice()
    {
        var (unitOfWork, context, mapper) = await CreateDatabase();
        var scannerHelper = await Setup(unitOfWork);

        var fileSystem = new MockFileSystem();
        fileSystem.AddDirectory("C:/Data/");
        fileSystem.AddDirectory("C:/Data/Accel World");
        fileSystem.AddDirectory("C:/Data/Accel World/Specials/");
        fileSystem.AddFile("C:/Data/Accel World/Accel World v1.cbz", new MockFileData(string.Empty));
        fileSystem.AddFile("C:/Data/Accel World/Accel World v2.cbz", new MockFileData(string.Empty));
        fileSystem.AddFile("C:/Data/Accel World/Accel World v2.pdf", new MockFileData(string.Empty));
        fileSystem.AddFile("C:/Data/Accel World/Specials/Accel World SP01.cbz", new MockFileData(string.Empty));
        fileSystem.AddFile("C:/Data/Black World/Black World SP01.cbz", new MockFileData(string.Empty));

        var ds = new DirectoryService(Substitute.For<ILogger<DirectoryService>>(), fileSystem);
        var psf = new ParseScannedFiles(Substitute.For<ILogger<ParseScannedFiles>>(), ds,
            new MockReadingItemService(ds, Substitute.For<IBookService>()), Substitute.For<IEventHub>());

        var library = await unitOfWork.LibraryRepository.GetLibraryForIdAsync(1,
            LibraryIncludes.Folders | LibraryIncludes.FileTypes);
        Assert.NotNull(library);
        var scanResults = await psf.ScanFiles("C:/Data", true, await unitOfWork.SeriesRepository.GetFolderPathMapAsync(1), library);

        Assert.Equal(2, scanResults.Count);
    }


    /// <summary>
    /// Due to this not being a library, it's going to consider everything under C:/Data as being one folder aka a series folder
    /// </summary>
    [Fact]
    public async Task ProcessFiles_ShouldCallFolderActionOnce()
    {
        var (unitOfWork, context, mapper) = await CreateDatabase();
        var scannerHelper = await Setup(unitOfWork);

        var fileSystem = new MockFileSystem();
        fileSystem.AddDirectory("C:/Data/");
        fileSystem.AddDirectory("C:/Data/Accel World");
        fileSystem.AddDirectory("C:/Data/Accel World/Specials/");
        fileSystem.AddFile("C:/Data/Accel World/Accel World v1.cbz", new MockFileData(string.Empty));
        fileSystem.AddFile("C:/Data/Accel World/Accel World v2.cbz", new MockFileData(string.Empty));
        fileSystem.AddFile("C:/Data/Accel World/Accel World v2.pdf", new MockFileData(string.Empty));
        fileSystem.AddFile("C:/Data/Accel World/Specials/Accel World SP01.cbz", new MockFileData(string.Empty));
        fileSystem.AddFile("C:/Data/Black World/Black World SP01.cbz", new MockFileData(string.Empty));

        var ds = new DirectoryService(Substitute.For<ILogger<DirectoryService>>(), fileSystem);
        var psf = new ParseScannedFiles(Substitute.For<ILogger<ParseScannedFiles>>(), ds,
            new MockReadingItemService(ds, Substitute.For<IBookService>()), Substitute.For<IEventHub>());

        var library = await unitOfWork.LibraryRepository.GetLibraryForIdAsync(1,
            LibraryIncludes.Folders | LibraryIncludes.FileTypes);
        Assert.NotNull(library);
        var scanResults = await psf.ScanFiles("C:/Data", false,
            await unitOfWork.SeriesRepository.GetFolderPathMapAsync(1), library);

        Assert.Single(scanResults);
    }




    #endregion

    [Fact]
    public async Task HasSeriesFolderNotChangedSinceLastScan_AllSeriesFoldersHaveChanges()
    {
        var (unitOfWork, context, mapper) = await CreateDatabase();
        var scannerHelper = await Setup(unitOfWork);

        const string testcase = "Subfolders always scanning all series changes - Manga.json";
        var infos = new Dictionary<string, ComicInfo>();
        var library = await scannerHelper.GenerateScannerData(testcase, infos);
        var testDirectoryPath = library.Folders.First().Path;

        unitOfWork.LibraryRepository.Update(library);
        await unitOfWork.CommitAsync();

        var fs = new FileSystem();
        var ds = new DirectoryService(Substitute.For<ILogger<DirectoryService>>(), fs);
        var psf = new ParseScannedFiles(Substitute.For<ILogger<ParseScannedFiles>>(), ds,
            new MockReadingItemService(ds, Substitute.For<IBookService>()), Substitute.For<IEventHub>());

        var scanner = scannerHelper.CreateServices(ds, fs);
        await scanner.ScanLibrary(library.Id);

        var postLib = await unitOfWork.LibraryRepository.GetLibraryForIdAsync(library.Id, LibraryIncludes.Series);
        Assert.NotNull(postLib);
        Assert.Equal(4, postLib.Series.Count);

        var spiceAndWolf = postLib.Series.First(x => x.Name == "Spice and Wolf");
        Assert.Equal(2, spiceAndWolf.Volumes.Count);

        var frieren = postLib.Series.First(x => x.Name == "Frieren - Beyond Journey's End");
        Assert.Single(frieren.Volumes);

        var executionerAndHerWayOfLife = postLib.Series.First(x => x.Name == "The Executioner and Her Way of Life");
        Assert.Equal(2, executionerAndHerWayOfLife.Volumes.Count);

        await Task.Delay(1100); // Ensure at least one second has passed since library scan

        // Add a new chapter to a volume of the series, and scan. Only the folder that got the file is read again
        var executionerCopyDir = Path.Join(Path.Join(testDirectoryPath, "The Executioner and Her Way of Life"),
               "The Executioner and Her Way of Life Vol. 1");
        File.Copy(Path.Join(executionerCopyDir, "The Executioner and Her Way of Life Vol. 1 Ch. 0001.cbz"),
            Path.Join(executionerCopyDir, "The Executioner and Her Way of Life Vol. 1 Ch. 0002.cbz"));

        // 4 series folders plus the 6 volume folders that hold the files
        var folderMap = await unitOfWork.SeriesRepository.GetFolderPathMapAsync(postLib.Id);
        Assert.Equal(10, folderMap.Count);

        var res = await psf.ScanFiles(testDirectoryPath, true, folderMap, postLib);
        var changes = res.Where(sc => sc.HasChanged).ToList();
        var change = Assert.Single(changes);
        Assert.EndsWith("The Executioner and Her Way of Life Vol. 1", change.Folder);
    }

    [Fact]
    public async Task HasSeriesFolderNotChangedSinceLastScan_PublisherLayout()
    {
        var (unitOfWork, context, mapper) = await CreateDatabase();
        var scannerHelper = await Setup(unitOfWork);

        const string testcase = "Subfolder always scanning fix publisher layout - Comic.json";
        var infos = new Dictionary<string, ComicInfo>();
        var library = await scannerHelper.GenerateScannerData(testcase, infos);
        var testDirectoryPath = library.Folders.First().Path;

        unitOfWork.LibraryRepository.Update(library);
        await unitOfWork.CommitAsync();

        var fs = new FileSystem();
        var ds = new DirectoryService(Substitute.For<ILogger<DirectoryService>>(), fs);
        var psf = new ParseScannedFiles(Substitute.For<ILogger<ParseScannedFiles>>(), ds,
            new MockReadingItemService(ds, Substitute.For<IBookService>()), Substitute.For<IEventHub>());

        var scanner = scannerHelper.CreateServices(ds, fs);
        await scanner.ScanLibrary(library.Id);

        var postLib = await unitOfWork.LibraryRepository.GetLibraryForIdAsync(library.Id, LibraryIncludes.Series);
        Assert.NotNull(postLib);
        Assert.Equal(4, postLib.Series.Count);

        var spiceAndWolf = postLib.Series.First(x => x.Name == "Spice and Wolf");
        Assert.Equal(2, spiceAndWolf.Volumes.Count);

        var frieren = postLib.Series.First(x => x.Name == "Frieren - Beyond Journey's End");
        Assert.Equal(2, frieren.Volumes.Count);

        await Task.Delay(1100); // Ensure at least one second has passed since library scan

        // Add a volume to a series, and scan. Ensure only this series is marked as HasChanged
        var executionerCopyDir = Path.Join(Path.Join(testDirectoryPath, "YenPress"), "The Executioner and Her Way of Life");
        File.Copy(Path.Join(executionerCopyDir, "The Executioner and Her Way of Life Vol. 1.cbz"),
            Path.Join(executionerCopyDir, "The Executioner and Her Way of Life Vol. 2.cbz"));

        var res = await psf.ScanFiles(testDirectoryPath, true,
            await unitOfWork.SeriesRepository.GetFolderPathMapAsync(postLib.Id), postLib);
        var changes = res.Count(sc => sc.HasChanged);
        Assert.Equal(1, changes);
    }

    [Fact]
    public async Task ScanLibrariesForSeries_OneSeriesChanged_OnlyThatSeriesHasChanged()
    {
        var (unitOfWork, _, _) = await CreateDatabase();
        var scannerHelper = await Setup(unitOfWork);

        const string testcase = "Subfolder always scanning fix publisher layout - Comic.json";
        var library = await scannerHelper.GenerateScannerData(testcase, new Dictionary<string, ComicInfo>());
        var testDirectoryPath = library.Folders.First().Path;

        unitOfWork.LibraryRepository.Update(library);
        await unitOfWork.CommitAsync();

        var fs = new FileSystem();
        var ds = new DirectoryService(Substitute.For<ILogger<DirectoryService>>(), fs);
        var psf = new ParseScannedFiles(Substitute.For<ILogger<ParseScannedFiles>>(), ds,
            new MockReadingItemService(ds, Substitute.For<IBookService>()), Substitute.For<IEventHub>());

        var scanner = scannerHelper.CreateServices(ds, fs);
        await scanner.ScanLibrary(library.Id);

        var postLib = await unitOfWork.LibraryRepository.GetLibraryForIdAsync(library.Id, LibraryIncludes.Series);
        Assert.NotNull(postLib);
        Assert.Equal(4, postLib.Series.Count);

        await Task.Delay(1100); // Ensure at least one second has passed since library scan

        var executionerDir = Path.Join(Path.Join(testDirectoryPath, "YenPress"), "The Executioner and Her Way of Life");
        File.Copy(Path.Join(executionerDir, "The Executioner and Her Way of Life Vol. 1.cbz"),
            Path.Join(executionerDir, "The Executioner and Her Way of Life Vol. 2.cbz"));

        var res = await psf.ScanLibrariesForSeries(postLib, [testDirectoryPath], true,
            await unitOfWork.SeriesRepository.GetFolderPathMapAsync(postLib.Id));

        Assert.Equal(4, res.Count);
        var changed = Assert.Single(res, r => r.HasChanged);
        Assert.Equal("The Executioner and Her Way of Life", changed.ParsedSeries.Name);
    }

    [Fact]
    public async Task SubFoldersNoSubFolders_SkipAll()
    {
        var (unitOfWork, context, mapper) = await CreateDatabase();
        var scannerHelper = await Setup(unitOfWork);

        const string testcase = "Subfolders and files at root - Manga.json";
        var infos = new Dictionary<string, ComicInfo>();
        var library = await scannerHelper.GenerateScannerData(testcase, infos);
        var testDirectoryPath = library.Folders.First().Path;

        unitOfWork.LibraryRepository.Update(library);
        await unitOfWork.CommitAsync();

        var fs = new FileSystem();
        var ds = new DirectoryService(Substitute.For<ILogger<DirectoryService>>(), fs);
        var psf = new ParseScannedFiles(Substitute.For<ILogger<ParseScannedFiles>>(), ds,
            new MockReadingItemService(ds, Substitute.For<IBookService>()), Substitute.For<IEventHub>());

        var scanner = scannerHelper.CreateServices(ds, fs);
        await scanner.ScanLibrary(library.Id);

        var postLib = await unitOfWork.LibraryRepository.GetLibraryForIdAsync(library.Id, LibraryIncludes.Series);
        Assert.NotNull(postLib);
        Assert.Single(postLib.Series);

        var spiceAndWolf = postLib.Series.First(x => x.Name == "Spice and Wolf");
        Assert.Equal(3, spiceAndWolf.Volumes.Count);
        Assert.Equal(4, spiceAndWolf.Volumes.Sum(v => v.Chapters.Count));

        // Needs to be actual time as the write time is now, so if we set LastFolderChecked in the past
        // it'll always a scan as it was changed since the last scan.
        await Task.Delay(1100); // Ensure at least one second has passed since library scan

        var res = await psf.ScanFiles(testDirectoryPath, true,
            await unitOfWork.SeriesRepository.GetFolderPathMapAsync(postLib.Id), postLib);
        Assert.DoesNotContain(res, sc => sc.HasChanged);
    }

    [Fact]
    public async Task SubFoldersNoSubFolders_ScanAllAfterAddInRoot()
    {
        var (unitOfWork, context, mapper) = await CreateDatabase();
        var scannerHelper = await Setup(unitOfWork);

        const string testcase = "Subfolders and files at root - Manga.json";
        var infos = new Dictionary<string, ComicInfo>();
        var library = await scannerHelper.GenerateScannerData(testcase, infos);
        var testDirectoryPath = library.Folders.First().Path;

        unitOfWork.LibraryRepository.Update(library);
        await unitOfWork.CommitAsync();

        var fs = new FileSystem();
        var ds = new DirectoryService(Substitute.For<ILogger<DirectoryService>>(), fs);
        var psf = new ParseScannedFiles(Substitute.For<ILogger<ParseScannedFiles>>(), ds,
            new MockReadingItemService(ds, Substitute.For<IBookService>()), Substitute.For<IEventHub>());

        var scanner = scannerHelper.CreateServices(ds, fs);
        await scanner.ScanLibrary(library.Id);

        var postLib = await unitOfWork.LibraryRepository.GetLibraryForIdAsync(library.Id, LibraryIncludes.Series);
        Assert.NotNull(postLib);
        Assert.Single(postLib.Series);

        var spiceAndWolf = postLib.Series.First(x => x.Name == "Spice and Wolf");
        Assert.Equal(3, spiceAndWolf.Volumes.Count);
        Assert.Equal(4, spiceAndWolf.Volumes.Sum(v => v.Chapters.Count));

        spiceAndWolf.LastFolderScanned = DateTime.Now.Subtract(TimeSpan.FromMinutes(2));
        context.Series.Update(spiceAndWolf);
        await context.SaveChangesAsync();

        // Add file at series root
        var spiceAndWolfDir = Path.Join(testDirectoryPath, "Spice and Wolf");
        File.Copy(Path.Join(spiceAndWolfDir, "Spice and Wolf Vol. 1.cbz"),
            Path.Join(spiceAndWolfDir, "Spice and Wolf Vol. 4.cbz"));

        var res = await psf.ScanFiles(testDirectoryPath, true,
            await unitOfWork.SeriesRepository.GetFolderPathMapAsync(postLib.Id), postLib);
        var changes = res.Count(sc => sc.HasChanged);
        Assert.Equal(2, changes);
    }

    [Fact]
    public async Task SubFoldersNoSubFolders_ScanAllAfterAddInSubFolder()
    {
        var (unitOfWork, context, mapper) = await CreateDatabase();
        var scannerHelper = await Setup(unitOfWork);

        const string testcase = "Subfolders and files at root - Manga.json";
        var infos = new Dictionary<string, ComicInfo>();
        var library = await scannerHelper.GenerateScannerData(testcase, infos);
        var testDirectoryPath = library.Folders.First().Path;

        unitOfWork.LibraryRepository.Update(library);
        await unitOfWork.CommitAsync();

        var fs = new FileSystem();
        var ds = new DirectoryService(Substitute.For<ILogger<DirectoryService>>(), fs);
        var psf = new ParseScannedFiles(Substitute.For<ILogger<ParseScannedFiles>>(), ds,
            new MockReadingItemService(ds, Substitute.For<IBookService>()), Substitute.For<IEventHub>());

        var scanner = scannerHelper.CreateServices(ds, fs);
        await scanner.ScanLibrary(library.Id);

        var postLib = await unitOfWork.LibraryRepository.GetLibraryForIdAsync(library.Id, LibraryIncludes.Series);
        Assert.NotNull(postLib);
        Assert.Single(postLib.Series);

        var spiceAndWolf = postLib.Series.First(x => x.Name == "Spice and Wolf");
        Assert.Equal(3, spiceAndWolf.Volumes.Count);
        Assert.Equal(4, spiceAndWolf.Volumes.Sum(v => v.Chapters.Count));

        spiceAndWolf.LastFolderScanned = DateTime.Now.Subtract(TimeSpan.FromMinutes(2));
        context.Series.Update(spiceAndWolf);
        await context.SaveChangesAsync();

        // Add file in subfolder
        var spiceAndWolfDir = Path.Join(Path.Join(testDirectoryPath, "Spice and Wolf"), "Spice and Wolf Vol. 3");
        File.Copy(Path.Join(spiceAndWolfDir, "Spice and Wolf Vol. 3 Ch. 0011.cbz"),
            Path.Join(spiceAndWolfDir, "Spice and Wolf Vol. 3 Ch. 0013.cbz"));

        var res = await psf.ScanFiles(testDirectoryPath, true,
            await unitOfWork.SeriesRepository.GetFolderPathMapAsync(postLib.Id), postLib);
        var changes = res.Count(sc => sc.HasChanged);
        Assert.Equal(2, changes);
    }

    [Fact]
    public async Task ScanSingleDirectory_LowestFolderPathIsDriveRoot_ReadsTheUnchangedSeriesFolder()
    {
        const string file = "M:/Higurashi When They Cry/Higurashi When They Cry v01.cbz";
        var writeTime = DateTime.UtcNow.AddDays(-1);
        var ds = Substitute.For<IDirectoryService>();
        ds.ScanFiles(default!, default!).ReturnsForAnyArgs([new FileStamp(file, 10, writeTime)]);
        var psf = new ParseScannedFiles(Substitute.For<ILogger<ParseScannedFiles>>(), ds,
            Substitute.For<IReadingItemService>(), Substitute.For<IEventHub>());

        var library = new LibraryBuilder("Manga").WithFolderPath(new FolderPathBuilder("M:/").Build()).Build();
        var seriesPaths = new Dictionary<string, IList<SeriesModified>>
        {
            ["M:/Higurashi When They Cry"] =
            [
                new SeriesModified
                {
                    SeriesName = "Higurashi When They Cry",
                    FolderPath = "M:/Higurashi When They Cry",
                    LowestFolderPath = "M:",
                    LastScanned = DateTime.Now,
                    LibraryRoots = ["M:/"],
                    FilesByFolder = new Dictionary<string, IReadOnlyList<KnownFile>>
                    {
                        ["M:/Higurashi When They Cry"] = [new KnownFile(1, file, 10, writeTime)],
                    },
                },
            ],
        };

        var result = await psf.ScanFiles("M:/Higurashi When They Cry", false, seriesPaths, library);

        Assert.True(Assert.Single(result).HasChanged);
        ds.Received(1).ScanFiles(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<GlobMatcher?>(), Arg.Any<SearchOption>());
        ds.Received(1).ScanFiles("M:/Higurashi When They Cry", Arg.Any<string>(), Arg.Any<GlobMatcher?>(), Arg.Any<SearchOption>());
    }

    [Fact]
    public void TrackSeriesAcrossScanResults_MergingEverythingIntoSeriesWithNoMeaningfulInformation()
    {
        List<ScanResult> scanResults =
        [
            new()
            {
               ParserInfos = [
                   // Should be ignored as the series does not contain meaningful information
                   // Would previously suck all series into it (when having no localised series set)
                   new ParserInfo
                   {
                       Series = "[&/"
                   },
                   new ParserInfo
                   {
                       Series = "Spice and Wolf"
                   }
                   ,new ParserInfo
                   {
                       Series = "Ikoku Nikki"
                   }
               ]
            }
        ];

        ConcurrentDictionary<ParsedSeries, List<ParserInfo>> scannedSeries = [];

        var psd = new ParseScannedFiles(Substitute.For<ILogger<ParseScannedFiles>>(), Substitute.For<IDirectoryService>(),
            Substitute.For<IReadingItemService>(), Substitute.For<IEventHub>());

        psd.TrackSeriesAcrossScanResults(scanResults, scannedSeries);

        Assert.Equal(2, scannedSeries.Count);
    }

    [Fact]
    public void TrackSeriesAcrossScanResults_Merging()
    {
        List<ScanResult> scanResults =
        [
            new()
            {
                ParserInfos = [
                    new ParserInfo
                    {
                        Series = "Spice and Wolf"
                    }
                    ,new ParserInfo
                    {
                        Series = "Ookami to Koushinryou",
                        LocalizedSeries = "Spice and Wolf"
                    }
                ]
            }
        ];

        ConcurrentDictionary<ParsedSeries, List<ParserInfo>> scannedSeries = [];

        var psd = new ParseScannedFiles(Substitute.For<ILogger<ParseScannedFiles>>(), Substitute.For<IDirectoryService>(),
            Substitute.For<IReadingItemService>(), Substitute.For<IEventHub>());

        psd.TrackSeriesAcrossScanResults(scanResults, scannedSeries);

        Assert.Single(scannedSeries);
        Assert.Single(scannedSeries.Values.First().DistinctBy(x => x.Series));
    }

    #region ParseFiles Concurrency

    /// <summary>
    /// Wraps <see cref="MockReadingItemService"/> and records how many ParseFile calls run
    /// concurrently, so we can assert the parallel parse path is bounded.
    /// </summary>
    private sealed class ConcurrencyTrackingReadingItemService : IReadingItemService
    {
        private readonly MockReadingItemService _inner;
        private int _current;
        private int _max;
        private int _totalCalls;

        public int MaxObservedConcurrency => _max;
        public int TotalCalls => _totalCalls;

        public ConcurrencyTrackingReadingItemService(IDirectoryService directoryService, IBookService bookService)
        {
            _inner = new MockReadingItemService(directoryService, bookService);
        }

        public ParseFileResult ParseFile(string path, string rootPath, string libraryRoot, LibraryType type, bool enableMetadata)
        {
            Interlocked.Increment(ref _totalCalls);
            var running = Interlocked.Increment(ref _current);
            int observedMax;
            while (running > (observedMax = _max))
            {
                Interlocked.CompareExchange(ref _max, running, observedMax);
            }

            try
            {
                // Hold the slot briefly so concurrent parses actually overlap
                Thread.Sleep(10);

                return _inner.ParseFile(path, rootPath, libraryRoot, type, enableMetadata);
            }
            finally
            {
                Interlocked.Decrement(ref _current);
            }
        }

        public int GetNumberOfPages(string filePath, MangaFormat format) => _inner.GetNumberOfPages(filePath, format);

        public string GetCoverImage(string fileFilePath, string fileName, MangaFormat format, EncodeFormat encodeFormat, CoverImageSize size = CoverImageSize.Default)
            => _inner.GetCoverImage(fileFilePath, fileName, format, encodeFormat, size);

        public void Extract(string fileFilePath, string targetDirectory, MangaFormat format, int imageCount = 1)
            => _inner.Extract(fileFilePath, targetDirectory, format, imageCount);
    }

    [Fact]
    public async Task ScanLibrariesForSeries_LargeFolder_BoundsParseConcurrencyAndParsesEveryFile()
    {
        var (unitOfWork, context, mapper) = await CreateDatabase();
        _ = await Setup(unitOfWork);

        // A single folder with >= 100 files takes the parallel parse path in ParseFiles
        const int fileCount = 150;
        var fileSystem = new MockFileSystem();
        fileSystem.AddDirectory(Root + "Data/");
        for (var i = 0; i < fileCount; i++)
        {
            fileSystem.AddFile(Root + $"Data/Accel World v{i:D3}.cbz", new MockFileData(string.Empty));
        }

        var ds = new DirectoryService(Substitute.For<ILogger<DirectoryService>>(), fileSystem);
        var readingItemService = new ConcurrencyTrackingReadingItemService(ds, Substitute.For<IBookService>());
        var psf = new ParseScannedFiles(Substitute.For<ILogger<ParseScannedFiles>>(), ds,
            readingItemService, Substitute.For<IEventHub>());

        var library = await unitOfWork.LibraryRepository.GetLibraryForIdAsync(1,
            LibraryIncludes.Folders | LibraryIncludes.FileTypes);
        Assert.NotNull(library);
        library.Type = LibraryType.Manga;

        await psf.ScanLibrariesForSeries(library, new List<string> { Root + "Data/" }, false,
            await unitOfWork.SeriesRepository.GetFolderPathMapAsync(1));

        // Every file still goes through the parser
        Assert.Equal(fileCount, readingItemService.TotalCalls);

        // ...and the parallel parse path never exceeds the scanner's concurrency cap.
        // Before the fix this branch was an unbounded Task.WhenAll over one Task.Run per file,
        // which let all 150 parses run at once and exhaust the ThreadPool.
        var expectedMax = Math.Max(1, Environment.ProcessorCount / 2);
        Assert.True(readingItemService.MaxObservedConcurrency <= expectedMax,
            $"Observed parse concurrency {readingItemService.MaxObservedConcurrency} exceeded the cap {expectedMax}");
    }

    #endregion

    #region Failed Files

    private const string MurderbotFolder = "B:/Fiction/Martha Wells/The Murderbot Diaries";
    private static readonly DateTime FailedWriteTime = new(2026, 10, 5, 12, 0, 0, DateTimeKind.Utc);
    private static readonly FileStamp Broken = new($"{MurderbotFolder}/Fugitive Telemetry.epub", 100, FailedWriteTime);
    private static readonly FileStamp Readable = new($"{MurderbotFolder}/All Systems Red.epub", 200, FailedWriteTime);
    private static readonly ParseIssue CorruptEpub = new(MediaErrorReason.CorruptEpub, "broken", "details");

    private static ParseScannedFiles FailedFileScanner(IDirectoryService ds, IReadingItemService? reader = null)
    {
        return new ParseScannedFiles(Substitute.For<ILogger<ParseScannedFiles>>(), ds,
            reader ?? Substitute.For<IReadingItemService>(), Substitute.For<IEventHub>());
    }

    private static IDirectoryService ListingOf(params FileStamp[] files)
    {
        var ds = Substitute.For<IDirectoryService>();
        ds.GetAllDirectories(default!).ReturnsForAnyArgs([MurderbotFolder]);
        ds.ScanFiles(default!, default!).ReturnsForAnyArgs(files);
        return ds;
    }

    private static IReadingItemService ReaderReturning(Func<string, ParseFileResult> resultForPath)
    {
        var reader = Substitute.For<IReadingItemService>();
        reader.ParseFile(default!, default!, default!, default, default).ReturnsForAnyArgs(ci => resultForPath(ci.ArgAt<string>(0)));
        return reader;
    }

    private static ParserInfo MurderbotInfo(FileStamp file, string series = "The Murderbot Diaries")
    {
        return new ParserInfo
        {
            Series = series,
            Filename = Path.GetFileName(file.Path),
            FullFilePath = file.Path,
            Format = MangaFormat.Epub,
            Volumes = Parser.LooseLeafVolume,
            Chapters = Parser.DefaultChapter,
        };
    }

    private static Library BooksLibrary()
    {
        return new LibraryBuilder("Books", LibraryType.Book).WithFolderPath(new FolderPathBuilder("B:/Fiction").Build()).Build();
    }

    private static FailedFile AsFailedFile(FileStamp stamp) => new(stamp.Path, stamp.Bytes, stamp.LastWriteTimeUtc);

    private static SeriesModified Owner(string name, params FileStamp[] files)
    {
        return new SeriesModified
        {
            SeriesName = name,
            FolderPath = MurderbotFolder,
            LowestFolderPath = MurderbotFolder,
            LastScanned = DateTime.Now,
            LibraryRoots = ["B:/Fiction"],
            FilesByFolder = new Dictionary<string, IReadOnlyList<KnownFile>>
            {
                [MurderbotFolder] = files.Select((f, i) => new KnownFile(i + 1, f.Path, f.Bytes, f.LastWriteTimeUtc)).ToList(),
            },
        };
    }

    private static Dictionary<string, IList<SeriesModified>> FolderOwnedBy(params SeriesModified[] owners)
    {
        return new Dictionary<string, IList<SeriesModified>> { [MurderbotFolder] = owners.ToList() };
    }

    private static Dictionary<string, IList<SeriesModified>> MurderbotOwnsReadable()
    {
        return FolderOwnedBy(Owner("The Murderbot Diaries", Readable));
    }

    [Theory]
    [InlineData(false, false)]
    [InlineData(true, true)]
    public async Task ScanFiles_FolderWithOnlyAFailedFile_IsSkippedUnlessForced(bool forceCheck, bool expectedChanged)
    {
        var psf = FailedFileScanner(ListingOf(Broken));

        var result = await psf.ScanFiles("B:/Fiction", true, new Dictionary<string, IList<SeriesModified>>(), BooksLibrary(),
            forceCheck, [AsFailedFile(Broken)]);

        Assert.Equal(expectedChanged, Assert.Single(result).HasChanged);
    }

    [Theory]
    [InlineData(true, false)]
    [InlineData(false, true)]
    public async Task ScanFiles_SeriesFolderWithAFailedFile_IsSkippedWhileTheRowMatches(bool hasRow, bool expectedChanged)
    {
        var psf = FailedFileScanner(ListingOf(Readable, Broken));

        var result = await psf.ScanFiles(MurderbotFolder, false, MurderbotOwnsReadable(), BooksLibrary(),
            failedFiles: hasRow ? [AsFailedFile(Broken)] : []);

        Assert.Equal(expectedChanged, Assert.Single(result).HasChanged);
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task ScanFiles_LooseFailedFileInParentFolder_IsSkippedWhileTheRowMatches(bool hasRow)
    {
        const string author = "B:/Fiction/Martha Wells";
        var loose = new FileStamp($"{author}/Compulsory.epub", 300, FailedWriteTime);
        var ds = Substitute.For<IDirectoryService>();
        ds.GetAllDirectories(default!).ReturnsForAnyArgs([author, MurderbotFolder]);
        ds.ScanFiles(MurderbotFolder, Arg.Any<string>(), Arg.Any<GlobMatcher?>(), Arg.Any<SearchOption>()).Returns([Readable]);
        ds.ScanFiles(author, Arg.Any<string>(), Arg.Any<GlobMatcher?>(), SearchOption.TopDirectoryOnly).Returns([loose]);
        var psf = FailedFileScanner(ds);

        var result = await psf.ScanFiles("B:/Fiction", true, new Dictionary<string, IList<SeriesModified>>(), BooksLibrary(),
            failedFiles: hasRow ? [AsFailedFile(loose)] : []);

        var authorResult = result.SingleOrDefault(r => r.Folder == author);
        if (hasRow)
        {
            Assert.Null(authorResult);
        }
        else
        {
            Assert.Contains(loose, authorResult!.Files);
        }
    }

    [Fact]
    public async Task ScanFiles_SeriesFolderNotInMap_FailedFileDoesNotSkipIt()
    {
        var psf = FailedFileScanner(ListingOf(Broken));

        var result = await psf.ScanFiles(MurderbotFolder, false, new Dictionary<string, IList<SeriesModified>>(), BooksLibrary(),
            failedFiles: [AsFailedFile(Broken)]);

        Assert.True(Assert.Single(result).HasChanged);
    }

    [Theory]
    [InlineData(3)]
    [InlineData(120)]
    public async Task ScanLibrariesForSeries_FailedParse_IsCollectedWithItsStamp(int fileCount)
    {
        var files = Enumerable.Range(0, fileCount)
            .Select(i => new FileStamp($@"B:\Fiction\Martha Wells\The Murderbot Diaries\Book {i}.epub", 100 + i, FailedWriteTime.AddMinutes(i)))
            .ToArray();
        var failing = files.Where((_, i) => i % 2 == 1).ToList();
        var failingPaths = failing.Select(f => f.Path).ToHashSet();
        var reader = ReaderReturning(path => failingPaths.Contains(path) ? ParseFileResult.Failed(CorruptEpub) : new ParseFileResult(null));
        var psf = FailedFileScanner(ListingOf(files), reader);

        await psf.ScanLibrariesForSeries(BooksLibrary(), ["B:/Fiction"], true, new Dictionary<string, IList<SeriesModified>>());

        var expected = failing
            .Select(f => new ScanIssue(f.Path.Replace('\\', '/'), f.Bytes, f.LastWriteTimeUtc, CorruptEpub.Reason, CorruptEpub.Comment, CorruptEpub.Details))
            .ToList();
        Assert.Equal(expected, psf.Issues);
    }

    [Theory]
    [InlineData("")]
    [InlineData("[&/")]
    public async Task ScanLibrariesForSeries_NoUsableSeriesName_IsCollectedWithItsStamp(string series)
    {
        var reader = ReaderReturning(_ => new ParseFileResult(MurderbotInfo(Broken, series)));
        var psf = FailedFileScanner(ListingOf(Broken), reader);

        var result = await psf.ScanLibrariesForSeries(BooksLibrary(), ["B:/Fiction"], true, new Dictionary<string, IList<SeriesModified>>());

        Assert.Empty(result);
        var issue = Assert.Single(psf.Issues);
        Assert.Equal((Broken.Path, Broken.Bytes, Broken.LastWriteTimeUtc), (issue.Path, issue.Bytes, issue.LastWriteTimeUtc));
        Assert.Equal(MediaErrorReason.NoSeriesName, issue.Reason);
    }

    [Fact]
    public async Task ScanLibrariesForSeries_ImportedFileWithAnIssue_IsKeptWithTheIssue()
    {
        var notStrict = new ParseIssue(MediaErrorReason.EpubNotStrict, "lenient", "navigation file is not a valid XHTML file");
        var reader = ReaderReturning(_ => new ParseFileResult(MurderbotInfo(Broken), notStrict));
        var psf = FailedFileScanner(ListingOf(Broken), reader);

        var result = await psf.ScanLibrariesForSeries(BooksLibrary(), ["B:/Fiction"], true, new Dictionary<string, IList<SeriesModified>>());

        Assert.Single(result);
        Assert.Equal(MediaErrorReason.EpubNotStrict, Assert.Single(psf.Issues).Reason);
    }

    [Fact]
    public async Task ScanLibrariesForSeries_RejectedFile_ReplacesItsMetadataIssue()
    {
        var unreadable = new ParseIssue(MediaErrorReason.MetadataUnreadable, "metadata", "details");
        var reader = ReaderReturning(_ => new ParseFileResult(MurderbotInfo(Broken, "[&/"), unreadable));
        var psf = FailedFileScanner(ListingOf(Broken), reader);

        await psf.ScanLibrariesForSeries(BooksLibrary(), ["B:/Fiction"], true, new Dictionary<string, IList<SeriesModified>>());

        Assert.Equal(MediaErrorReason.NoSeriesName, Assert.Single(psf.Issues).Reason);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task ScanLibrariesForSeries_FailedFileThatParsesNow_IsResolved(bool forceCheck)
    {
        var replaced = Broken with { Bytes = Broken.Bytes + 1 };
        var reader = ReaderReturning(_ => new ParseFileResult(MurderbotInfo(replaced)));
        var psf = FailedFileScanner(ListingOf(replaced), reader);

        await psf.ScanLibrariesForSeries(BooksLibrary(), ["B:/Fiction"], true, new Dictionary<string, IList<SeriesModified>>(),
            forceCheck, [AsFailedFile(Broken)]);

        Assert.Empty(psf.Issues);
        Assert.Equal([Broken.Path], psf.ResolvedFailedFiles);
    }

    [Fact]
    public async Task ScanLibrariesForSeries_FailedFileDeleted_IsResolved()
    {
        var psf = FailedFileScanner(ListingOf(Readable), ReaderReturning(_ => new ParseFileResult(MurderbotInfo(Readable))));

        await psf.ScanLibrariesForSeries(BooksLibrary(), ["B:/Fiction"], true, MurderbotOwnsReadable(), false, [AsFailedFile(Broken)]);

        Assert.Equal([Broken.Path], psf.ResolvedFailedFiles);
    }

    [Fact]
    public async Task ScanLibrariesForSeries_FailedFileStillFailing_IsNotResolved()
    {
        var replaced = Broken with { Bytes = Broken.Bytes + 1 };
        var psf = FailedFileScanner(ListingOf(replaced), ReaderReturning(_ => ParseFileResult.Failed(CorruptEpub)));

        await psf.ScanLibrariesForSeries(BooksLibrary(), ["B:/Fiction"], true, new Dictionary<string, IList<SeriesModified>>(),
            false, [AsFailedFile(Broken)]);

        Assert.Empty(psf.ResolvedFailedFiles);
        Assert.Equal(replaced.Bytes, Assert.Single(psf.Issues).Bytes);
    }

    [Fact]
    public async Task ScanLibrariesForSeries_UnchangedFolder_KeepsItsFailedFile()
    {
        var psf = FailedFileScanner(ListingOf(Broken));

        await psf.ScanLibrariesForSeries(BooksLibrary(), ["B:/Fiction"], true, new Dictionary<string, IList<SeriesModified>>(),
            false, [AsFailedFile(Broken)]);

        Assert.Empty(psf.Issues);
        Assert.Empty(psf.ResolvedFailedFiles);
    }

    #endregion

    #region Sort Order

    public static IEnumerable<object[]> UpdateSortOrderData => new List<object[]>
    {
        // All whole numbers
        new object[] { new[] { "1", "2", "3" }, new[] { 1f, 2f, 3f } },

        // Whole and float numbers
        new object[] { new[] { "1", "2.5", "3" }, new[] { 1f, 2.5f, 3f } },

        // Whole ranges (uses min of range)
        new object[] { new[] { "1-3", "4-6" }, new[] { 1f, 4f } },

        // Whole, float, and ranges mixed
        new object[] { new[] { "1", "2.5", "4-6" }, new[] { 1f, 2.5f, 4f } },

        // Out-of-order input still resolves correctly per item
        new object[] { new[] { "3", "1", "2" }, new[] { 3f, 1f, 2f } },

        // Original: single nonsense suffix
        new object[] { new[] { "15", "15.HU" }, new[] { 15f, 15.1f } },

        // Original: story A/B suffixes
        new object[] { new[] { "15", "15 (A Story)", "15 (B Story)" }, new[] { 15f, 15.1f, 15.2f } },
        // Ensure "real" numbers are sorted first
        new object[] { new[] { "01  (A Story)", "1", "01 (B Story)" }, new[] { 1.1f, 1f, 1.2f } },

        // Two different nonsense suffixes on the same base
        // BEY comes before UH
        new object[] { new[] { "15", "15.UH", "15.BEY" }, new[] { 15f, 15.2f, 15.1f } },

        // Duplicate whole numbers (no suffix at all)
        new object[] { new[] { "15", "15" }, new[] { 15f, 15.1f } },

        // Nonsense suffixes with different bases: no bump between them
        new object[] { new[] { "15.UH", "16.BEY" }, new[] { 15f, 16f } },

        // Decimal base with story suffixes
        new object[] { new[] { "15.5 (A Story)", "15.5 (B Story)" }, new[] { 15.5f, 15.6f } },

        // Range collides with a plain duplicate of its min value. Non range goes first; numbers get priority
        new object[] { new[] { "4-6", "4" }, new[] { 4.1f, 4f } },

        // Three-way duplicate nonsense chain
        new object[] { new[] { "15", "15.HU", "15.LR" }, new[] { 15f, 15.1f, 15.2f } },

        // Different base values with nonsense after
        new object[] { new[] { "15", "15.HU", "16", "16 (A Story)", "17" }, new[] { 15f, 15.1f, 16, 16.1f, 17f } },
    };

    [Theory]
    [MemberData(nameof(UpdateSortOrderData))]
    public void TestUpdateSortOrder(string[] chapters, float[] expectedOrders)
    {
        var series = new ParsedSeries
        {
            Name = "Spice and Wolf",
            NormalizedName = "Spice and Wolf".ToNormalized(),
            Format = MangaFormat.Archive
        };

        ConcurrentDictionary<ParsedSeries, List<ParserInfo>> scannedSeries = [];
        scannedSeries[series] = chapters
            .Select(c => new ParserInfo
            {
                Series = "Spice and Wolf",
                Chapters = c
            })
            .ToList();

        ParseScannedFiles.UpdateSortOrder(scannedSeries, series);

        for (var i = 0; i < expectedOrders.Length; i++)
        {
            Assert.Equal(expectedOrders[i], scannedSeries[series][i].IssueOrder, precision: 2);
        }
    }

    #endregion
}
