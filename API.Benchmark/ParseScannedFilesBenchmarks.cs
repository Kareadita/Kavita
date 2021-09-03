using System;
using System.IO;
using API.Entities.Enums;
using API.Interfaces.Services;
using API.Services;
using API.Services.Tasks.Scanner;
using BenchmarkDotNet.Attributes;
using BenchmarkDotNet.Order;
using Microsoft.Extensions.Logging;
using NSubstitute;

namespace API.Benchmark
{
    [MemoryDiagnoser]
    [Orderer(SummaryOrderPolicy.FastestToSlowest)]
    [RankColumn]
    [SimpleJob(launchCount: 1, warmupCount: 3, targetCount: 5, invocationCount: 100, id: "Test"), ShortRunJob]
    public class ParseScannedFilesBenchmarks
    {
        private readonly ParseScannedFiles _parseScannedFiles;
        private readonly ILogger<ParseScannedFiles> _logger = Substitute.For<ILogger<ParseScannedFiles>>();
        private readonly ILogger<BookService> _bookLogger = Substitute.For<ILogger<BookService>>();

        public ParseScannedFilesBenchmarks()
        {
            IBookService bookService = new BookService(_bookLogger);
            _parseScannedFiles = new ParseScannedFiles(bookService, _logger);
        }

        [Benchmark]
        public void Test()
        {
            var libraryPath = Path.Join(Directory.GetCurrentDirectory(),
                "../../../Services/Test Data/ScannerService/Manga");
            var parsedSeries = _parseScannedFiles.ScanLibrariesForSeries(LibraryType.Manga, new string[] {libraryPath},
                out var totalFiles, out var scanElapsedTime);
        }

    }
}
