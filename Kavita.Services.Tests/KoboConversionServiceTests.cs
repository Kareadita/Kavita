using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using Kavita.API.Services;
using Kavita.Common;
using Kavita.Database.Tests;
using Kavita.Models.Entities.Enums;
using Kavita.Services.Builders;
using Kavita.Services.Kobo;
using Microsoft.Extensions.Logging;
using NSubstitute;
using Xunit.Abstractions;

namespace Kavita.Services.Tests;

public class KoboConversionServiceTests(ITestOutputHelper testOutputHelper) : AbstractDbTest(testOutputHelper)
{
    private readonly string _cbzPath = Path.GetFullPath(Path.Join(Directory.GetCurrentDirectory(),
        "../../../Test Data/ArchiveService/CoverImages/v10.cbz"));

    [Fact]
    public async Task GetOrConvert_OverBudget_EnqueuesAndThrowsUnavailable()
    {
        KoboConversionService.ResetInFlightForTests();
        var (unitOfWork, _, _) = await CreateDatabase();
        var cacheDir = Path.Join(Path.GetTempPath(), "kavita-kobo-cache-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(cacheDir);

        var converter = Substitute.For<IKoboArchiveEpubConverter>();
        converter.ConvertAsync(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns(ci => Task.Delay(Timeout.Infinite, ci.ArgAt<CancellationToken>(3)));

        var scheduler = Substitute.For<IKoboConversionJobScheduler>();
        var directoryService = Substitute.For<IDirectoryService>();
        directoryService.LongTermCacheDirectory.Returns(cacheDir);
        directoryService.ExistOrCreate(Arg.Any<string>()).Returns(true);

        var service = new KoboConversionService(
            Substitute.For<ILogger<KoboConversionService>>(),
            directoryService,
            converter,
            scheduler,
            unitOfWork);

        var file = new MangaFileBuilder(_cbzPath, MangaFormat.Archive, 10).WithExtension(".cbz").WithBytes(100)
            .Build();

        var ex = await Assert.ThrowsAsync<KavitaException>(() =>
            service.GetOrConvertEpubAsync(42, file, "Title", budgetSeconds: 1));
        Assert.Equal(KoboConversionService.ConvertUnavailableMessage, ex.Message);
        scheduler.Received(1).EnqueueBackgroundConvert(42);
    }

    [Fact]
    public async Task GetOrConvert_HardFailure_ThrowsFailed()
    {
        KoboConversionService.ResetInFlightForTests();
        var (unitOfWork, _, _) = await CreateDatabase();
        var cacheDir = Path.Join(Path.GetTempPath(), "kavita-kobo-cache-" + Guid.NewGuid().ToString("N"));
        var converter = Substitute.For<IKoboArchiveEpubConverter>();
        converter.ConvertAsync(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns<Task>(_ => throw new InvalidOperationException("corrupt archive"));

        var directoryService = Substitute.For<IDirectoryService>();
        directoryService.LongTermCacheDirectory.Returns(cacheDir);
        directoryService.ExistOrCreate(Arg.Any<string>()).Returns(true);

        var service = new KoboConversionService(
            Substitute.For<ILogger<KoboConversionService>>(),
            directoryService,
            converter,
            Substitute.For<IKoboConversionJobScheduler>(),
            unitOfWork);

        var file = new MangaFileBuilder(_cbzPath, MangaFormat.Archive, 10).WithExtension(".cbz").Build();
        var ex = await Assert.ThrowsAsync<KavitaException>(() =>
            service.GetOrConvertEpubAsync(7, file, "Title", budgetSeconds: 30));
        Assert.Equal(KoboConversionService.ConvertFailedMessage, ex.Message);
    }

    [Fact]
    public void ComputeFingerprint_ChangesWhenSourceMetadataChanges()
    {
        var file = new MangaFileBuilder(_cbzPath, MangaFormat.Archive, 10).WithBytes(10)
            .WithLastModified(DateTime.UtcNow).Build();
        var a = KoboConversionService.ComputeFingerprint(file);
        file.Bytes = 11;
        var b = KoboConversionService.ComputeFingerprint(file);
        Assert.NotEqual(a, b);
    }
}
