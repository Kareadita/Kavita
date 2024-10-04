using System;
using System.IO;
using System.IO.Abstractions;
using API.Entities.Enums;
using Microsoft.Extensions.Logging.Abstractions;
using API.Services;
using API.Services.ImageServices;
using API.Services.ImageServices.ImageMagick;
using BenchmarkDotNet.Attributes;
using BenchmarkDotNet.Order;
using EasyCaching.Core;
using NSubstitute;


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
    private readonly IImageFactory _imageFactory;
    private const string SourceImage = "Data/comic-normal.jpg";
        

    public ArchiveServiceBenchmark()
    {
        _directoryService = new DirectoryService(null, new FileSystem());
        _imageService = new ImageService(null, _directoryService, Substitute.For<IEasyCachingProviderFactory>(), Substitute.For<IImageFactory>());
        _archiveService = new ArchiveService(new NullLogger<ArchiveService>(), _directoryService, _imageService, Substitute.For<IMediaErrorService>());
        _imageFactory = new ImageMagickImageFactory();
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
    public void ImageMagick_ExtractImage_PNG()
    {
        var outputDirectory = "Data/ImageMagick";
        _directoryService.ExistOrCreate(outputDirectory);

        using var stream = new FileStream(SourceImage, FileMode.Open);
        using var thumbnail2 = _imageFactory.Create(stream);
        int width = 320;
        int height = (int)(thumbnail2.Height * (width / (double)thumbnail2.Width));
        thumbnail2.Thumbnail(width, height);
        thumbnail2.Save(_directoryService.FileSystem.Path.Join(outputDirectory, "imagesharp.png"), EncodeFormat.PNG, 100);
    }

    [Benchmark]
    public void ImageMagick_ExtractImage_WebP()
    {
        var outputDirectory = "Data/ImageMagick";
        _directoryService.ExistOrCreate(outputDirectory);

        using var stream = new FileStream(SourceImage, FileMode.Open);
        using var thumbnail2 = _imageFactory.Create(stream);
        int width = 320;
        int height = (int)(thumbnail2.Height * (width / (double)thumbnail2.Width));
        thumbnail2.Thumbnail(width, height);
        thumbnail2.Save(_directoryService.FileSystem.Path.Join(outputDirectory, "imagesharp.webp"), EncodeFormat.PNG, 100);
    }


    // Benchmark to test default GetNumberOfPages from archive
    // vs a new method where I try to open the archive and return said stream
}
