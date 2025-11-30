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
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using NSubstitute;
using Xunit.Abstractions;

namespace API.Tests.Helpers;
#nullable enable

public class ScannerHelper
{
    private readonly IUnitOfWork _unitOfWork;
    private readonly ITestOutputHelper _testOutputHelper;
    private readonly string _testDirectory = Path.Join(Directory.GetCurrentDirectory(), "../../../Services/Test Data/ScannerService/ScanTests");
    private readonly string _testcasesDirectory = Path.Join(Directory.GetCurrentDirectory(), "../../../Services/Test Data/ScannerService/TestCases");
    private readonly string _imagePath = Path.Join(Directory.GetCurrentDirectory(), "../../../Services/Test Data/ScannerService/1x1.png");
    private static readonly string[] ComicInfoExtensions = [".cbz", ".cbr", ".zip", ".rar"];
    private static readonly string[] EpubExtensions = [".epub"];

    public ScannerHelper(IUnitOfWork unitOfWork, ITestOutputHelper testOutputHelper)
    {
        _unitOfWork = unitOfWork;
        _testOutputHelper = testOutputHelper;
    }

    public async Task<Library> GenerateScannerData(string testcase, Dictionary<string, ComicInfo>? comicInfos = null)
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

    public ScannerService CreateServices(DirectoryService ds = null, IFileSystem fs = null)
    {
        fs ??= new FileSystem();
        ds ??= new DirectoryService(Substitute.For<ILogger<DirectoryService>>(), fs);
        var archiveService = new ArchiveService(Substitute.For<ILogger<ArchiveService>>(), ds,
            Substitute.For<IImageService>(), Substitute.For<IMediaErrorService>());
        var readingItemService = new ReadingItemService(archiveService, Substitute.For<IBookService>(),
            Substitute.For<IImageService>(), ds, Substitute.For<ILogger<ReadingItemService>>());



        var processSeries = new ProcessSeries(_unitOfWork, Substitute.For<ILogger<ProcessSeries>>(),
            Substitute.For<IEventHub>(),
            ds, Substitute.For<ICacheHelper>(), readingItemService, new FileService(fs),
            Substitute.For<IReadingListService>(),
            Substitute.For<IExternalMetadataService>());

        var serviceProvider = Substitute.For<IServiceProvider>();
        serviceProvider.GetService(typeof(IUnitOfWork)).Returns(_unitOfWork);
        serviceProvider.GetService(typeof(IProcessSeries)).Returns(processSeries);
        serviceProvider.GetService(typeof(IMetadataService)).Returns(Substitute.For<IMetadataService>());
        serviceProvider.GetService(typeof(IWordCountAnalyzerService))
            .Returns(Substitute.For<IWordCountAnalyzerService>());

        var scope = Substitute.For<IServiceScope>();
        scope.ServiceProvider.Returns(serviceProvider);

        var scopeFactory = Substitute.For<IServiceScopeFactory>();
        scopeFactory.CreateScope().Returns(scope);

        var scanner = new ScannerService(_unitOfWork, Substitute.For<ILogger<ScannerService>>(),
            Substitute.For<IMetadataService>(),
            Substitute.For<ICacheService>(), Substitute.For<IEventHub>(), ds,
            readingItemService, scopeFactory, Substitute.For<IWordCountAnalyzerService>());
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



    private async Task<string> GenerateTestDirectory(string mapPath, Dictionary<string, ComicInfo>? comicInfos = null)
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
        await Scaffold(testDirectory, filePaths ?? [], comicInfos);

        _testOutputHelper.WriteLine($"Test Directory Path: {testDirectory}");

        return Path.GetFullPath(testDirectory);
    }


    public async Task Scaffold(string testDirectory, List<string> filePaths, Dictionary<string, ComicInfo>? comicInfos = null)
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
            else if (EpubExtensions.Contains(ext) && comicInfos != null && comicInfos.TryGetValue(Path.GetFileName(relativePath), out var epubInfo))
            {
                CreateMinimalEpub(fullPath, epubInfo);
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

    private void CreateMinimalEpub(string filePath, ComicInfo? comicInfo = null)
    {
        using (var archive = ZipFile.Open(filePath, ZipArchiveMode.Create))
        {
            // EPUB requires a mimetype file as the first entry (uncompressed)
            var mimetypeEntry = archive.CreateEntry("mimetype", CompressionLevel.NoCompression);
            using (var mimetypeStream = mimetypeEntry.Open())
            using (var writer = new StreamWriter(mimetypeStream, Encoding.ASCII))
            {
                writer.Write("application/epub+zip");
            }

            // Create META-INF/container.xml
            var containerEntry = archive.CreateEntry("META-INF/container.xml");
            using (var containerStream = containerEntry.Open())
            using (var writer = new StreamWriter(containerStream, Encoding.UTF8))
            {
                writer.Write("""
                    <?xml version="1.0" encoding="UTF-8"?>
                    <container version="1.0" xmlns="urn:oasis:names:tc:opendocument:xmlns:container">
                        <rootfiles>
                            <rootfile full-path="OEBPS/content.opf" media-type="application/oebps-package+xml"/>
                        </rootfiles>
                    </container>
                    """);
            }

            // Create content.opf with metadata
            var contentOpf = GenerateContentOpf(comicInfo);
            var contentEntry = archive.CreateEntry("OEBPS/content.opf");
            using (var contentStream = contentEntry.Open())
            using (var writer = new StreamWriter(contentStream, Encoding.UTF8))
            {
                writer.Write(contentOpf);
            }

            // Add a minimal chapter XHTML file
            var chapterEntry = archive.CreateEntry("OEBPS/chapter1.xhtml");
            using (var chapterStream = chapterEntry.Open())
            using (var writer = new StreamWriter(chapterStream, Encoding.UTF8))
            {
                writer.Write("""
                    <?xml version="1.0" encoding="UTF-8"?>
                    <!DOCTYPE html>
                    <html xmlns="http://www.w3.org/1999/xhtml">
                    <head>
                        <title>Chapter 1</title>
                    </head>
                    <body>
                        <p>Test content.</p>
                    </body>
                    </html>
                    """);
            }

            // Add the cover image
            archive.CreateEntryFromFile(_imagePath, "OEBPS/cover.png");
        }
        Console.WriteLine($"Created minimal EPUB archive: {filePath} with{(comicInfo != null ? "" : "out")} metadata.");
    }

    private static string GenerateContentOpf(ComicInfo? comicInfo)
    {
        var sb = new StringBuilder();
        sb.AppendLine("""<?xml version="1.0" encoding="UTF-8"?>""");
        sb.AppendLine("""<package xmlns="http://www.idpf.org/2007/opf" version="3.0" unique-identifier="book-id">""");

        // Metadata section
        sb.AppendLine("    <metadata xmlns:dc=\"http://purl.org/dc/elements/1.1/\" xmlns:opf=\"http://www.idpf.org/2007/opf\" xmlns:calibre=\"http://calibre.kovidgoyal.net/2009/metadata\">");

        if (comicInfo != null)
        {
            if (!string.IsNullOrEmpty(comicInfo.Title))
                sb.AppendLine($"        <dc:title>{EscapeXml(comicInfo.Title)}</dc:title>");
            else
                sb.AppendLine("        <dc:title>Untitled</dc:title>");

            if (!string.IsNullOrEmpty(comicInfo.Series))
            {
                sb.AppendLine($"        <meta property=\"belongs-to-collection\" id=\"collection\">{EscapeXml(comicInfo.Series)}</meta>");
                sb.AppendLine("        <meta refines=\"#collection\" property=\"collection-type\">series</meta>");
            }

            if (!string.IsNullOrEmpty(comicInfo.Writer))
                sb.AppendLine($"        <dc:creator opf:role=\"aut\">{EscapeXml(comicInfo.Writer)}</dc:creator>");

            if (!string.IsNullOrEmpty(comicInfo.Publisher))
                sb.AppendLine($"        <dc:publisher>{EscapeXml(comicInfo.Publisher)}</dc:publisher>");

            if (!string.IsNullOrEmpty(comicInfo.Summary))
                sb.AppendLine($"        <dc:description>{EscapeXml(comicInfo.Summary)}</dc:description>");

            if (!string.IsNullOrEmpty(comicInfo.LanguageISO))
                sb.AppendLine($"        <dc:language>{EscapeXml(comicInfo.LanguageISO)}</dc:language>");
            else
                sb.AppendLine("        <dc:language>en</dc:language>");

            if (!string.IsNullOrEmpty(comicInfo.Isbn))
                sb.AppendLine($"        <dc:identifier id=\"book-id\" opf:scheme=\"ISBN\">{EscapeXml(comicInfo.Isbn)}</dc:identifier>");
            else
                sb.AppendLine($"        <dc:identifier id=\"book-id\">urn:uuid:{Guid.NewGuid()}</dc:identifier>");

            if (comicInfo.Year > 0)
            {
                var date = $"{comicInfo.Year:D4}";
                if (comicInfo.Month > 0)
                {
                    date += $"-{comicInfo.Month:D2}";
                    if (comicInfo.Day > 0)
                        date += $"-{comicInfo.Day:D2}";
                }
                sb.AppendLine($"        <dc:date>{date}</dc:date>");
            }

            if (!string.IsNullOrEmpty(comicInfo.TitleSort))
                sb.AppendLine($"        <meta name=\"calibre:title_sort\" content=\"{EscapeXml(comicInfo.TitleSort)}\"/>");

            if (!string.IsNullOrEmpty(comicInfo.SeriesSort))
                sb.AppendLine($"        <meta name=\"calibre:series_sort\" content=\"{EscapeXml(comicInfo.SeriesSort)}\"/>");

            if (!string.IsNullOrEmpty(comicInfo.Number))
                sb.AppendLine($"        <meta name=\"calibre:series_index\" content=\"{EscapeXml(comicInfo.Number)}\"/>");
        }
        else
        {
            sb.AppendLine("        <dc:title>Untitled</dc:title>");
            sb.AppendLine("        <dc:language>en</dc:language>");
            sb.AppendLine($"        <dc:identifier id=\"book-id\">urn:uuid:{Guid.NewGuid()}</dc:identifier>");
        }

        sb.AppendLine("    </metadata>");

        // Manifest section
        sb.AppendLine("    <manifest>");
        sb.AppendLine("        <item id=\"chapter1\" href=\"chapter1.xhtml\" media-type=\"application/xhtml+xml\"/>");
        sb.AppendLine("        <item id=\"cover\" href=\"cover.png\" media-type=\"image/png\" properties=\"cover-image\"/>");
        sb.AppendLine("    </manifest>");

        // Spine section
        sb.AppendLine("    <spine>");
        sb.AppendLine("        <itemref idref=\"chapter1\"/>");
        sb.AppendLine("    </spine>");

        sb.AppendLine("</package>");

        return sb.ToString();
    }

    private static string EscapeXml(string text)
    {
        if (string.IsNullOrEmpty(text)) return text;

        return text
            .Replace("&", "&amp;")
            .Replace("<", "&lt;")
            .Replace(">", "&gt;")
            .Replace("\"", "&quot;")
            .Replace("'", "&apos;");
    }

}
