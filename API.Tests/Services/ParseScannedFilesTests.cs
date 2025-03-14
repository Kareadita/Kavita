using System;
using System.Collections.Generic;
using System.Data.Common;
using System.IO;
using System.IO.Abstractions;
using System.IO.Abstractions.TestingHelpers;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using API.Data;
using API.Data.Metadata;
using API.Data.Repositories;
using API.Entities;
using API.Entities.Enums;
using API.Extensions;
using API.Helpers.Builders;
using API.Services;
using API.Services.Tasks.Scanner;
using API.Services.Tasks.Scanner.Parser;
using API.SignalR;
using API.Tests.Helpers;
using AutoMapper;
using Hangfire;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.Extensions.Logging;
using NSubstitute;
using Xunit;
using Xunit.Abstractions;

namespace API.Tests.Services;

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

    public ParserInfo Parse(string path, string rootPath, string libraryRoot, LibraryType type)
    {
        if (_comicVineParser.IsApplicable(path, type))
        {
            return _comicVineParser.Parse(path, rootPath, libraryRoot, type, GetComicInfo(path));
        }
        if (_imageParser.IsApplicable(path, type))
        {
            return _imageParser.Parse(path, rootPath, libraryRoot, type, GetComicInfo(path));
        }
        if (_bookParser.IsApplicable(path, type))
        {
            return _bookParser.Parse(path, rootPath, libraryRoot, type, GetComicInfo(path));
        }
        if (_pdfParser.IsApplicable(path, type))
        {
            return _pdfParser.Parse(path, rootPath, libraryRoot, type, GetComicInfo(path));
        }
        if (_basicParser.IsApplicable(path, type))
        {
            return _basicParser.Parse(path, rootPath, libraryRoot, type, GetComicInfo(path));
        }

        return null;
    }

    public ParserInfo ParseFile(string path, string rootPath, string libraryRoot, LibraryType type)
    {
        return Parse(path, rootPath, libraryRoot, type);
    }
}

public class ParseScannedFilesTests : AbstractDbTest
{
    private readonly ILogger<ParseScannedFiles> _logger = Substitute.For<ILogger<ParseScannedFiles>>();
    private readonly ScannerHelper _scannerHelper;

    public ParseScannedFilesTests(ITestOutputHelper testOutputHelper)
    {
        // Since ProcessFile relies on _readingItemService, we can implement our own versions of _readingItemService so we have control over how the calls work
        GlobalConfiguration.Configuration.UseInMemoryStorage();
        _scannerHelper = new ScannerHelper(_unitOfWork, testOutputHelper);
    }

    protected override async Task ResetDb()
    {
        _context.Series.RemoveRange(_context.Series.ToList());

        await _context.SaveChangesAsync();
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
    //         false, await _unitOfWork.SeriesRepository.GetFolderPathMap(1), TrackFiles);
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
        var fileSystem = new MockFileSystem();
        fileSystem.AddDirectory("C:/Data/");
        fileSystem.AddFile("C:/Data/Accel World v1.cbz", new MockFileData(string.Empty));
        fileSystem.AddFile("C:/Data/Accel World v2.cbz", new MockFileData(string.Empty));
        fileSystem.AddFile("C:/Data/Accel World v2.pdf", new MockFileData(string.Empty));
        fileSystem.AddFile("C:/Data/Nothing.pdf", new MockFileData(string.Empty));

        var ds = new DirectoryService(Substitute.For<ILogger<DirectoryService>>(), fileSystem);
        var psf = new ParseScannedFiles(Substitute.For<ILogger<ParseScannedFiles>>(), ds,
            new MockReadingItemService(ds, Substitute.For<IBookService>()), Substitute.For<IEventHub>());


        var library =
            await _unitOfWork.LibraryRepository.GetLibraryForIdAsync(1,
                LibraryIncludes.Folders | LibraryIncludes.FileTypes);
        Assert.NotNull(library);

        library.Type = LibraryType.Manga;
        var parsedSeries = await psf.ScanLibrariesForSeries(library, new List<string>() {"C:/Data/"}, false,
            await _unitOfWork.SeriesRepository.GetFolderPathMap(1));


        // Assert.Equal(3, parsedSeries.Values.Count);
        // Assert.NotEmpty(parsedSeries.Keys.Where(p => p.Format == MangaFormat.Archive && p.Name.Equals("Accel World")));

        Assert.Equal(3, parsedSeries.Count);
        Assert.NotEmpty(parsedSeries.Select(p => p.ParsedSeries).Where(p => p.Format == MangaFormat.Archive && p.Name.Equals("Accel World")));
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
        var fileSystem = CreateTestFilesystem();
        var ds = new DirectoryService(Substitute.For<ILogger<DirectoryService>>(), fileSystem);
        var psf = new ParseScannedFiles(Substitute.For<ILogger<ParseScannedFiles>>(), ds,
            new MockReadingItemService(ds, Substitute.For<IBookService>()), Substitute.For<IEventHub>());

        var directoriesSeen = new HashSet<string>();
        var library = await _unitOfWork.LibraryRepository.GetLibraryForIdAsync(1,
                LibraryIncludes.Folders | LibraryIncludes.FileTypes);
        var scanResults = await psf.ScanFiles("C:/Data/", true, await _unitOfWork.SeriesRepository.GetFolderPathMap(1), library);
        foreach (var scanResult in scanResults)
        {
            directoriesSeen.Add(scanResult.Folder);
        }

        Assert.Equal(2, directoriesSeen.Count);
    }

    [Fact]
    public async Task ProcessFiles_ForNonLibraryMode_CallsFolderActionOnce()
    {
        var fileSystem = CreateTestFilesystem();
        var ds = new DirectoryService(Substitute.For<ILogger<DirectoryService>>(), fileSystem);
        var psf = new ParseScannedFiles(Substitute.For<ILogger<ParseScannedFiles>>(), ds,
            new MockReadingItemService(ds, Substitute.For<IBookService>()), Substitute.For<IEventHub>());

        var library = await _unitOfWork.LibraryRepository.GetLibraryForIdAsync(1,
            LibraryIncludes.Folders | LibraryIncludes.FileTypes);
        Assert.NotNull(library);

        var directoriesSeen = new HashSet<string>();
        var scanResults = await psf.ScanFiles("C:/Data/", false,
            await _unitOfWork.SeriesRepository.GetFolderPathMap(1), library);

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

        var library = await _unitOfWork.LibraryRepository.GetLibraryForIdAsync(1,
            LibraryIncludes.Folders | LibraryIncludes.FileTypes);
        Assert.NotNull(library);
        var scanResults = await psf.ScanFiles("C:/Data", true, await _unitOfWork.SeriesRepository.GetFolderPathMap(1), library);

        Assert.Equal(2, scanResults.Count);
    }


    /// <summary>
    /// Due to this not being a library, it's going to consider everything under C:/Data as being one folder aka a series folder
    /// </summary>
    [Fact]
    public async Task ProcessFiles_ShouldCallFolderActionOnce()
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

        var ds = new DirectoryService(Substitute.For<ILogger<DirectoryService>>(), fileSystem);
        var psf = new ParseScannedFiles(Substitute.For<ILogger<ParseScannedFiles>>(), ds,
            new MockReadingItemService(ds, Substitute.For<IBookService>()), Substitute.For<IEventHub>());

        var library = await _unitOfWork.LibraryRepository.GetLibraryForIdAsync(1,
            LibraryIncludes.Folders | LibraryIncludes.FileTypes);
        Assert.NotNull(library);
        var scanResults = await psf.ScanFiles("C:/Data", false,
            await _unitOfWork.SeriesRepository.GetFolderPathMap(1), library);

        Assert.Single(scanResults);
    }




    #endregion

    [Fact]
    public async Task HasSeriesFolderNotChangedSinceLastScan_AllSeriesFoldersHaveChanges()
    {
        const string testcase = "Subfolders always scanning all series changes - Manga.json";
        var infos = new Dictionary<string, ComicInfo>();
        var library = await _scannerHelper.GenerateScannerData(testcase, infos);
        var testDirectoryPath = library.Folders.First().Path;

        _unitOfWork.LibraryRepository.Update(library);
        await _unitOfWork.CommitAsync();

        var fs = new FileSystem();
        var ds = new DirectoryService(Substitute.For<ILogger<DirectoryService>>(), fs);
        var psf = new ParseScannedFiles(Substitute.For<ILogger<ParseScannedFiles>>(), ds,
            new MockReadingItemService(ds, Substitute.For<IBookService>()), Substitute.For<IEventHub>());

        var scanner = _scannerHelper.CreateServices(ds, fs);
        await scanner.ScanLibrary(library.Id);

        var postLib = await _unitOfWork.LibraryRepository.GetLibraryForIdAsync(library.Id, LibraryIncludes.Series);
        Assert.NotNull(postLib);
        Assert.Equal(4, postLib.Series.Count);

        var spiceAndWolf = postLib.Series.First(x => x.Name == "Spice and Wolf");
        Assert.Equal(2, spiceAndWolf.Volumes.Count);

        var frieren = postLib.Series.First(x => x.Name == "Frieren - Beyond Journey's End");
        Assert.Single(frieren.Volumes);

        var executionerAndHerWayOfLife = postLib.Series.First(x => x.Name == "The Executioner and Her Way of Life");
        Assert.Equal(2, executionerAndHerWayOfLife.Volumes.Count);

        Thread.Sleep(1100); // Ensure at least one second has passed since library scan

        // Add a new chapter to a volume of the series, and scan. Validate that only, and all directories of this
        // series are marked as HasChanged
        var executionerCopyDir = Path.Join(Path.Join(testDirectoryPath, "The Executioner and Her Way of Life"),
               "The Executioner and Her Way of Life Vol. 1");
        File.Copy(Path.Join(executionerCopyDir, "The Executioner and Her Way of Life Vol. 1 Ch. 0001.cbz"),
            Path.Join(executionerCopyDir, "The Executioner and Her Way of Life Vol. 1 Ch. 0002.cbz"));

        // 4 series, of which 2 have volumes as directories
        var folderMap = await _unitOfWork.SeriesRepository.GetFolderPathMap(postLib.Id);
        Assert.Equal(6, folderMap.Count);

        var res = await psf.ScanFiles(testDirectoryPath, true, folderMap, postLib);
        var changes = res.Where(sc => sc.HasChanged).ToList();
        Assert.Equal(2, changes.Count);
        // Only volumes of The Executioner and Her Way of Life should be marked as HasChanged (Spice and Wolf also has 2 volumes dirs)
        Assert.Equal(2, changes.Count(sc => sc.Folder.Contains("The Executioner and Her Way of Life")));
    }

    [Fact]
    public async Task HasSeriesFolderNotChangedSinceLastScan_PublisherLayout()
    {
        const string testcase = "Subfolder always scanning fix publisher layout - Comic.json";
        var infos = new Dictionary<string, ComicInfo>();
        var library = await _scannerHelper.GenerateScannerData(testcase, infos);
        var testDirectoryPath = library.Folders.First().Path;

        _unitOfWork.LibraryRepository.Update(library);
        await _unitOfWork.CommitAsync();

        var fs = new FileSystem();
        var ds = new DirectoryService(Substitute.For<ILogger<DirectoryService>>(), fs);
        var psf = new ParseScannedFiles(Substitute.For<ILogger<ParseScannedFiles>>(), ds,
            new MockReadingItemService(ds, Substitute.For<IBookService>()), Substitute.For<IEventHub>());

        var scanner = _scannerHelper.CreateServices(ds, fs);
        await scanner.ScanLibrary(library.Id);

        var postLib = await _unitOfWork.LibraryRepository.GetLibraryForIdAsync(library.Id, LibraryIncludes.Series);
        Assert.NotNull(postLib);
        Assert.Equal(4, postLib.Series.Count);

        var spiceAndWolf = postLib.Series.First(x => x.Name == "Spice and Wolf");
        Assert.Equal(2, spiceAndWolf.Volumes.Count);

        var frieren = postLib.Series.First(x => x.Name == "Frieren - Beyond Journey's End");
        Assert.Equal(2, frieren.Volumes.Count);

        Thread.Sleep(1100); // Ensure at least one second has passed since library scan

        // Add a volume to a series, and scan. Ensure only this series is marked as HasChanged
        var executionerCopyDir = Path.Join(Path.Join(testDirectoryPath, "YenPress"), "The Executioner and Her Way of Life");
        File.Copy(Path.Join(executionerCopyDir, "The Executioner and Her Way of Life Vol. 1.cbz"),
            Path.Join(executionerCopyDir, "The Executioner and Her Way of Life Vol. 2.cbz"));

        var res = await psf.ScanFiles(testDirectoryPath, true,
            await _unitOfWork.SeriesRepository.GetFolderPathMap(postLib.Id), postLib);
        var changes = res.Count(sc => sc.HasChanged);
        Assert.Equal(1, changes);
    }

    [Fact]
    public async Task SubFoldersNoSubFolders_SkipAll()
    {
        const string testcase = "Subfolders and files at root - Manga.json";
        var infos = new Dictionary<string, ComicInfo>();
        var library = await _scannerHelper.GenerateScannerData(testcase, infos);
        var testDirectoryPath = library.Folders.First().Path;

        _unitOfWork.LibraryRepository.Update(library);
        await _unitOfWork.CommitAsync();

        var fs = new FileSystem();
        var ds = new DirectoryService(Substitute.For<ILogger<DirectoryService>>(), fs);
        var psf = new ParseScannedFiles(Substitute.For<ILogger<ParseScannedFiles>>(), ds,
            new MockReadingItemService(ds, Substitute.For<IBookService>()), Substitute.For<IEventHub>());

        var scanner = _scannerHelper.CreateServices(ds, fs);
        await scanner.ScanLibrary(library.Id);

        var postLib = await _unitOfWork.LibraryRepository.GetLibraryForIdAsync(library.Id, LibraryIncludes.Series);
        Assert.NotNull(postLib);
        Assert.Single(postLib.Series);

        var spiceAndWolf = postLib.Series.First(x => x.Name == "Spice and Wolf");
        Assert.Equal(3, spiceAndWolf.Volumes.Count);
        Assert.Equal(4, spiceAndWolf.Volumes.Sum(v => v.Chapters.Count));

        // Needs to be actual time as the write time is now, so if we set LastFolderChecked in the past
        // it'll always a scan as it was changed since the last scan.
        Thread.Sleep(1100); // Ensure at least one second has passed since library scan

        var res = await psf.ScanFiles(testDirectoryPath, true,
            await _unitOfWork.SeriesRepository.GetFolderPathMap(postLib.Id), postLib);
        Assert.DoesNotContain(res, sc => sc.HasChanged);
    }

    [Fact]
    public async Task SubFoldersNoSubFolders_ScanAllAfterAddInRoot()
    {
        const string testcase = "Subfolders and files at root - Manga.json";
        var infos = new Dictionary<string, ComicInfo>();
        var library = await _scannerHelper.GenerateScannerData(testcase, infos);
        var testDirectoryPath = library.Folders.First().Path;

        _unitOfWork.LibraryRepository.Update(library);
        await _unitOfWork.CommitAsync();

        var fs = new FileSystem();
        var ds = new DirectoryService(Substitute.For<ILogger<DirectoryService>>(), fs);
        var psf = new ParseScannedFiles(Substitute.For<ILogger<ParseScannedFiles>>(), ds,
            new MockReadingItemService(ds, Substitute.For<IBookService>()), Substitute.For<IEventHub>());

        var scanner = _scannerHelper.CreateServices(ds, fs);
        await scanner.ScanLibrary(library.Id);

        var postLib = await _unitOfWork.LibraryRepository.GetLibraryForIdAsync(library.Id, LibraryIncludes.Series);
        Assert.NotNull(postLib);
        Assert.Single(postLib.Series);

        var spiceAndWolf = postLib.Series.First(x => x.Name == "Spice and Wolf");
        Assert.Equal(3, spiceAndWolf.Volumes.Count);
        Assert.Equal(4, spiceAndWolf.Volumes.Sum(v => v.Chapters.Count));

        spiceAndWolf.LastFolderScanned = DateTime.Now.Subtract(TimeSpan.FromMinutes(2));
        _context.Series.Update(spiceAndWolf);
        await _context.SaveChangesAsync();

        // Add file at series root
        var spiceAndWolfDir = Path.Join(testDirectoryPath, "Spice and Wolf");
        File.Copy(Path.Join(spiceAndWolfDir, "Spice and Wolf Vol. 1.cbz"),
            Path.Join(spiceAndWolfDir, "Spice and Wolf Vol. 4.cbz"));

        var res = await psf.ScanFiles(testDirectoryPath, true,
            await _unitOfWork.SeriesRepository.GetFolderPathMap(postLib.Id), postLib);
        var changes = res.Count(sc => sc.HasChanged);
        Assert.Equal(2, changes);
    }

    [Fact]
    public async Task SubFoldersNoSubFolders_ScanAllAfterAddInSubFolder()
    {
        const string testcase = "Subfolders and files at root - Manga.json";
        var infos = new Dictionary<string, ComicInfo>();
        var library = await _scannerHelper.GenerateScannerData(testcase, infos);
        var testDirectoryPath = library.Folders.First().Path;

        _unitOfWork.LibraryRepository.Update(library);
        await _unitOfWork.CommitAsync();

        var fs = new FileSystem();
        var ds = new DirectoryService(Substitute.For<ILogger<DirectoryService>>(), fs);
        var psf = new ParseScannedFiles(Substitute.For<ILogger<ParseScannedFiles>>(), ds,
            new MockReadingItemService(ds, Substitute.For<IBookService>()), Substitute.For<IEventHub>());

        var scanner = _scannerHelper.CreateServices(ds, fs);
        await scanner.ScanLibrary(library.Id);

        var postLib = await _unitOfWork.LibraryRepository.GetLibraryForIdAsync(library.Id, LibraryIncludes.Series);
        Assert.NotNull(postLib);
        Assert.Single(postLib.Series);

        var spiceAndWolf = postLib.Series.First(x => x.Name == "Spice and Wolf");
        Assert.Equal(3, spiceAndWolf.Volumes.Count);
        Assert.Equal(4, spiceAndWolf.Volumes.Sum(v => v.Chapters.Count));

        spiceAndWolf.LastFolderScanned = DateTime.Now.Subtract(TimeSpan.FromMinutes(2));
        _context.Series.Update(spiceAndWolf);
        await _context.SaveChangesAsync();

        // Add file in subfolder
        var spiceAndWolfDir = Path.Join(Path.Join(testDirectoryPath, "Spice and Wolf"), "Spice and Wolf Vol. 3");
        File.Copy(Path.Join(spiceAndWolfDir, "Spice and Wolf Vol. 3 Ch. 0011.cbz"),
            Path.Join(spiceAndWolfDir, "Spice and Wolf Vol. 3 Ch. 0013.cbz"));

        var res = await psf.ScanFiles(testDirectoryPath, true,
            await _unitOfWork.SeriesRepository.GetFolderPathMap(postLib.Id), postLib);
        var changes = res.Count(sc => sc.HasChanged);
        Assert.Equal(2, changes);
    }
}
