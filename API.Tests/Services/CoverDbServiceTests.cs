using System.IO;
using System.Reflection;
using System.Threading.Tasks;
using API.Constants;
using API.Data;
using API.Entities.Enums;
using API.Extensions;
using API.Services;
using API.Services.Tasks.Metadata;
using API.SignalR;
using EasyCaching.Core;
using Kavita.Common;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using NSubstitute;
using Xunit;
using Xunit.Abstractions;

namespace API.Tests.Services;

public class CoverDbServiceTests(ITestOutputHelper outputHelper): AbstractDbTest(outputHelper)
{
    private static readonly IEasyCachingProviderFactory CacheFactory = Substitute.For<IEasyCachingProviderFactory>();

    private static readonly string FaviconPath = Path.Join(Directory.GetCurrentDirectory(),
        "../../../Services/Test Data/CoverDbService/Favicons");
    /// <summary>
    /// Path to download files temp to. Should be empty after each test.
    /// </summary>
    private static readonly string TempPath = Path.Join(Directory.GetCurrentDirectory(),
        "../../../Services/Test Data/CoverDbService/Temp");


    private static Task<(IDirectoryService, ICoverDbService)> Setup(IUnitOfWork unitOfWork)
    {

        var directoryService = new DirectoryService(Substitute.For<ILogger<DirectoryService>>(), CreateFileSystem());
        var imageService = new ImageService(Substitute.For<ILogger<ImageService>>(), directoryService);

        var coverDbService = new CoverDbService(Substitute.For<ILogger<CoverDbService>>(), directoryService, CacheFactory,
            Substitute.For<IHostEnvironment>(), imageService, unitOfWork, Substitute.For<IEventHub>());

        return Task.FromResult<(IDirectoryService, ICoverDbService)>((directoryService, coverDbService));
    }


    #region Download Favicon

    /// <summary>
    /// I cannot figure out how to test this code due to the reliance on the directoryService.FaviconDirectory and not being
    /// able to redirect it to the real filesystem.
    /// </summary>
    public async Task DownloadFaviconAsync_ShouldDownloadAndMatchExpectedFavicon()
    {
        var (unitOfWork, context, _) = await CreateDatabase();
        var (directoryService, coverDbService) = await Setup(unitOfWork);


        var testUrl = "https://anilist.co/anime/6205/Kmpfer/";
        var encodeFormat = EncodeFormat.WEBP;
        var expectedFaviconPath = Path.Combine(FaviconPath, "anilist.co.webp");

        // Ensure TempPath exists
        directoryService.ExistOrCreate(TempPath);

        var baseUrl = "https://anilist.co";

        // Ensure there is no cache result for this URL
        var provider = Substitute.For<IEasyCachingProvider>();
        provider.GetAsync<string>(baseUrl).Returns(new CacheValue<string>(null, false));
        CacheFactory.GetCachingProvider(EasyCacheProfiles.Favicon).Returns(provider);


        // // Replace favicon directory with TempPath
        // var directoryService = (DirectoryService)directoryService;
        // directoryService.FaviconDirectory = TempPath;

        // Hack: Swap FaviconDirectory with TempPath for ability to download real files
        typeof(DirectoryService)
            .GetField("FaviconDirectory", BindingFlags.NonPublic | BindingFlags.Instance)
            ?.SetValue(directoryService, TempPath);


        // Act
        var resultFilename = await coverDbService.DownloadFaviconAsync(testUrl, encodeFormat);
        var actualFaviconPath = Path.Combine(TempPath, resultFilename);

        // Assert file exists
        Assert.True(File.Exists(actualFaviconPath), "Downloaded favicon does not exist in temp path");

        // Load and compare similarity

        var similarity = expectedFaviconPath.CalculateSimilarity(actualFaviconPath); // Assuming you have this extension
        Assert.True(similarity > 0.9f, $"Image similarity too low: {similarity}");
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
