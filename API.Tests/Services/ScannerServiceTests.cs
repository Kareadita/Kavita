using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Abstractions;
using System.IO.Compression;
using System.Linq;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;
using API.Data;
using API.Data.Repositories;
using API.Entities;
using API.Entities.Enums;
using API.Helpers;
using API.Helpers.Builders;
using API.Services;
using API.Services.Plus;
using API.Services.Tasks;
using API.Services.Tasks.Metadata;
using API.Services.Tasks.Scanner;
using API.Services.Tasks.Scanner.Parser;
using API.SignalR;
using API.Tests.Helpers;
using Hangfire;
using Microsoft.Extensions.Logging;
using NSubstitute;
using Xunit;
using Xunit.Abstractions;

namespace API.Tests.Services;

public class ScannerServiceTests : AbstractDbTest
{
    private readonly ITestOutputHelper _testOutputHelper;
    private readonly string _testDirectory = Path.Join(Directory.GetCurrentDirectory(), "../../../Services/Test Data/ScannerService/ScanTests");
    private readonly string _testcasesDirectory = Path.Join(Directory.GetCurrentDirectory(), "../../../Services/Test Data/ScannerService/TestCases");
    private readonly string _imagePath = Path.Join(Directory.GetCurrentDirectory(), "../../../Services/Test Data/ScannerService/1x1.png");

    public ScannerServiceTests(ITestOutputHelper testOutputHelper)
    {
        _testOutputHelper = testOutputHelper;

        // Set up Hangfire to use in-memory storage for testing
        GlobalConfiguration.Configuration.UseInMemoryStorage();
    }

    protected override async Task ResetDb()
    {
        _context.Library.RemoveRange(_context.Library);
        await _context.SaveChangesAsync();
    }

    [Fact]
    public void FindSeriesNotOnDisk_Should_Remove1()
    {
        var infos = new Dictionary<ParsedSeries, IList<ParserInfo>>();

        ParserInfoFactory.AddToParsedInfo(infos, new ParserInfo() {Series = "Darker than Black", Volumes = "1", Format = MangaFormat.Archive});
        //AddToParsedInfo(infos, new ParserInfo() {Series = "Darker than Black", Volumes = "1", Format = MangaFormat.Epub});

        var existingSeries = new List<Series>
        {
            new SeriesBuilder("Darker Than Black")
                .WithFormat(MangaFormat.Epub)

                .WithVolume(new VolumeBuilder("1")
                .WithName("1")
                .Build())
                .WithLocalizedName("Darker Than Black")
                .Build()
        };

        Assert.Single(ScannerService.FindSeriesNotOnDisk(existingSeries, infos));
    }

    [Fact]
    public void FindSeriesNotOnDisk_Should_RemoveNothing_Test()
    {
        var infos = new Dictionary<ParsedSeries, IList<ParserInfo>>();

        ParserInfoFactory.AddToParsedInfo(infos, new ParserInfo() {Series = "Darker than Black", Format = MangaFormat.Archive});
        ParserInfoFactory.AddToParsedInfo(infos, new ParserInfo() {Series = "Cage of Eden", Volumes = "1", Format = MangaFormat.Archive});
        ParserInfoFactory.AddToParsedInfo(infos, new ParserInfo() {Series = "Cage of Eden", Volumes = "10", Format = MangaFormat.Archive});

        var existingSeries = new List<Series>
        {
            new SeriesBuilder("Cage of Eden")
                .WithFormat(MangaFormat.Archive)

                .WithVolume(new VolumeBuilder("1")
                    .WithName("1")
                    .Build())
                .WithLocalizedName("Darker Than Black")
                .Build(),
            new SeriesBuilder("Darker Than Black")
                .WithFormat(MangaFormat.Archive)
                .WithVolume(new VolumeBuilder("1")
                    .WithName("1")
                    .Build())
                .WithLocalizedName("Darker Than Black")
                .Build(),
        };

        Assert.Empty(ScannerService.FindSeriesNotOnDisk(existingSeries, infos));
    }

    [Fact]
    public async Task ScanLibrary_ComicVine_PublisherFolder()
    {
        var testcase = "Publisher - ComicVine.json";
        var postLib = await GenerateScannerData(testcase);

        Assert.NotNull(postLib);
        Assert.Equal(4, postLib.Series.Count);

        Assert.True(true);
    }

    private async Task<Library> GenerateScannerData(string testcase)
    {
        var testDirectoryPath = await GenerateTestDirectory(Path.Join(_testcasesDirectory, testcase));
        _testOutputHelper.WriteLine($"Test Directory Path: {testDirectoryPath}");

        var (publisher, type) = SplitPublisherAndLibraryType(Path.GetFileNameWithoutExtension(testcase));

        var library = new LibraryBuilder(publisher, type)
            .WithFolders([new FolderPath() {Path = testDirectoryPath}])
            .Build();

        var admin = new AppUserBuilder("admin", "admin@kavita.com", Seed.DefaultThemes[0])
            .WithLibrary(library)
            .Build();

        _unitOfWork.UserRepository.Add(admin); // Admin is needed for generating collections/reading lists
        _unitOfWork.LibraryRepository.Add(library);
        await _unitOfWork.CommitAsync();

        var ds = new DirectoryService(Substitute.For<ILogger<DirectoryService>>(), new FileSystem());
        var mockReadingService = new MockReadingItemService(ds, Substitute.For<IBookService>());
        var processSeries = new ProcessSeries(_unitOfWork, Substitute.For<ILogger<ProcessSeries>>(),
            Substitute.For<IEventHub>(),
            ds, Substitute.For<ICacheHelper>(), mockReadingService, Substitute.For<IFileService>(),
            Substitute.For<IMetadataService>(),
            Substitute.For<IWordCountAnalyzerService>(), Substitute.For<ICollectionTagService>(),
            Substitute.For<IReadingListService>(),
            Substitute.For<IExternalMetadataService>(), new TagManagerService(_unitOfWork, Substitute.For<ILogger<TagManagerService>>()));

        var scanner = new ScannerService(_unitOfWork, Substitute.For<ILogger<ScannerService>>(),
            Substitute.For<IMetadataService>(),
            Substitute.For<ICacheService>(), Substitute.For<IEventHub>(), ds,
            mockReadingService, processSeries, Substitute.For<IWordCountAnalyzerService>());

        await scanner.ScanLibrary(library.Id);

        var postLib = await _unitOfWork.LibraryRepository.GetLibraryForIdAsync(library.Id, LibraryIncludes.Series);
        return postLib;
    }

    private static (string Publisher, LibraryType Type) SplitPublisherAndLibraryType(string input)
    {
        // Split the input string based on " - "
        var parts = input.Split(" - ", StringSplitOptions.RemoveEmptyEntries);

        if (parts.Length != 2)
        {
            throw new ArgumentException("Input must be in the format 'Publisher - LibraryType'");
        }

        var publisher = parts[0].Trim();
        var libraryTypeString = parts[1].Trim();

        // Try to parse the right-hand side as a LibraryType enum
        if (!Enum.TryParse<LibraryType>(libraryTypeString, out var libraryType))
        {
            throw new ArgumentException($"'{libraryTypeString}' is not a valid LibraryType");
        }

        return (publisher, libraryType);
    }



    private async Task<string> GenerateTestDirectory(string mapPath)
    {
        // Read the map file
        var mapContent = await File.ReadAllTextAsync(mapPath);

        // Deserialize the JSON content into a list of strings using System.Text.Json
        var filePaths = JsonSerializer.Deserialize<List<string>>(mapContent);

        // Create a test directory
        var testDirectory = Path.Combine(_testDirectory, Path.GetFileNameWithoutExtension(mapPath));
        if (Directory.Exists(testDirectory))
        {
            Directory.Delete(testDirectory, true);
        }
        Directory.CreateDirectory(testDirectory);

        // Generate the files and folders
        await Scaffold(testDirectory, filePaths);

        return testDirectory;
    }


    private async Task Scaffold(string testDirectory, List<string> filePaths)
    {
        foreach (var relativePath in filePaths)
        {
            var fullPath = Path.Combine(testDirectory, relativePath);
            var fileDir = Path.GetDirectoryName(fullPath);

            // Create the directory if it doesn't exist
            if (!Directory.Exists(fileDir))
            {
                Directory.CreateDirectory(fileDir);
                Console.WriteLine($"Created directory: {fileDir}");
            }

            var ext = Path.GetExtension(fullPath).ToLower();
            if (new[] { ".cbz", ".cbr", ".zip", ".rar" }.Contains(ext))
            {
                CreateMinimalCbz(fullPath, includeMetadata: true);
            }
            else
            {
                // Create an empty file
                await File.Create(fullPath).DisposeAsync();
                Console.WriteLine($"Created empty file: {fullPath}");
            }
        }
    }

    private void CreateMinimalCbz(string filePath, bool includeMetadata)
    {
        var tempImagePath = _imagePath; // Assuming _imagePath is a valid path to the 1x1 image

        using (var archive = ZipFile.Open(filePath, ZipArchiveMode.Create))
        {
            // Add the 1x1 image to the archive
            archive.CreateEntryFromFile(tempImagePath, "1x1.png");

            if (includeMetadata)
            {
                var comicInfo = GenerateComicInfo();
                var entry = archive.CreateEntry("ComicInfo.xml");
                using var entryStream = entry.Open();
                using var writer = new StreamWriter(entryStream, Encoding.UTF8);
                writer.Write(comicInfo);
            }
        }
        Console.WriteLine($"Created minimal CBZ archive: {filePath} with{(includeMetadata ? "" : "out")} metadata.");
    }

    private string GenerateComicInfo()
    {
        var comicInfo = new StringBuilder();
        comicInfo.AppendLine("<?xml version='1.0' encoding='utf-8'?>");
        comicInfo.AppendLine("<ComicInfo>");

        // People Tags
        string[] people = { /* Your list of people here */ };
        string[] genres = { /* Your list of genres here */ };

        void AddRandomTag(string tagName, string[] choices)
        {
            if (new Random().Next(0, 2) == 1) // 50% chance to include the tag
            {
                var selected = choices.OrderBy(x => Guid.NewGuid()).Take(new Random().Next(1, 5)).ToArray();
                comicInfo.AppendLine($"  <{tagName}>{string.Join(", ", selected)}</{tagName}>");
            }
        }

        foreach (var tag in new[] { "Writer", "Penciller", "Inker", "CoverArtist", "Publisher", "Character", "Imprint", "Colorist", "Letterer", "Editor", "Translator", "Team", "Location" })
        {
            AddRandomTag(tag, people);
        }

        AddRandomTag("Genre", genres);
        comicInfo.AppendLine("</ComicInfo>");

        return comicInfo.ToString();
    }
}
