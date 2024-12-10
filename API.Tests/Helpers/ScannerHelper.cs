using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Abstractions;
using System.IO.Compression;
using System.Linq;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;
using System.Xml;
using System.Xml.Serialization;
using API.Data;
using API.Data.Metadata;
using API.Entities;
using API.Entities.Enums;
using API.Helpers;
using API.Helpers.Builders;
using API.Services;
using API.Services.Plus;
using API.Services.Tasks;
using API.Services.Tasks.Metadata;
using API.Services.Tasks.Scanner;
using API.SignalR;
using Microsoft.Extensions.Logging;
using NSubstitute;
using Xunit.Abstractions;

namespace API.Tests.Helpers;

public class ScannerHelper
{
    private readonly IUnitOfWork _unitOfWork;
    private readonly ITestOutputHelper _testOutputHelper;
    private readonly string _testDirectory = Path.Join(Directory.GetCurrentDirectory(), "../../../Services/Test Data/ScannerService/ScanTests");
    private readonly string _testcasesDirectory = Path.Join(Directory.GetCurrentDirectory(), "../../../Services/Test Data/ScannerService/TestCases");
    private readonly string _imagePath = Path.Join(Directory.GetCurrentDirectory(), "../../../Services/Test Data/ScannerService/1x1.png");
    private static readonly string[] ComicInfoExtensions = new[] { ".cbz", ".cbr", ".zip", ".rar" };

    public ScannerHelper(IUnitOfWork unitOfWork, ITestOutputHelper testOutputHelper)
    {
        _unitOfWork = unitOfWork;
        _testOutputHelper = testOutputHelper;
    }

    public async Task<Library> GenerateScannerData(string testcase, Dictionary<string, ComicInfo> comicInfos = null)
    {
        var testDirectoryPath = await GenerateTestDirectory(Path.Join(_testcasesDirectory, testcase), comicInfos);

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

        return library;
    }

    public ScannerService CreateServices()
    {
        var fs = new FileSystem();
        var ds = new DirectoryService(Substitute.For<ILogger<DirectoryService>>(), fs);
        var archiveService = new ArchiveService(Substitute.For<ILogger<ArchiveService>>(), ds,
            Substitute.For<IImageService>(), Substitute.For<IMediaErrorService>());
        var readingItemService = new ReadingItemService(archiveService, Substitute.For<IBookService>(),
            Substitute.For<IImageService>(), ds, Substitute.For<ILogger<ReadingItemService>>());


        var processSeries = new ProcessSeries(_unitOfWork, Substitute.For<ILogger<ProcessSeries>>(),
            Substitute.For<IEventHub>(),
            ds, Substitute.For<ICacheHelper>(), readingItemService, new FileService(fs),
            Substitute.For<IMetadataService>(),
            Substitute.For<IWordCountAnalyzerService>(),
            Substitute.For<IReadingListService>(),
            Substitute.For<IExternalMetadataService>());

        var scanner = new ScannerService(_unitOfWork, Substitute.For<ILogger<ScannerService>>(),
            Substitute.For<IMetadataService>(),
            Substitute.For<ICacheService>(), Substitute.For<IEventHub>(), ds,
            readingItemService, processSeries, Substitute.For<IWordCountAnalyzerService>());
        return scanner;
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



    private async Task<string> GenerateTestDirectory(string mapPath, Dictionary<string, ComicInfo> comicInfos = null)
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
        await Scaffold(testDirectory, filePaths, comicInfos);

        _testOutputHelper.WriteLine($"Test Directory Path: {testDirectory}");

        return testDirectory;
    }


    public async Task Scaffold(string testDirectory, List<string> filePaths, Dictionary<string, ComicInfo> comicInfos = null)
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
            if (ComicInfoExtensions.Contains(ext) && comicInfos != null && comicInfos.TryGetValue(Path.GetFileName(relativePath), out var info))
            {
                CreateMinimalCbz(fullPath, info);
            }
            else
            {
                // Create an empty file
                await File.Create(fullPath).DisposeAsync();
                Console.WriteLine($"Created empty file: {fullPath}");
            }
        }
    }

    private void CreateMinimalCbz(string filePath, ComicInfo? comicInfo = null)
    {
        using (var archive = ZipFile.Open(filePath, ZipArchiveMode.Create))
        {
            // Add the 1x1 image to the archive
            archive.CreateEntryFromFile(_imagePath, "1x1.png");

            if (comicInfo != null)
            {
                // Serialize ComicInfo object to XML
                var comicInfoXml = SerializeComicInfoToXml(comicInfo);

                // Create an entry for ComicInfo.xml in the archive
                var entry = archive.CreateEntry("ComicInfo.xml");
                using var entryStream = entry.Open();
                using var writer = new StreamWriter(entryStream, Encoding.UTF8);

                // Write the XML to the archive
                writer.Write(comicInfoXml);
            }

        }
        Console.WriteLine($"Created minimal CBZ archive: {filePath} with{(comicInfo != null ? "" : "out")} metadata.");
    }


    private static string SerializeComicInfoToXml(ComicInfo comicInfo)
    {
        var xmlSerializer = new XmlSerializer(typeof(ComicInfo));
        using var stringWriter = new StringWriter();
        using (var xmlWriter = XmlWriter.Create(stringWriter, new XmlWriterSettings { Indent = true, Encoding = new UTF8Encoding(false), OmitXmlDeclaration = false}))
        {
            xmlSerializer.Serialize(xmlWriter, comicInfo);
        }

        // For the love of god, I spent 2 hours trying to get utf-8 with no BOM
        return stringWriter.ToString().Replace("""<?xml version="1.0" encoding="utf-16"?>""",
            @"<?xml version='1.0' encoding='utf-8'?>");
    }
}
