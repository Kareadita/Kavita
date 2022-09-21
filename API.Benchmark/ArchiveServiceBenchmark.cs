using System;
using System.IO.Abstractions;
using Microsoft.Extensions.Logging.Abstractions;
using API.Services;
using BenchmarkDotNet.Attributes;
using BenchmarkDotNet.Order;

namespace API.Benchmark;

[StopOnFirstError]
[MemoryDiagnoser]
[RankColumn]
[Orderer(SummaryOrderPolicy.FastestToSlowest)]
[SimpleJob(launchCount: 1, warmupCount: 5, targetCount: 20)]
public class ArchiveServiceBenchmark
{
    private readonly ArchiveService _archiveService;
    private readonly IDirectoryService _directoryService;
    private readonly IImageService _imageService;

    public ArchiveServiceBenchmark()
    {
        _directoryService = new DirectoryService(null, new FileSystem());
        _imageService = new ImageService(null, _directoryService);
        _archiveService = new ArchiveService(new NullLogger<ArchiveService>(), _directoryService, _imageService);
    }

    [Benchmark(Baseline = true)]
    public void TestGetComicInfo_baseline()
    {
        if (_archiveService.GetComicInfo("Data/ComicInfo.zip") == null) {
            throw new Exception("ComicInfo not found");
        }
    }

    [Benchmark]
    public void TestGetComicInfo_duplicate()
    {
        if (_archiveService.GetComicInfo("Data/ComicInfo_duplicateInfos.zip") == null) {
            throw new Exception("ComicInfo not found");
        }
    }

    [Benchmark]
    public void TestGetComicInfo_outside_root()
    {
        if (_archiveService.GetComicInfo("Data/ComicInfo_outside_root.zip") == null) {
            throw new Exception("ComicInfo not found");
        }
    }

    // Benchmark to test default GetNumberOfPages from archive
    // vs a new method where I try to open the archive and return said stream
}
