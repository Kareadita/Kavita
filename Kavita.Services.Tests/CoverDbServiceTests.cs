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
        var imageService = new ImageService(Substitute.For<ILogger<ImageService>>(), directoryService);

        var coverDbService = new CoverDbService(Substitute.For<ILogger<CoverDbService>>(), directoryService, CacheFactory,
            Substitute.For<IHostEnvironment>(), imageService, unitOfWork, Substitute.For<IEventHub>(),
            Substitute.For<IUrlValidationService>());

        return Task.FromResult<(IDirectoryService, ICoverDbService)>((directoryService, coverDbService));
    }


    /// <summary>
    /// NetVips writes through native paths, so these use the real file system. Directories resolve under the test's
    /// working directory (config/covers, config/temp, config/favicons)
    /// </summary>
    private static (DirectoryService, ICoverDbService) SetupOnDisk(IUnitOfWork unitOfWork)
    {
        var directoryService = new DirectoryService(Substitute.For<ILogger<DirectoryService>>(), new FileSystem());
        directoryService.ExistOrCreate(directoryService.CoverImageDirectory);
        directoryService.ExistOrCreate(directoryService.TempDirectory);
        directoryService.ExistOrCreate(directoryService.FaviconDirectory);

        var imageService = new ImageService(Substitute.For<ILogger<ImageService>>(), directoryService);
        var coverDbService = new CoverDbService(Substitute.For<ILogger<CoverDbService>>(), directoryService, CacheFactory,
            Substitute.For<IHostEnvironment>(), imageService, unitOfWork, Substitute.For<IEventHub>(),
            Substitute.For<IUrlValidationService>());

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
    public async Task SetSeriesCoverByUrl_Url_WritesAtCoverImageSize()
    {
        var (unitOfWork, context, _) = await CreateDatabase();
        var (directoryService, coverDbService) = SetupOnDisk(unitOfWork);

        var series = await AddSeries(context, 91001);
        using var httpTest = new HttpTest();
        httpTest.RespondWith(() => new ByteArrayContent(PngBytes(3000, 4300)));

        await coverDbService.SetSeriesCoverByUrl(series, "https://example.com/cover.png", fromBase64: false);

        var path = Path.Join(directoryService.CoverImageDirectory, series.CoverImage);
        try
        {
            var (width, height) = CoverImageSize.Default.GetDimensions();
            using var cover = Image.NewFromFile(path);
            Assert.True(cover.Width <= width && cover.Height <= height, $"Cover is {cover.Width}x{cover.Height}");
        }
        finally
        {
            DeleteQuietly(path);
        }
    }

    [Fact]
    public async Task SetSeriesCoverByUrl_KPlusArtBeatsScanCover()
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
        series.CoverImage = "v91002_c91002.webp";
        using var httpTest = new HttpTest();
        httpTest.RespondWith(() => new ByteArrayContent(PngBytes(3000, 4300)));

        try
        {
            await coverDbService.SetSeriesCoverByUrl(series, "https://example.com/cover.png", fromBase64: false,
                chooseBetterImage: true);

            Assert.Equal("series91002.webp", series.CoverImage);
        }
        finally
        {
            DeleteQuietly(scanCover);
            DeleteQuietly(Path.Join(directoryService.CoverImageDirectory, "series91002.webp"));
        }
    }

    #endregion

    #region Download Favicon

    [Fact]
    public async Task DownloadFavicon_Jpeg_SavedAt32()
    {
        var (unitOfWork, _, _) = await CreateDatabase();
        var (directoryService, coverDbService) = SetupOnDisk(unitOfWork);
        var provider = Substitute.For<IEasyCachingProvider>();
        provider.GetAsync<string>(Arg.Any<string>(), Arg.Any<CancellationToken>()).Returns(new CacheValue<string>(null, false));
        CacheFactory.GetCachingProvider(EasyCacheProfiles.Favicon).Returns(provider);

        using var icon = Image.Black(64, 64, bands: 3);
        var jpeg = icon.JpegsaveBuffer();
        using var httpTest = new HttpTest();
        httpTest.RespondWith("<html><head><link rel=\"icon\" href=\"/icon-32.png\"></head></html>");
        httpTest.RespondWith(() => new ByteArrayContent(jpeg));

        var fileName = await coverDbService.DownloadFaviconAsync("https://favicon-jpeg.example.com/page", EncodeFormat.WEBP);

        var path = Path.Join(directoryService.FaviconDirectory, fileName);
        try
        {
            using var saved = Image.NewFromFile(path);
            Assert.Equal((32, 32), (saved.Width, saved.Height));
        }
        finally
        {
            DeleteQuietly(path);
        }
    }

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
