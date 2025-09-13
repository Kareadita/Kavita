using System;
using System.IO;
using System.IO.Abstractions;
using Microsoft.Extensions.Logging.Abstractions;
using API.Services;
using BenchmarkDotNet.Attributes;
using BenchmarkDotNet.Order;
using EasyCaching.Core;
using NSubstitute;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.Formats.Png;
using SixLabors.ImageSharp.Formats.Webp;
using SixLabors.ImageSharp.Processing;

namespace API.Benchmark;

[StopOnFirstError]
[MemoryDiagnoser]
[RankColumn]
[Orderer(SummaryOrderPolicy.FastestToSlowest)]
[SimpleJob(launchCount: 1, warmupCount: 5, invocationCount: 20)]
public class ArchiveServiceBenchmark
{
    private readonly ArchiveService _archiveService;
    private readonly IDirectoryService _directoryService;
    private readonly IImageService _imageService;
    private readonly PngEncoder _pngEncoder = new PngEncoder();
    private readonly WebpEncoder _webPEncoder = new WebpEncoder();
    private const string SourceImage = "C:/Users/josep/Pictures/obey_by_grrsa-d6llkaa_colored_by_me.png";


    public ArchiveServiceBenchmark()
    {
        _directoryService = new DirectoryService(null, new FileSystem());
        _imageService = new ImageService(null, _directoryService);
        _archiveService = new ArchiveService(new NullLogger<ArchiveService>(), _directoryService, _imageService, Substitute.For<IMediaErrorService>());
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

    [Benchmark]
    public void ImageSharp_ExtractImage_PNG()
    {
        var outputDirectory = "C:/Users/josep/Pictures/imagesharp/";
        _directoryService.ExistOrCreate(outputDirectory);

        using var stream = new FileStream(SourceImage, FileMode.Open);
        using var thumbnail2 = SixLabors.ImageSharp.Image.Load(stream);
        thumbnail2.Mutate(x => x.Resize(320, 0));
        thumbnail2.Save(_directoryService.FileSystem.Path.Join(outputDirectory, "imagesharp.png"), _pngEncoder);
    }

    [Benchmark]
    public void ImageSharp_ExtractImage_WebP()
    {
        var outputDirectory = "C:/Users/josep/Pictures/imagesharp/";
        _directoryService.ExistOrCreate(outputDirectory);

        using var stream = new FileStream(SourceImage, FileMode.Open);
        using var thumbnail2 = SixLabors.ImageSharp.Image.Load(stream);
        thumbnail2.Mutate(x => x.Resize(320, 0));
        thumbnail2.Save(_directoryService.FileSystem.Path.Join(outputDirectory, "imagesharp.webp"), _webPEncoder);
    }

    [Benchmark]
    public void NetVips_ExtractImage_PNG()
    {
        var outputDirectory = "C:/Users/josep/Pictures/netvips/";
        _directoryService.ExistOrCreate(outputDirectory);

        using var stream = new FileStream(SourceImage, FileMode.Open);
        using var thumbnail = NetVips.Image.ThumbnailStream(stream, 320);
        thumbnail.WriteToFile(_directoryService.FileSystem.Path.Join(outputDirectory, "netvips.png"));
    }

    [Benchmark]
    public void NetVips_ExtractImage_WebP()
    {
        var outputDirectory = "C:/Users/josep/Pictures/netvips/";
        _directoryService.ExistOrCreate(outputDirectory);

        using var stream = new FileStream(SourceImage, FileMode.Open);
        using var thumbnail = NetVips.Image.ThumbnailStream(stream, 320);
        thumbnail.WriteToFile(_directoryService.FileSystem.Path.Join(outputDirectory, "netvips.webp"));
    }

    // Benchmark to test default GetNumberOfPages from archive
    // vs a new method where I try to open the archive and return said stream
}
