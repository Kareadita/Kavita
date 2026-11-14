using System.IO.Abstractions;
using EasyCaching.Core;
using Flurl.Http.Testing;
using Kavita.API.Database;
using Kavita.API.Services;
using Kavita.API.Services.Metadata;
using Kavita.API.Services.SignalR;
using Kavita.Common;
using Kavita.Common.Extensions;
using Kavita.Database;
using Kavita.Database.Tests;
using Kavita.Models.Builders;
using Kavita.Models.Constants;
using Kavita.Models.Entities;
using Kavita.Models.Entities.Enums;
using Kavita.Models.Images;
using Kavita.Services.Images;
using Kavita.Services.Metadata;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using NSubstitute;
using Xunit.Abstractions;
using Image = NetVips.Image;

namespace Kavita.Services.Tests;

public class CoverDbServiceTests(ITestOutputHelper outputHelper): AbstractDbTest(outputHelper)
{
    private static readonly IEasyCachingProviderFactory CacheFactory = Substitute.For<IEasyCachingProviderFactory>();

    private static Task<(IDirectoryService, ICoverDbService)> Setup(IUnitOfWork unitOfWork)
    {

        var directoryService = new DirectoryService(Substitute.For<ILogger<DirectoryService>>(), CreateFileSystem());

        var coverDbService = new CoverDbService(Substitute.For<ILogger<CoverDbService>>(), directoryService, CacheFactory,
            Substitute.For<IHostEnvironment>(), unitOfWork, Substitute.For<IUrlValidationService>(),
            Substitute.For<IImageFileService>());

        return Task.FromResult<(IDirectoryService, ICoverDbService)>((directoryService, coverDbService));
    }


    /// <summary>
    /// NetVips writes through native paths, so these use the real file system. Directories resolve under the test's
    /// working directory (config/covers, config/temp)
    /// </summary>
    private static (DirectoryService, ICoverDbService) SetupOnDisk(IUnitOfWork unitOfWork)
    {
        var directoryService = new DirectoryService(Substitute.For<ILogger<DirectoryService>>(), new FileSystem());
        directoryService.ExistOrCreate(directoryService.CoverImageDirectory);
        directoryService.ExistOrCreate(directoryService.TempDirectory);

        var coverDbService = new CoverDbService(Substitute.For<ILogger<CoverDbService>>(), directoryService, CacheFactory,
            Substitute.For<IHostEnvironment>(), unitOfWork, Substitute.For<IUrlValidationService>(),
            new ImageFileService(unitOfWork, new ImagePipeline(Substitute.For<ILogger<ImagePipeline>>()),
                directoryService, Substitute.For<IEventHub>(), Substitute.For<ILogger<ImageFileService>>()));

        return (directoryService, coverDbService);
    }

    private static async Task<Series> AddSeries(DataContext context, int id)
    {
        var series = new SeriesBuilder("Cover Test " + id).WithLibraryId(1).Build();

        series.Id = id;
        context.Series.Add(series);
        await context.SaveChangesAsync();

        return series;
    }

    private static byte[] PngBytes(int width, int height)
    {
        using var image = Image.Black(width, height, bands: 3);
        return image.PngsaveBuffer();
    }

    private static void DeleteQuietly(string path)
    {
        try { File.Delete(path); } catch (IOException) { }
    }

    #region Set Cover By Url

    [Fact]
    public async Task SetCoverByUrl_KPlusArtBeatsScanCover()
    {
        var (unitOfWork, context, _) = await CreateDatabase();
        var (directoryService, coverDbService) = SetupOnDisk(unitOfWork);

        var series = await AddSeries(context, 91002);
        var (width, height) = CoverImageSize.Default.GetDimensions();
        var scanCover = Path.Join(directoryService.CoverImageDirectory, "v91002_c91002.webp");
        using (var scan = Image.Black(width, height, bands: 3))
        {
            scan.WriteToFile(scanCover);
        }
        series.CoverImageFile = new ImageFile
        {
            Kind = ImageKind.ChapterCover,
            FileName = "v91002_c91002.webp",
            Origin = ImageOrigin.Scan,
            Format = EncodeFormat.WEBP,
        };
        await context.SaveChangesAsync();
        using var httpTest = new HttpTest();
        httpTest.RespondWith(() => new ByteArrayContent(PngBytes(3000, 4300)));

        try
        {
            await coverDbService.SetCoverByUrlAsync(series, "https://example.com/cover.png",
                new CoverWrite(ImageOrigin.KavitaPlus, Lock: true, ChooseBetter: true));

            Assert.Equal(ImageKind.SeriesCover, series.CoverImageFile.Kind);
            Assert.Equal(ImageOrigin.KavitaPlus, series.CoverImageFile.Origin);
            Assert.True(File.Exists(scanCover));
        }
        finally
        {
            DeleteQuietly(scanCover);
            DeleteQuietly(Path.Join(directoryService.CoverImageDirectory, series.CoverImageFile!.FileName));
        }
    }

    #endregion

    #region Download Favicon

    [Fact]
    public async Task DownloadFaviconAsync_ShouldThrowKavitaException_WhenPreviouslyFailedUrlExistsInCache()
    {
        var (unitOfWork, context, _) = await CreateDatabase();
        var (directoryService, coverDbService) = await Setup(unitOfWork);


        var testUrl = "https://example.com";
        var encodeFormat = EncodeFormat.WEBP;

        var provider = Substitute.For<IEasyCachingProvider>();
        provider.GetAsync<string>(Arg.Any<string>())
            .Returns(new CacheValue<string>(string.Empty, true)); // Simulate previous failure

        CacheFactory.GetCachingProvider(EasyCacheProfiles.Favicon).Returns(provider);

        // Act & Assert
        await Assert.ThrowsAsync<KavitaException>(() =>
            coverDbService.DownloadFaviconAsync(testUrl, encodeFormat));
    }

    #endregion


}
