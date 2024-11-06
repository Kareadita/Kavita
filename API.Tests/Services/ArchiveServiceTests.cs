using System.Diagnostics;
using System.IO;
using System.IO.Abstractions;
using System.IO.Abstractions.TestingHelpers;
using System.IO.Compression;
using System.Linq;
using API.Archive;
using API.Entities.Enums;
using API.Services;
using EasyCaching.Core;
using Microsoft.Extensions.Logging;
using NetVips;
using NSubstitute;
using NSubstitute.Extensions;
using Xunit;
using Xunit.Abstractions;

namespace API.Tests.Services;

public class ArchiveServiceTests
{
    private readonly ITestOutputHelper _testOutputHelper;
    private readonly ArchiveService _archiveService;
    private readonly ILogger<ArchiveService> _logger = Substitute.For<ILogger<ArchiveService>>();
    private readonly ILogger<DirectoryService> _directoryServiceLogger = Substitute.For<ILogger<DirectoryService>>();
    private readonly IDirectoryService _directoryService = new DirectoryService(Substitute.For<ILogger<DirectoryService>>(), new FileSystem());

    public ArchiveServiceTests(ITestOutputHelper testOutputHelper)
    {
        _testOutputHelper = testOutputHelper;
        _archiveService = new ArchiveService(_logger, _directoryService,
            new ImageService(Substitute.For<ILogger<ImageService>>(), _directoryService),
            Substitute.For<IMediaErrorService>());
    }

    [Theory]
    [InlineData("flat file.zip", false)]
    [InlineData("file in folder in folder.zip", true)]
    [InlineData("file in folder.zip", true)]
    [InlineData("file in folder_alt.zip", true)]
    public void ArchiveNeedsFlatteningTest(string archivePath, bool expected)
    {
        var testDirectory = Path.Join(Directory.GetCurrentDirectory(), "../../../Services/Test Data/ArchiveService/Archives");
        var file = Path.Join(testDirectory, archivePath);
        using var archive = ZipFile.OpenRead(file);
        Assert.Equal(expected, _archiveService.ArchiveNeedsFlattening(archive));
    }

    [Theory]
    [InlineData("non existent file.zip", false)]
    [InlineData("winrar.rar", true)]
    [InlineData("empty.zip", true)]
    [InlineData("flat file.zip", true)]
    [InlineData("file in folder in folder.zip", true)]
    [InlineData("file in folder.zip", true)]
    [InlineData("file in folder_alt.zip", true)]
    public void IsValidArchiveTest(string archivePath, bool expected)
    {
        var testDirectory = Path.Join(Directory.GetCurrentDirectory(), "../../../Services/Test Data/ArchiveService/Archives");
        Assert.Equal(expected, _archiveService.IsValidArchive(Path.Join(testDirectory, archivePath)));
    }

    [Theory]
    [InlineData("non existent file.zip", 0)]
    [InlineData("winrar.rar", 0)]
    [InlineData("empty.zip", 0)]
    [InlineData("flat file.zip", 1)]
    [InlineData("file in folder in folder.zip", 1)]
    [InlineData("file in folder.zip", 1)]
    [InlineData("file in folder_alt.zip", 1)]
    [InlineData("macos_none.zip", 0)]
    [InlineData("macos_one.zip", 1)]
    [InlineData("macos_native.zip", 21)]
    [InlineData("macos_withdotunder_one.zip", 1)]
    public void GetNumberOfPagesFromArchiveTest(string archivePath, int expected)
    {
        var testDirectory = Path.Join(Directory.GetCurrentDirectory(), "../../../Services/Test Data/ArchiveService/Archives");
        var sw = Stopwatch.StartNew();
        Assert.Equal(expected, _archiveService.GetNumberOfPagesFromArchive(Path.Join(testDirectory, archivePath)));
        _testOutputHelper.WriteLine($"Processed Original in {sw.ElapsedMilliseconds} ms");
    }



    [Theory]
    [InlineData("non existent file.zip", ArchiveLibrary.NotSupported)]
    [InlineData("winrar.rar", ArchiveLibrary.SharpCompress)]
    [InlineData("empty.zip", ArchiveLibrary.Default)]
    [InlineData("flat file.zip", ArchiveLibrary.Default)]
    [InlineData("file in folder in folder.zip", ArchiveLibrary.Default)]
    [InlineData("file in folder.zip", ArchiveLibrary.Default)]
    [InlineData("file in folder_alt.zip", ArchiveLibrary.Default)]
    public void CanOpenArchive(string archivePath, ArchiveLibrary expected)
    {
        var sw = Stopwatch.StartNew();
        var testDirectory = Path.Join(Directory.GetCurrentDirectory(), "../../../Services/Test Data/ArchiveService/Archives");

        Assert.Equal(expected, _archiveService.CanOpen(Path.Join(testDirectory, archivePath)));
        _testOutputHelper.WriteLine($"Processed Original in {sw.ElapsedMilliseconds} ms");
    }


    [Theory]
    [InlineData("non existent file.zip", 0)]
    [InlineData("winrar.rar", 0)]
    [InlineData("empty.zip", 0)]
    [InlineData("flat file.zip", 1)]
    [InlineData("file in folder in folder.zip", 1)]
    [InlineData("file in folder.zip", 1)]
    [InlineData("file in folder_alt.zip", 1)]
    public void CanExtractArchive(string archivePath, int expectedFileCount)
    {

        var testDirectory = Path.Join(Directory.GetCurrentDirectory(), "../../../Services/Test Data/ArchiveService/Archives");
        var extractDirectory = Path.Join(Directory.GetCurrentDirectory(), "../../../Services/Test Data/ArchiveService/Archives/Extraction");

        _directoryService.ClearAndDeleteDirectory(extractDirectory);

        var sw = Stopwatch.StartNew();
        _archiveService.ExtractArchive(Path.Join(testDirectory, archivePath), extractDirectory);
        var di1 = new DirectoryInfo(extractDirectory);
        Assert.Equal(expectedFileCount, di1.Exists ? _directoryService.GetFiles(extractDirectory, searchOption:SearchOption.AllDirectories).Count() : 0);
        _testOutputHelper.WriteLine($"Processed in {sw.ElapsedMilliseconds} ms");

        _directoryService.ClearAndDeleteDirectory(extractDirectory);
    }


    [Theory]
    [InlineData(new [] {"folder.jpg"}, "folder.jpg")]
    [InlineData(new [] {"vol1/"}, "")]
    [InlineData(new [] {"folder.jpg", "vol1/folder.jpg"}, "folder.jpg")]
    [InlineData(new [] {"cover.jpg", "vol1/folder.jpg"}, "cover.jpg")]
    [InlineData(new [] {"__MACOSX/cover.jpg", "vol1/page 01.jpg"}, "")]
    [InlineData(new [] {"Akame ga KILL! ZERO - c055 (v10) - p000 [Digital] [LuCaZ].jpg", "Akame ga KILL! ZERO - c055 (v10) - p000 [Digital] [LuCaZ].jpg", "Akame ga KILL! ZERO - c060 (v10) - p200 [Digital] [LuCaZ].jpg", "folder.jpg"}, "folder.jpg")]
    public void FindFolderEntry(string[] files, string expected)
    {
        var foundFile = ArchiveService.FindFolderEntry(files);
        Assert.Equal(expected, string.IsNullOrEmpty(foundFile) ? "" : foundFile);
    }

    [Theory]
    [InlineData(new [] {"folder.jpg"}, "folder.jpg")]
    [InlineData(new [] {"vol1/"}, "")]
    [InlineData(new [] {"folder.jpg", "vol1/folder.jpg"}, "folder.jpg")]
    [InlineData(new [] {"cover.jpg", "vol1/folder.jpg"}, "cover.jpg")]
    [InlineData(new [] {"page 2.jpg", "page 10.jpg"}, "page 2.jpg")]
    [InlineData(new [] {"__MACOSX/cover.jpg", "vol1/page 01.jpg"}, "vol1/page 01.jpg")]
    [InlineData(new [] {"Akame ga KILL! ZERO - c055 (v10) - p000 [Digital] [LuCaZ].jpg", "Akame ga KILL! ZERO - c055 (v10) - p000 [Digital] [LuCaZ].jpg", "Akame ga KILL! ZERO - c060 (v10) - p200 [Digital] [LuCaZ].jpg", "folder.jpg"}, "Akame ga KILL! ZERO - c055 (v10) - p000 [Digital] [LuCaZ].jpg")]
    [InlineData(new [] {"001.jpg", "001 - chapter 1/001.jpg"}, "001.jpg")]
    [InlineData(new [] {"chapter 1/001.jpg", "chapter 2/002.jpg", "somefile.jpg"}, "somefile.jpg")]
    public void FindFirstEntry(string[] files, string expected)
    {
        var foundFile = ArchiveService.FirstFileEntry(files, string.Empty);
        Assert.Equal(expected, string.IsNullOrEmpty(foundFile) ? "" : foundFile);
    }


    [Theory(Skip = "No specific reason")]
    //[InlineData("v10.cbz", "v10.expected.png")] // Commented out as these break usually when NetVips is updated
    //[InlineData("v10 - with folder.cbz", "v10 - with folder.expected.png")]
    //[InlineData("v10 - nested folder.cbz", "v10 - nested folder.expected.png")]
    [InlineData("macos_native.zip", "macos_native.png")]
    //[InlineData("v10 - duplicate covers.cbz", "v10 - duplicate covers.expected.png")]
    [InlineData("sorting.zip", "sorting.expected.png")]
    [InlineData("test.zip", "test.expected.jpg")]
    public void GetCoverImage_Default_Test(string inputFile, string expectedOutputFile)
    {
        var ds = Substitute.For<DirectoryService>(_directoryServiceLogger, new FileSystem());
        var imageService = new ImageService(Substitute.For<ILogger<ImageService>>(), ds);
        var archiveService =  Substitute.For<ArchiveService>(_logger, ds, imageService, Substitute.For<IMediaErrorService>());

        var testDirectory = Path.GetFullPath(Path.Join(Directory.GetCurrentDirectory(), "../../../Services/Test Data/ArchiveService/CoverImages"));
        var expectedBytes = Image.Thumbnail(Path.Join(testDirectory, expectedOutputFile), 320).WriteToBuffer(".png");

        archiveService.Configure().CanOpen(Path.Join(testDirectory, inputFile)).Returns(ArchiveLibrary.Default);

        var outputDir = Path.Join(testDirectory, "output");
        _directoryService.ClearDirectory(outputDir);
        _directoryService.ExistOrCreate(outputDir);

        var coverImagePath = archiveService.GetCoverImage(Path.Join(testDirectory, inputFile),
            Path.GetFileNameWithoutExtension(inputFile) + "_output", outputDir, EncodeFormat.PNG);
        var actual = File.ReadAllBytes(Path.Join(outputDir, coverImagePath));


        Assert.Equal(expectedBytes, actual);
        _directoryService.ClearAndDeleteDirectory(outputDir);
    }


    [Theory(Skip = "No specific reason")]
    //[InlineData("v10.cbz", "v10.expected.png")] // Commented out as these break usually when NetVips is updated
    //[InlineData("v10 - with folder.cbz", "v10 - with folder.expected.png")]
    //[InlineData("v10 - nested folder.cbz", "v10 - nested folder.expected.png")]
    [InlineData("macos_native.zip", "macos_native.png")]
    //[InlineData("v10 - duplicate covers.cbz", "v10 - duplicate covers.expected.png")]
    [InlineData("sorting.zip", "sorting.expected.png")]
    public void GetCoverImage_SharpCompress_Test(string inputFile, string expectedOutputFile)
    {
        var imageService = new ImageService(Substitute.For<ILogger<ImageService>>(), _directoryService);
        var archiveService =  Substitute.For<ArchiveService>(_logger,
            new DirectoryService(_directoryServiceLogger, new FileSystem()), imageService,
            Substitute.For<IMediaErrorService>());
        var testDirectory = API.Services.Tasks.Scanner.Parser.Parser.NormalizePath(Path.GetFullPath(Path.Join(Directory.GetCurrentDirectory(), "../../../Services/Test Data/ArchiveService/CoverImages")));

        var outputDir = Path.Join(testDirectory, "output");
        _directoryService.ClearDirectory(outputDir);
        _directoryService.ExistOrCreate(outputDir);

        archiveService.Configure().CanOpen(Path.Join(testDirectory, inputFile)).Returns(ArchiveLibrary.SharpCompress);
        var coverOutputFile = archiveService.GetCoverImage(Path.Join(testDirectory, inputFile),
            Path.GetFileNameWithoutExtension(inputFile), outputDir, EncodeFormat.PNG);
        var actualBytes = File.ReadAllBytes(Path.Join(outputDir, coverOutputFile));
        var expectedBytes = File.ReadAllBytes(Path.Join(testDirectory, expectedOutputFile));
        Assert.Equal(expectedBytes, actualBytes);

        _directoryService.ClearAndDeleteDirectory(outputDir);
    }

    [Theory]
    [InlineData("Archives/macos_native.zip")]
    [InlineData("Formats/One File with DB_Supported.zip")]
    public void CanParseCoverImage(string inputFile)
    {
        var imageService = Substitute.For<IImageService>();
        imageService.WriteCoverThumbnail(Arg.Any<Stream>(), Arg.Any<string>(), Arg.Any<string>(), Arg.Any<EncodeFormat>())
            .Returns(x => "cover.jpg");
        var archiveService = new ArchiveService(_logger, _directoryService, imageService, Substitute.For<IMediaErrorService>());
        var testDirectory = Path.Join(Directory.GetCurrentDirectory(), "../../../Services/Test Data/ArchiveService/");
        var inputPath = Path.GetFullPath(Path.Join(testDirectory, inputFile));
        var outputPath = Path.Join(testDirectory, Path.GetFileNameWithoutExtension(inputFile) + "_output");
        new DirectoryInfo(outputPath).Create();
        var expectedImage = archiveService.GetCoverImage(inputPath, inputFile, outputPath, EncodeFormat.PNG);
        Assert.Equal("cover.jpg", expectedImage);
        new DirectoryInfo(outputPath).Delete();
    }

    #region ShouldHaveComicInfo

    [Fact]
    public void ShouldHaveComicInfo()
    {
        var testDirectory = Path.Join(Directory.GetCurrentDirectory(), "../../../Services/Test Data/ArchiveService/ComicInfos");
        var archive = Path.Join(testDirectory, "ComicInfo.zip");
        const string summaryInfo = "By all counts, Ryouta Sakamoto is a loser when he's not holed up in his room, bombing things into oblivion in his favorite online action RPG. But his very own uneventful life is blown to pieces when he's abducted and taken to an uninhabited island, where he soon learns the hard way that he's being pitted against others just like him in a explosives-riddled death match! How could this be happening? Who's putting them up to this? And why!? The name, not to mention the objective, of this very real survival game is eerily familiar to Ryouta, who has mastered its virtual counterpart-BTOOOM! Can Ryouta still come out on top when he's playing for his life!?";

        var comicInfo = _archiveService.GetComicInfo(archive);
        Assert.NotNull(comicInfo);
        Assert.Equal(summaryInfo, comicInfo.Summary);
    }

    [Fact]
    public void ShouldHaveComicInfo_CanParseUmlaut()
    {
        var testDirectory = Path.Join(Directory.GetCurrentDirectory(), "../../../Services/Test Data/ArchiveService/ComicInfos");
        var archive = Path.Join(testDirectory, "Umlaut.zip");

        var comicInfo = _archiveService.GetComicInfo(archive);
        Assert.NotNull(comicInfo);
        Assert.Equal("Belladonna", comicInfo.Series);
    }

    [Fact]
    public void ShouldHaveComicInfo_WithAuthors()
    {
        var testDirectory = Path.Join(Directory.GetCurrentDirectory(), "../../../Services/Test Data/ArchiveService/ComicInfos");
        var archive = Path.Join(testDirectory, "ComicInfo_authors.zip");

        var comicInfo = _archiveService.GetComicInfo(archive);
        Assert.NotNull(comicInfo);
        Assert.Equal("Junya Inoue", comicInfo.Writer);
    }

    [Theory]
    [InlineData("ComicInfo_duplicateInfos.zip")]
    [InlineData("ComicInfo_duplicateInfos_reversed.zip")]
    [InlineData("ComicInfo_duplicateInfos.rar")]
    public void ShouldHaveComicInfo_TopLevelFileOnly(string filename)
    {
        var testDirectory = Path.Join(Directory.GetCurrentDirectory(), "../../../Services/Test Data/ArchiveService/ComicInfos");
        var archive = Path.Join(testDirectory, filename);

        var comicInfo = _archiveService.GetComicInfo(archive);
        Assert.NotNull(comicInfo);
        Assert.Equal("BTOOOM!", comicInfo.Series);
    }

    [Fact]
    public void ShouldHaveComicInfo_OutsideRoot()
    {
        var testDirectory = Path.Join(Directory.GetCurrentDirectory(), "../../../Services/Test Data/ArchiveService/ComicInfos");
        var archive = Path.Join(testDirectory, "ComicInfo_outside_root.zip");

        var comicInfo = _archiveService.GetComicInfo(archive);
        Assert.NotNull(comicInfo);
        Assert.Equal("BTOOOM! - Duplicate", comicInfo.Series);
    }

    [Fact]
    public void ShouldHaveComicInfo_OutsideRoot_SharpCompress()
    {
        var testDirectory = Path.Join(Directory.GetCurrentDirectory(), "../../../Services/Test Data/ArchiveService/ComicInfos");
        var archive = Path.Join(testDirectory, "ComicInfo_outside_root_SharpCompress.cb7");

        var comicInfo = _archiveService.GetComicInfo(archive);
        Assert.NotNull(comicInfo);
        Assert.Equal("Fire Punch", comicInfo.Series);
    }

    #endregion

    #region CanParseComicInfo

    [Fact]
    public void CanParseComicInfo()
    {
        var testDirectory = Path.Join(Directory.GetCurrentDirectory(), "../../../Services/Test Data/ArchiveService/ComicInfos");
        var archive = Path.Join(testDirectory, "ComicInfo.zip");
        var comicInfo = _archiveService.GetComicInfo(archive);

        Assert.NotNull(comicInfo);
        Assert.Equal("Yen Press", comicInfo.Publisher);
        Assert.Equal("Manga, Movies & TV", comicInfo.Genre);
        Assert.Equal("By all counts, Ryouta Sakamoto is a loser when he's not holed up in his room, bombing things into oblivion in his favorite online action RPG. But his very own uneventful life is blown to pieces when he's abducted and taken to an uninhabited island, where he soon learns the hard way that he's being pitted against others just like him in a explosives-riddled death match! How could this be happening? Who's putting them up to this? And why!? The name, not to mention the objective, of this very real survival game is eerily familiar to Ryouta, who has mastered its virtual counterpart-BTOOOM! Can Ryouta still come out on top when he's playing for his life!?",
            comicInfo.Summary);
        Assert.Equal(194, comicInfo.PageCount);
        Assert.Equal("en", comicInfo.LanguageISO);
        Assert.Equal("Scraped metadata from Comixology [CMXDB450184]", comicInfo.Notes);
        Assert.Equal("BTOOOM!", comicInfo.Series);
        Assert.Equal("v01", comicInfo.Title);
        Assert.Equal("https://www.comixology.com/BTOOOM/digital-comic/450184", comicInfo.Web);
    }

    #endregion

    #region CanParseComicInfo_DefaultNumberIsBlank

    [Fact]
    public void CanParseComicInfo_DefaultNumberIsBlank()
    {
        var testDirectory = Path.Join(Directory.GetCurrentDirectory(), "../../../Services/Test Data/ArchiveService/ComicInfos");
        var archive = Path.Join(testDirectory, "ComicInfo2.zip");
        var comicInfo = _archiveService.GetComicInfo(archive);

        Assert.NotNull(comicInfo);
        Assert.Equal("Hellboy", comicInfo.Series);
        Assert.Equal("The Right Hand of Doom", comicInfo.Title);
        Assert.Equal("", comicInfo.Number);
        Assert.Equal(0, comicInfo.Count);
        Assert.Equal("4", comicInfo.Volume);
    }


    #endregion

    #region FindCoverImageFilename

    [Theory]
    [InlineData(new string[] {}, "", null)]
    [InlineData(new [] {"001.jpg", "002.jpg"}, "Test.zip", "001.jpg")]
    [InlineData(new [] {"001.jpg", "!002.jpg"}, "Test.zip", "!002.jpg")]
    [InlineData(new [] {"001.jpg", "!001.jpg"}, "Test.zip", "!001.jpg")]
    [InlineData(new [] {"001.jpg", "cover.jpg"}, "Test.zip", "cover.jpg")]
    [InlineData(new [] {"001.jpg", "Chapter 20/cover.jpg", "Chapter 21/0001.jpg"}, "Test.zip", "Chapter 20/cover.jpg")]
    [InlineData(new [] {"._/001.jpg", "._/cover.jpg", "010.jpg"}, "Test.zip", "010.jpg")]
    [InlineData(new [] {"001.txt", "002.txt", "a.jpg"}, "Test.zip", "a.jpg")]
    public void FindCoverImageFilename(string[] filenames, string archiveName, string expected)
    {
        Assert.Equal(expected, ArchiveService.FindCoverImageFilename(archiveName, filenames));
    }


    #endregion

    #region CreateZipForDownload

    [Fact(Skip = "No specific reason")]
    public void CreateZipForDownloadTest()
    {
        var fileSystem = new MockFileSystem();
        var ds = new DirectoryService(Substitute.For<ILogger<DirectoryService>>(), fileSystem);
        //_archiveService.CreateZipForDownload(new []{}, outputDirectory)
    }

    #endregion
}
