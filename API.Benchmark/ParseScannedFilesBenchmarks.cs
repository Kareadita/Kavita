using System.IO;
using System.IO.Abstractions;
using System.Threading.Tasks;
using API.Entities.Enums;
using API.Parser;
using API.Services;
using API.Services.Tasks.Scanner;
using API.SignalR;
using BenchmarkDotNet.Attributes;
using BenchmarkDotNet.Order;
using Microsoft.Extensions.Logging;
using NSubstitute;

namespace API.Benchmark
{
    [MemoryDiagnoser]
    [Orderer(SummaryOrderPolicy.FastestToSlowest)]
    [RankColumn]
    //[SimpleJob(launchCount: 1, warmupCount: 3, targetCount: 5, invocationCount: 100, id: "Test"), ShortRunJob]
    public class ParseScannedFilesBenchmarks
    {
        private readonly ParseScannedFiles _parseScannedFiles;
        private readonly ILogger<ParseScannedFiles> _logger = Substitute.For<ILogger<ParseScannedFiles>>();
        private readonly ILogger<BookService> _bookLogger = Substitute.For<ILogger<BookService>>();
        private readonly IArchiveService _archiveService = Substitute.For<ArchiveService>();

        public ParseScannedFilesBenchmarks()
        {
            var directoryService = new DirectoryService(Substitute.For<ILogger<DirectoryService>>(), new FileSystem());
            _parseScannedFiles = new ParseScannedFiles(
                Substitute.For<ILogger>(),
                directoryService,
                new ReadingItemService(_archiveService, new BookService(_bookLogger, directoryService, new ImageService(Substitute.For<ILogger<ImageService>>(), directoryService)), Substitute.For<ImageService>(), directoryService),
                Substitute.For<IEventHub>());
        }

        // [Benchmark]
        // public void Test()
        // {
        //     var libraryPath = Path.Join(Directory.GetCurrentDirectory(),
        //         "../../../Services/Test Data/ScannerService/Manga");
        //     var parsedSeries = _parseScannedFiles.ScanLibrariesForSeries(LibraryType.Manga, new string[] {libraryPath},
        //         out var totalFiles, out var scanElapsedTime);
        // }

        /// <summary>
        /// Generate a list of Series and another list with
        /// </summary>
        [Benchmark]
        public async Task MergeName()
        {
            var libraryPath = Path.Join(Directory.GetCurrentDirectory(),
                "../../../Services/Test Data/ScannerService/Manga");
            var p1 = new ParserInfo()
            {
                Chapters = "0",
                Edition = "",
                Format = MangaFormat.Archive,
                FullFilePath = Path.Join(libraryPath, "A Town Where You Live", "A_Town_Where_You_Live_v01.zip"),
                IsSpecial = false,
                Series = "A Town Where You Live",
                Title = "A Town Where You Live",
                Volumes = "1"
            };
            await _parseScannedFiles.ScanLibrariesForSeries(LibraryType.Manga, new [] {libraryPath}, "Manga");
            _parseScannedFiles.MergeName(p1);
        }
    }
}
