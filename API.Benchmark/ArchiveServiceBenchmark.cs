using System;
using System.IO.Abstractions;
using Microsoft.Extensions.Logging.Abstractions;
using API.Services;
using BenchmarkDotNet.Attributes;

namespace API.Benchmark;

[StopOnFirstError]
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

    [Benchmark]
    public void TestGetComicInfo()
    {
        if (_archiveService.GetComicInfo("Data/ComicInfo_duplicateInfos.zip") == null) {
            throw new Exception("ComicInfo not found");
        }
    }

    // Benchmark to test default GetNumberOfPages from archive
    // vs a new method where I try to open the archive and return said stream
}
