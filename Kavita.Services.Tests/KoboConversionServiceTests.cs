using System;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Kavita.API.Database;
using Kavita.API.Services;
using Kavita.API.Services.SignalR;
using Kavita.Common;
using Kavita.Database.Tests;
using Kavita.Models.Builders;
using Kavita.Models.DTOs.SignalR;
using Kavita.Models.Entities.Enums;
using Kavita.Services.Builders;
using Kavita.Services.Kobo;
using Kavita.Services.Scanner;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using NSubstitute;
using Xunit.Abstractions;

namespace Kavita.Services.Tests;

public class KoboConversionServiceTests(ITestOutputHelper testOutputHelper) : AbstractDbTest(testOutputHelper)
{
    private readonly string _cbzPath = Path.GetFullPath(Path.Join(Directory.GetCurrentDirectory(),
        "../../../Test Data/ArchiveService/CoverImages/v10.cbz"));
    private readonly string _epubPath = Path.Join(Directory.GetCurrentDirectory(), "Data", "AesopsFables.epub");

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

        var service = CreateService(unitOfWork, directoryService, converter, scheduler);

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

        var service = CreateService(unitOfWork, directoryService, converter);

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

    [Fact]
    public async Task ConvertLibraryForKobo_FillsSharedCache_WithoutBudget_AndReportsProgress()
    {
        KoboConversionService.ResetInFlightForTests();
        var (unitOfWork, context, _) = await CreateDatabase();
        var library = new LibraryBuilder("Warm Lib").WithAllowKoboSync(true).Build();
        context.Library.Add(library);
        await context.SaveChangesAsync();

        var cbzChapter = new ChapterBuilder("1")
            .WithPages(10)
            .WithFile(new MangaFileBuilder(_cbzPath, MangaFormat.Archive, 10).WithExtension(".cbz").Build())
            .Build();
        var epubChapter = new ChapterBuilder("1")
            .WithPages(10)
            .WithFile(new MangaFileBuilder(_epubPath, MangaFormat.Epub, 10).Build())
            .Build();
        var seriesCbz = new SeriesBuilder("Archive Series")
            .WithFormat(MangaFormat.Archive)
            .WithVolume(new VolumeBuilder(Parser.LooseLeafVolume).WithChapter(cbzChapter).Build())
            .Build();
        var seriesEpub = new SeriesBuilder("Epub Series")
            .WithFormat(MangaFormat.Epub)
            .WithVolume(new VolumeBuilder(Parser.LooseLeafVolume).WithChapter(epubChapter).Build())
            .Build();
        seriesCbz.Library = library;
        seriesEpub.Library = library;
        context.Series.AddRange(seriesCbz, seriesEpub);
        await context.SaveChangesAsync();

        var cacheDir = Path.Join(Path.GetTempPath(), "kavita-kobo-cache-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(cacheDir);

        var convertCalls = 0;
        var converter = Substitute.For<IKoboArchiveEpubConverter>();
        converter.ConvertAsync(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns(ci =>
            {
                convertCalls++;
                // Simulate work longer than a typical in-request budget to show library warm-up is unbound.
                Thread.Sleep(50);
                var output = ci.ArgAt<string>(1);
                Directory.CreateDirectory(Path.GetDirectoryName(output)!);
                File.WriteAllText(output, "epub-bytes");
                return Task.CompletedTask;
            });

        var directoryService = Substitute.For<IDirectoryService>();
        directoryService.LongTermCacheDirectory.Returns(cacheDir);
        directoryService.ExistOrCreate(Arg.Any<string>()).Returns(ci =>
        {
            Directory.CreateDirectory(ci.ArgAt<string>(0));
            return true;
        });

        var eventHub = Substitute.For<IEventHub>();
        var service = CreateService(unitOfWork, directoryService, converter, eventHub: eventHub);

        await service.ConvertLibraryForKoboAsync(library.Id);

        Assert.Equal(1, convertCalls); // EPUB-only chapter skipped
        var cached = Directory.GetFiles(Path.Combine(cacheDir, KoboConversionService.CacheFolderName), "*.epub",
            SearchOption.AllDirectories);
        Assert.Single(cached);

        await eventHub.Received().SendMessageAsync(MessageFactory.NotificationProgress,
            Arg.Is<SignalRMessage>(m =>
                m.Name == "KoboConvertProgress" && m.EventType == ProgressEventType.Started),
            Arg.Any<bool>(), Arg.Any<CancellationToken>());
        await eventHub.Received().SendMessageAsync(MessageFactory.NotificationProgress,
            Arg.Is<SignalRMessage>(m =>
                m.Name == "KoboConvertProgress" && m.EventType == ProgressEventType.Ended),
            Arg.Any<bool>(), Arg.Any<CancellationToken>());
        await eventHub.Received().SendMessageAsync(MessageFactory.NotificationProgress,
            Arg.Is<SignalRMessage>(m =>
                m.Name == "KoboConvertProgress" && m.EventType == ProgressEventType.Updated),
            Arg.Any<bool>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ClearConversionCache_EmptiesSharedCache()
    {
        KoboConversionService.ResetInFlightForTests();
        var (unitOfWork, _, _) = await CreateDatabase();
        var cacheDir = Path.Join(Path.GetTempPath(), "kavita-kobo-cache-" + Guid.NewGuid().ToString("N"));
        var koboRoot = Path.Combine(cacheDir, KoboConversionService.CacheFolderName, "99");
        Directory.CreateDirectory(koboRoot);
        var cachedFile = Path.Combine(koboRoot, "abc.epub");
        await File.WriteAllTextAsync(cachedFile, "cached");

        var directoryService = Substitute.For<IDirectoryService>();
        directoryService.LongTermCacheDirectory.Returns(cacheDir);
        directoryService.ExistOrCreate(Arg.Any<string>()).Returns(ci =>
        {
            Directory.CreateDirectory(ci.ArgAt<string>(0));
            return true;
        });
        directoryService.When(d => d.ClearDirectory(Arg.Any<string>()))
            .Do(ci =>
            {
                var path = ci.ArgAt<string>(0);
                if (!Directory.Exists(path)) return;
                foreach (var entry in Directory.EnumerateFileSystemEntries(path))
                {
                    if (Directory.Exists(entry)) Directory.Delete(entry, true);
                    else File.Delete(entry);
                }
            });

        var service = CreateService(unitOfWork, directoryService, Substitute.For<IKoboArchiveEpubConverter>());
        await service.ClearConversionCacheAsync();

        Assert.False(File.Exists(cachedFile));
        Assert.Empty(Directory.EnumerateFileSystemEntries(
            Path.Combine(cacheDir, KoboConversionService.CacheFolderName)));
    }

    [Fact]
    public async Task ConvertLibraryForKobo_ThenDownload_ServesFromCacheWithoutReconvert()
    {
        KoboConversionService.ResetInFlightForTests();
        var (unitOfWork, context, mapper) = await CreateDatabase();
        var library = new LibraryBuilder("Warm Then Download").WithAllowKoboSync(true).Build();
        context.Library.Add(library);

        var user = new AppUserBuilder("kobo-user", "kobo@test.com").WithLibrary(library).Build();
        context.AppUser.Add(user);
        await context.SaveChangesAsync();

        var chapter = new ChapterBuilder("1")
            .WithPages(10)
            .WithFile(new MangaFileBuilder(_cbzPath, MangaFormat.Archive, 10).WithExtension(".cbz").Build())
            .Build();
        var series = new SeriesBuilder("Warm Series")
            .WithFormat(MangaFormat.Archive)
            .WithVolume(new VolumeBuilder(Parser.LooseLeafVolume).WithChapter(chapter).Build())
            .Build();
        series.Library = library;
        context.Series.Add(series);
        await context.SaveChangesAsync();

        // Reload chapter id after save
        chapter = await context.Chapter.Include(c => c.Files).SingleAsync(c => c.Id == chapter.Id);

        var cacheDir = Path.Join(Path.GetTempPath(), "kavita-kobo-cache-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(cacheDir);

        var convertCalls = 0;
        var converter = Substitute.For<IKoboArchiveEpubConverter>();
        converter.ConvertAsync(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns(ci =>
            {
                convertCalls++;
                var output = ci.ArgAt<string>(1);
                Directory.CreateDirectory(Path.GetDirectoryName(output)!);
                File.WriteAllText(output, "epub-bytes");
                return Task.CompletedTask;
            });

        var directoryService = Substitute.For<IDirectoryService>();
        directoryService.LongTermCacheDirectory.Returns(cacheDir);
        directoryService.ExistOrCreate(Arg.Any<string>()).Returns(ci =>
        {
            Directory.CreateDirectory(ci.ArgAt<string>(0));
            return true;
        });

        var conversion = CreateService(unitOfWork, directoryService, converter);
        await conversion.ConvertLibraryForKoboAsync(library.Id);
        Assert.Equal(1, convertCalls);

        // Download after warm-up must hit cache (no second convert), even with a tiny budget.
        var download = await conversion.GetOrConvertEpubAsync(chapter.Id, chapter.Files.First(), "Title",
            budgetSeconds: 1);
        Assert.True(File.Exists(download));
        Assert.Equal(1, convertCalls);
    }

    [Fact]
    public void TryGetCachedKepubPath_ReturnsPath_WhenArtifactExists()
    {
        var cacheDir = Path.Join(Path.GetTempPath(), "kavita-kobo-kepub-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(cacheDir);
        try
        {
            var directoryService = Substitute.For<IDirectoryService>();
            directoryService.LongTermCacheDirectory.Returns(cacheDir);

            var service = CreateService(Substitute.For<IUnitOfWork>(), directoryService,
                Substitute.For<IKoboArchiveEpubConverter>());
            var file = new MangaFileBuilder(_epubPath, MangaFormat.Epub, 10).WithBytes(100).Build();
            var fingerprint = KoboConversionService.ComputeFingerprint(file);
            var expected = service.GetKepubCacheFilePath(99, fingerprint);
            Directory.CreateDirectory(Path.GetDirectoryName(expected)!);
            File.WriteAllText(expected, "kepub");

            Assert.Equal(expected, service.TryGetCachedKepubPath(99, file));
            Assert.Null(service.TryGetCachedKepubPath(100, file));
        }
        finally
        {
            if (Directory.Exists(cacheDir)) Directory.Delete(cacheDir, true);
        }
    }

    private static KoboConversionService CreateService(
        IUnitOfWork unitOfWork,
        IDirectoryService directoryService,
        IKoboArchiveEpubConverter converter,
        IKoboConversionJobScheduler? scheduler = null,
        IEventHub? eventHub = null)
    {
        return new KoboConversionService(
            Substitute.For<ILogger<KoboConversionService>>(),
            directoryService,
            converter,
            scheduler ?? Substitute.For<IKoboConversionJobScheduler>(),
            unitOfWork,
            eventHub ?? Substitute.For<IEventHub>());
    }
}
