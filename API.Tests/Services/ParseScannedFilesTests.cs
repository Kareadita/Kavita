using System;
using System.Collections.Generic;
using System.IO.Abstractions.TestingHelpers;
using System.Linq;
using System.Threading.Tasks;
using API.Data.Metadata;
using API.Data.Repositories;
using API.Entities.Enums;
using API.Services;
using API.Services.Tasks.Scanner;
using API.Services.Tasks.Scanner.Parser;
using API.SignalR;
using Microsoft.Extensions.Logging;
using NSubstitute;
using Xunit;

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

    public string GetCoverImage(string fileFilePath, string fileName, MangaFormat format, EncodeFormat encodeFormat, CoverImageSize size = CoverImageSize.Default)
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

    public ParseScannedFilesTests()
    {
        // Since ProcessFile relies on _readingItemService, we can implement our own versions of _readingItemService so we have control over how the calls work

    }

    protected override async Task ResetDb()
    {
        _context.Series.RemoveRange(_context.Series.ToList());

        await _context.SaveChangesAsync();
    }

    #region MergeLocalizedSeriesWithSeries

    /// <summary>
    /// Test that a file encountered with a series matching a previously encountered localized series gets updated
    /// </summary>
    [Fact]
    public void MergeLocalizedSeriesWithSeries_ShouldUpdateSeriesAndLocalizedSeries()
    {
        var fileSystem = new MockFileSystem();
        var ds = new DirectoryService(Substitute.For<ILogger<DirectoryService>>(), fileSystem);
        var psf = new ParseScannedFiles(Substitute.For<ILogger<ParseScannedFiles>>(), ds,
            new MockReadingItemService(ds, Substitute.For<IBookService>()), Substitute.For<IEventHub>());

        // Series of vol 2 matches localized series of vol 1 - update so they stack
        var infos = new List<ParserInfo>(){
            new() { FullFilePath = "C:/Data/Accel World v01.cbz", Filename = "Accel World v01.cbz", Series = "Accel World", LocalizedSeries = "World of Acceleration" },
            new() { FullFilePath = "C:/Data/Accel World v02.cbz", Filename = "Accel World v02.cbz", Series = "World of Acceleration" }
        };
        psf.MergeLocalizedSeriesWithSeries(infos);
        Assert.Equal("Accel World", infos[1].Series);
        Assert.Equal("World of Acceleration", infos[1].LocalizedSeries);

        // Series of vol 2 matches localized series of vol 1 - update so they stack
        infos = [
            new() { FullFilePath = "C:/Data/Accel World v01.cbz", Filename = "Accel World v01.cbz", Series = "Accel World", LocalizedSeries = "World of Acceleration" },
            new() { FullFilePath = "C:/Data/Accel World v02.cbz", Filename = "Accel World v02.cbz", Series = "World of Acceleration", LocalizedSeries = "Accel World" }
        ];
        psf.MergeLocalizedSeriesWithSeries(infos);
        Assert.Equal("Accel World", infos[1].Series);
        Assert.Equal("World of Acceleration", infos[1].LocalizedSeries);

        // Multiple series subfolders within the same folder, make sure they don't stack
        infos = [
            new() { FullFilePath = "C:/Data/OSHIMI Shuzo/Avant-garde Yumeko/Avant-garde Yumeko v01.cbz", Filename = "Avant-garde Yumeko v01.cbz", Series = "Avant-garde Yumeko", LocalizedSeries = "アバンギャルド夢子" },
            new() { FullFilePath = "C:/Data/OSHIMI Shuzo/Blood on the Tracks/Blood on the Tracks v01.cbz", Filename = "Blood on the Tracks v01.cbz", Series = "Blood on the Tracks", LocalizedSeries = "血の轍" }
        ];
        psf.MergeLocalizedSeriesWithSeries(infos);
        Assert.Equal("Avant-garde Yumeko", infos[0].Series);
        Assert.Equal("アバンギャルド夢子", infos[0].LocalizedSeries);
        Assert.Equal("Blood on the Tracks", infos[1].Series);
        Assert.Equal("血の轍", infos[1].LocalizedSeries);

        // Multiple series subfolders within the same folder, make sure they don't stack
        infos = [
            new() { FullFilePath = "C:/Data/OSHIMI Shuzo/Avant-garde Yumeko/Avant-garde Yumeko v01.cbz", Filename = "Avant-garde Yumeko v01.cbz", Series = "Avant-garde Yumeko", LocalizedSeries = "Avant-garde Yumeko" },
            new() { FullFilePath = "C:/Data/OSHIMI Shuzo/Blood on the Tracks/Blood on the Tracks v01.cbz", Filename = "Blood on the Tracks v01.cbz", Series = "Blood on the Tracks" }
        ];
        psf.MergeLocalizedSeriesWithSeries(infos);
        Assert.Equal("Avant-garde Yumeko", infos[0].Series);
        Assert.Equal("Avant-garde Yumeko", infos[0].LocalizedSeries);
        Assert.Equal("Blood on the Tracks", infos[1].Series);
        Assert.Equal("", infos[1].LocalizedSeries);

        // Multiple series subfolders within the same folder, make sure only matching series stack
        infos = [
            new() { FullFilePath = "C:/Data/OSHIMI Shuzo/Avant-garde Yumeko/Avant-garde Yumeko v01.cbz", Filename = "Avant-garde Yumeko v01.cbz", Series = "Avant-garde Yumeko", LocalizedSeries = "アバンギャルド夢子" },
            new() { FullFilePath = "C:/Data/OSHIMI Shuzo/Avant-garde Yumeko/Avant-garde Yumeko v02.cbz", Filename = "Avant-garde Yumeko v02.cbz", Series = "アバンギャルド夢子" },
            new() { FullFilePath = "C:/Data/OSHIMI Shuzo/Avant-garde Yumeko/Avant-garde Yumeko v03.cbz", Filename = "Avant-garde Yumeko v03.cbz", Series = "Avant-garde Yumeko", LocalizedSeries = "アバンギャルド夢子" },
            new() { FullFilePath = "C:/Data/OSHIMI Shuzo/Blood on the Tracks/Blood on the Tracks v01.cbz", Filename = "Blood on the Tracks v01.cbz", Series = "Blood on the Tracks", LocalizedSeries = "血の轍" },
            new() { FullFilePath = "C:/Data/OSHIMI Shuzo/Blood on the Tracks/Blood on the Tracks v02.cbz", Filename = "Blood on the Tracks v02.cbz", Series = "Blood on the Tracks", LocalizedSeries = "血の轍" },
            new() { FullFilePath = "C:/Data/OSHIMI Shuzo/Blood on the Tracks/Blood on the Tracks v03.cbz", Filename = "Blood on the Tracks v03.cbz", Series = "血の轍" },
            new() { FullFilePath = "C:/Data/OSHIMI Shuzo/Welcome Back, Alice/Welcome Back Alice v01.cbz", Filename = "Welcome Back Alice v01.cbz", Series = "Welcome Back, Alice", LocalizedSeries = "おかえりアリス" },
            new() { FullFilePath = "C:/Data/OSHIMI Shuzo/Welcome Back, Alice/Welcome Back Alice v02.cbz", Filename = "Welcome Back Alice v02.cbz", Series = "Welcome Back, Alice" },
            new() { FullFilePath = "C:/Data/OSHIMI Shuzo/Welcome Back, Alice/Welcome Back Alice v03.cbz", Filename = "Welcome Back Alice v03.cbz", Series = "Welcome Back, Alice" }
        ];
        psf.MergeLocalizedSeriesWithSeries(infos);
        Assert.Equal("Avant-garde Yumeko", infos[0].Series);
        Assert.Equal("アバンギャルド夢子", infos[0].LocalizedSeries);
        Assert.Equal("Avant-garde Yumeko", infos[1].Series);
        Assert.Equal("アバンギャルド夢子", infos[1].LocalizedSeries);
        Assert.Equal("Avant-garde Yumeko", infos[2].Series);
        Assert.Equal("アバンギャルド夢子", infos[2].LocalizedSeries);
        Assert.Equal("Blood on the Tracks", infos[3].Series);
        Assert.Equal("血の轍", infos[3].LocalizedSeries);
        Assert.Equal("Blood on the Tracks", infos[4].Series);
        Assert.Equal("血の轍", infos[4].LocalizedSeries);
        Assert.Equal("Blood on the Tracks", infos[5].Series);
        Assert.Equal("血の轍", infos[5].LocalizedSeries);
        Assert.Equal("Welcome Back, Alice", infos[6].Series);
        Assert.Equal("おかえりアリス", infos[6].LocalizedSeries);
        Assert.Equal("Welcome Back, Alice", infos[7].Series);
        Assert.Equal("", infos[7].LocalizedSeries); // not updated, because series didn't match localized series of vol 1
        Assert.Equal("Welcome Back, Alice", infos[8].Series);
        Assert.Equal("", infos[8].LocalizedSeries); // not updated, beceasue series didn't match localized series of vol 1
    }

    #endregion

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

        // var parsedSeries = new Dictionary<ParsedSeries, IList<ParserInfo>>();
        //
        // Task TrackFiles(Tuple<bool, IList<ParserInfo>> parsedInfo)
        // {
        //     var skippedScan = parsedInfo.Item1;
        //     var parsedFiles = parsedInfo.Item2;
        //     if (parsedFiles.Count == 0) return Task.CompletedTask;
        //
        //     var foundParsedSeries = new ParsedSeries()
        //     {
        //         Name = parsedFiles.First().Series,
        //         NormalizedName = parsedFiles.First().Series.ToNormalized(),
        //         Format = parsedFiles.First().Format
        //     };
        //
        //     parsedSeries.Add(foundParsedSeries, parsedFiles);
        //     return Task.CompletedTask;
        // }

        var library =
            await _unitOfWork.LibraryRepository.GetLibraryForIdAsync(1,
                LibraryIncludes.Folders | LibraryIncludes.FileTypes);
        Assert.NotNull(library);

        library.Type = LibraryType.Manga;
        var parsedSeries = await psf.ScanLibrariesForSeries(library, new List<string>() { "C:/Data/" }, false,
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
        var scanResults = await psf.ProcessFiles("C:/Data/", true, await _unitOfWork.SeriesRepository.GetFolderPathMap(1), library);
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
        var scanResults = await psf.ProcessFiles("C:/Data/", false,
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
        var scanResults = await psf.ProcessFiles("C:/Data", true, await _unitOfWork.SeriesRepository.GetFolderPathMap(1), library);

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
        var scanResults = await psf.ProcessFiles("C:/Data", false,
            await _unitOfWork.SeriesRepository.GetFolderPathMap(1), library);

        Assert.Single(scanResults);
    }




    #endregion
}
