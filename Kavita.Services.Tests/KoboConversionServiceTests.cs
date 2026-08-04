using System;
using System.IO;
using System.IO.Abstractions;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Kavita.API.Database;
using Kavita.API.Services;
using Kavita.API.Services.Scanner;
using Kavita.API.Services.SignalR;
using Kavita.Common;
using Kavita.Database.Tests;
using Kavita.Models.Builders;
using Kavita.Models.DTOs.SignalR;
using Kavita.Models.Entities.Enums;
using Kavita.Models.Entities.User;
using Kavita.Services.Builders;
using Kavita.Services.Kobo;
using Kavita.Services.Scanner;
using Kavita.Services.Tests.Kobo;
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
        await SetConversionCacheDirectory(unitOfWork, Path.Combine(cacheDir, KoboConversionService.CacheFolderName));
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
        await SetConversionCacheDirectory(unitOfWork, Path.Combine(cacheDir, KoboConversionService.CacheFolderName));
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
    public void ComputeFingerprint_IncludesConvertContractVersion()
    {
        Assert.Equal(1, KoboConversionService.ConvertContractVersion);

        var file = new MangaFileBuilder(_cbzPath, MangaFormat.Archive, 10).WithBytes(10)
            .WithLastModified(new DateTime(2026, 1, 2, 3, 4, 5, DateTimeKind.Utc)).Build();
        var raw =
            $"{KoboConversionService.ConvertContractVersion}|{file.FilePath}|{file.Bytes}|{file.LastModifiedUtc.Ticks}";
        var expected = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(raw))).ToLowerInvariant();
        Assert.Equal(expected, KoboConversionService.ComputeFingerprint(file));
    }

    [Fact]
    public async Task GetOrConvertEpub_MissesCache_WhenArtifactUsesPreVersionFingerprint()
    {
        KoboConversionService.ResetInFlightForTests();
        var (unitOfWork, _, _) = await CreateDatabase();
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
        await SetConversionCacheDirectory(unitOfWork, Path.Combine(cacheDir, KoboConversionService.CacheFolderName));
        directoryService.ExistOrCreate(Arg.Any<string>()).Returns(ci =>
        {
            Directory.CreateDirectory(ci.ArgAt<string>(0));
            return true;
        });

        var service = CreateService(unitOfWork, directoryService, converter);
        var file = new MangaFileBuilder(_cbzPath, MangaFormat.Archive, 10).WithExtension(".cbz").WithBytes(100)
            .WithLastModified(new DateTime(2026, 3, 1, 0, 0, 0, DateTimeKind.Utc)).Build();

        // Simulate a pre-ConvertContractVersion cache key (source identity only).
        var legacyRaw = $"{file.FilePath}|{file.Bytes}|{file.LastModifiedUtc.Ticks}";
        var legacyFingerprint = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(legacyRaw)))
            .ToLowerInvariant();
        var legacyPath = service.GetCacheFilePath(42, legacyFingerprint);
        Directory.CreateDirectory(Path.GetDirectoryName(legacyPath)!);
        await File.WriteAllTextAsync(legacyPath, "stale-epub");

        var path = await service.GetOrConvertEpubAsync(42, file, "Title", budgetSeconds: 30);
        var currentFingerprint = KoboConversionService.ComputeFingerprint(file);

        Assert.Equal(1, convertCalls);
        Assert.NotEqual(legacyFingerprint, currentFingerprint);
        Assert.Equal(service.GetCacheFilePath(42, currentFingerprint), path);
        Assert.True(File.Exists(path));
        Assert.Equal("epub-bytes", await File.ReadAllTextAsync(path));
    }

    [Fact]
    public void TryGetCachedKepubPath_SharesVersionedFingerprintWithEpubPool()
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

            var epubPath = service.GetCacheFilePath(7, fingerprint);
            var kepubPath = service.GetKepubCacheFilePath(7, fingerprint);
            Assert.Equal(Path.GetDirectoryName(epubPath), Path.GetDirectoryName(kepubPath));
            Assert.Equal(fingerprint, Path.GetFileNameWithoutExtension(epubPath));
            Assert.StartsWith(fingerprint, Path.GetFileName(kepubPath), StringComparison.OrdinalIgnoreCase);
        }
        finally
        {
            if (Directory.Exists(cacheDir)) Directory.Delete(cacheDir, true);
        }
    }

    [Fact]
    public async Task GetOrConvertEpub_HardFails_WhenSpinePageCountMismatchesChapterPages()
    {
        KoboConversionService.ResetInFlightForTests();
        var (unitOfWork, context, _) = await CreateDatabase();
        var library = new LibraryBuilder("Mismatch Lib").WithAllowKoboSync(true).Build();
        context.Library.Add(library);
        var chapter = new ChapterBuilder("1")
            .WithPages(10)
            .WithFile(new MangaFileBuilder(_cbzPath, MangaFormat.Archive, 10).WithExtension(".cbz").Build())
            .Build();
        var series = new SeriesBuilder("Mismatch Series")
            .WithFormat(MangaFormat.Archive)
            .WithVolume(new VolumeBuilder(Parser.LooseLeafVolume).WithChapter(chapter).Build())
            .Build();
        series.Library = library;
        context.Series.Add(series);
        await context.SaveChangesAsync();
        chapter = await context.Chapter.Include(c => c.Files).SingleAsync(c => c.Id == chapter.Id);

        var cacheDir = Path.Join(Path.GetTempPath(), "kavita-kobo-mismatch-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(cacheDir);

        var converter = Substitute.For<IKoboArchiveEpubConverter>();
        converter.ConvertAsync(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns(ci =>
            {
                // Convert produced 3 pages; chapter.Pages is 10.
                KoboConvertEpubTestFactory.WriteMinimalConvertEpub(ci.ArgAt<string>(1), 3);
                return Task.CompletedTask;
            });

        var directoryService = Substitute.For<IDirectoryService>();
        directoryService.LongTermCacheDirectory.Returns(cacheDir);
        await SetConversionCacheDirectory(unitOfWork, Path.Combine(cacheDir, KoboConversionService.CacheFolderName));
        directoryService.ExistOrCreate(Arg.Any<string>()).Returns(ci =>
        {
            Directory.CreateDirectory(ci.ArgAt<string>(0));
            return true;
        });

        var service = CreateService(unitOfWork, directoryService, converter);
        var ex = await Assert.ThrowsAsync<KavitaException>(() =>
            service.GetOrConvertEpubAsync(chapter.Id, chapter.Files.First(), "Title", budgetSeconds: 30));
        Assert.Equal(KoboConversionService.ConvertFailedMessage, ex.Message);

        var cacheRoot = Path.Combine(cacheDir, KoboConversionService.CacheFolderName);
        if (Directory.Exists(cacheRoot))
        {
            Assert.Empty(Directory.GetFiles(cacheRoot, "*.epub", SearchOption.AllDirectories)
                .Where(KoboConversionService.IsEpubPoolFile));
        }
    }

    [Fact]
    public async Task GetOrConvertEpub_InvalidatesStaleCache_WhenSpineMismatchesChapterPages()
    {
        KoboConversionService.ResetInFlightForTests();
        var (unitOfWork, context, _) = await CreateDatabase();
        var library = new LibraryBuilder("Stale Lib").WithAllowKoboSync(true).Build();
        context.Library.Add(library);
        var chapter = new ChapterBuilder("1")
            .WithPages(10)
            .WithFile(new MangaFileBuilder(_cbzPath, MangaFormat.Archive, 10).WithExtension(".cbz")
                .WithBytes(100).Build())
            .Build();
        var series = new SeriesBuilder("Stale Series")
            .WithFormat(MangaFormat.Archive)
            .WithVolume(new VolumeBuilder(Parser.LooseLeafVolume).WithChapter(chapter).Build())
            .Build();
        series.Library = library;
        context.Series.Add(series);
        await context.SaveChangesAsync();
        chapter = await context.Chapter.Include(c => c.Files).SingleAsync(c => c.Id == chapter.Id);

        var cacheDir = Path.Join(Path.GetTempPath(), "kavita-kobo-stale-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(cacheDir);

        var convertCalls = 0;
        var converter = Substitute.For<IKoboArchiveEpubConverter>();
        converter.ConvertAsync(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns(ci =>
            {
                convertCalls++;
                KoboConvertEpubTestFactory.WriteMinimalConvertEpub(ci.ArgAt<string>(1), 10);
                return Task.CompletedTask;
            });

        var directoryService = Substitute.For<IDirectoryService>();
        directoryService.LongTermCacheDirectory.Returns(cacheDir);
        await SetConversionCacheDirectory(unitOfWork, Path.Combine(cacheDir, KoboConversionService.CacheFolderName));
        directoryService.ExistOrCreate(Arg.Any<string>()).Returns(ci =>
        {
            Directory.CreateDirectory(ci.ArgAt<string>(0));
            return true;
        });

        var service = CreateService(unitOfWork, directoryService, converter);
        var fingerprint = KoboConversionService.ComputeFingerprint(chapter.Files.First());
        var stalePath = service.GetCacheFilePath(chapter.Id, fingerprint);
        KoboConvertEpubTestFactory.WriteMinimalConvertEpub(stalePath, 3); // stale spine ≠ Pages

        var path = await service.GetOrConvertEpubAsync(chapter.Id, chapter.Files.First(), "Title",
            budgetSeconds: 30);

        Assert.Equal(1, convertCalls);
        Assert.Equal(stalePath, path);
        Assert.Equal(10, KoboConvertEpubInspector.TryCountSpinePages(path));
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
                KoboConvertEpubTestFactory.WriteMinimalConvertEpub(output, 10);
                return Task.CompletedTask;
            });

        var directoryService = Substitute.For<IDirectoryService>();
        directoryService.LongTermCacheDirectory.Returns(cacheDir);
        await SetConversionCacheDirectory(unitOfWork, Path.Combine(cacheDir, KoboConversionService.CacheFolderName));
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
        await SetConversionCacheDirectory(unitOfWork, Path.Combine(cacheDir, KoboConversionService.CacheFolderName));
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
    public async Task EnforceConversionCacheCaps_EvictsLeastRecentlyAccessedEpub_WhenCapSet()
    {
        KoboConversionService.ResetInFlightForTests();
        var (unitOfWork, _, _) = await CreateDatabase();
        await ConfigureCacheCaps(unitOfWork, epubMaxBytes: 15, kepubMaxBytes: null);

        var cacheDir = Path.Join(Path.GetTempPath(), "kavita-kobo-lru-" + Guid.NewGuid().ToString("N"));
        var chapterDir = Path.Combine(cacheDir, KoboConversionService.CacheFolderName, "1");
        Directory.CreateDirectory(chapterDir);

        var older = Path.Combine(chapterDir, "older.epub");
        var newer = Path.Combine(chapterDir, "newer.epub");
        var kepub = Path.Combine(chapterDir, "keep" + KoboConversionService.KepubCacheExtension);
        await File.WriteAllTextAsync(older, "AAAAAAAAAA"); // 10 bytes
        await File.WriteAllTextAsync(newer, "BBBBBBBBBB"); // 10 bytes
        await File.WriteAllTextAsync(kepub, "KKKKKKKKKKKKKKKK"); // 16 bytes — different pool
        File.SetLastAccessTimeUtc(older, DateTime.UtcNow.AddHours(-2));
        File.SetLastAccessTimeUtc(newer, DateTime.UtcNow.AddHours(-1));
        File.SetLastAccessTimeUtc(kepub, DateTime.UtcNow.AddHours(-3));

        var directoryService = Substitute.For<IDirectoryService>();
        directoryService.LongTermCacheDirectory.Returns(cacheDir);
        await SetConversionCacheDirectory(unitOfWork, Path.Combine(cacheDir, KoboConversionService.CacheFolderName));
        var service = CreateService(unitOfWork, directoryService, Substitute.For<IKoboArchiveEpubConverter>());

        await service.EnforceConversionCacheCapsAsync();

        Assert.False(File.Exists(older));
        Assert.True(File.Exists(newer));
        Assert.True(File.Exists(kepub)); // KEPUB pool uncapped
    }

    [Fact]
    public async Task EnforceConversionCacheCaps_NoOp_WhenCapsUnset()
    {
        KoboConversionService.ResetInFlightForTests();
        var (unitOfWork, _, _) = await CreateDatabase();

        var cacheDir = Path.Join(Path.GetTempPath(), "kavita-kobo-lru-unlimited-" + Guid.NewGuid().ToString("N"));
        var chapterDir = Path.Combine(cacheDir, KoboConversionService.CacheFolderName, "2");
        Directory.CreateDirectory(chapterDir);
        var a = Path.Combine(chapterDir, "a.epub");
        var b = Path.Combine(chapterDir, "b.epub");
        await File.WriteAllTextAsync(a, new string('A', 100));
        await File.WriteAllTextAsync(b, new string('B', 100));

        var directoryService = Substitute.For<IDirectoryService>();
        directoryService.LongTermCacheDirectory.Returns(cacheDir);
        await SetConversionCacheDirectory(unitOfWork, Path.Combine(cacheDir, KoboConversionService.CacheFolderName));
        var service = CreateService(unitOfWork, directoryService, Substitute.For<IKoboArchiveEpubConverter>());

        await service.EnforceConversionCacheCapsAsync();

        Assert.True(File.Exists(a));
        Assert.True(File.Exists(b));
    }

    [Fact]
    public async Task ConvertAndCacheEpub_EvictsOnWrite_WhenOverCap()
    {
        KoboConversionService.ResetInFlightForTests();
        var (unitOfWork, _, _) = await CreateDatabase();
        await ConfigureCacheCaps(unitOfWork, epubMaxBytes: 12, kepubMaxBytes: null);

        var cacheDir = Path.Join(Path.GetTempPath(), "kavita-kobo-lru-write-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(cacheDir);

        var oldDir = Path.Combine(cacheDir, KoboConversionService.CacheFolderName, "10");
        Directory.CreateDirectory(oldDir);
        var oldFile = Path.Combine(oldDir, "stale.epub");
        await File.WriteAllTextAsync(oldFile, "OLDOLDOLD"); // 9 bytes
        File.SetLastAccessTimeUtc(oldFile, DateTime.UtcNow.AddDays(-1));

        var converter = Substitute.For<IKoboArchiveEpubConverter>();
        converter.ConvertAsync(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns(ci =>
            {
                var output = ci.ArgAt<string>(1);
                Directory.CreateDirectory(Path.GetDirectoryName(output)!);
                File.WriteAllText(output, "NEWNEWNEW"); // 9 bytes
                return Task.CompletedTask;
            });

        var directoryService = Substitute.For<IDirectoryService>();
        directoryService.LongTermCacheDirectory.Returns(cacheDir);
        await SetConversionCacheDirectory(unitOfWork, Path.Combine(cacheDir, KoboConversionService.CacheFolderName));
        directoryService.ExistOrCreate(Arg.Any<string>()).Returns(ci =>
        {
            Directory.CreateDirectory(ci.ArgAt<string>(0));
            return true;
        });

        var service = CreateService(unitOfWork, directoryService, converter);
        var file = new MangaFileBuilder(_cbzPath, MangaFormat.Archive, 10).WithExtension(".cbz").WithBytes(50)
            .Build();

        var written = await service.GetOrConvertEpubAsync(20, file, "Title", budgetSeconds: 30);

        Assert.True(File.Exists(written));
        Assert.False(File.Exists(oldFile));
        var remaining = Directory.GetFiles(Path.Combine(cacheDir, KoboConversionService.CacheFolderName),
                "*.epub", SearchOption.AllDirectories)
            .Where(KoboConversionService.IsEpubPoolFile)
            .ToArray();
        Assert.Single(remaining);
        Assert.Equal(written, remaining[0]);
    }

    [Fact]
    public async Task ClearConversionCache_StillWipesAllPools_WithCapsConfigured()
    {
        KoboConversionService.ResetInFlightForTests();
        var (unitOfWork, _, _) = await CreateDatabase();
        await ConfigureCacheCaps(unitOfWork, epubMaxBytes: 100, kepubMaxBytes: 100);

        var cacheDir = Path.Join(Path.GetTempPath(), "kavita-kobo-clear-caps-" + Guid.NewGuid().ToString("N"));
        var koboRoot = Path.Combine(cacheDir, KoboConversionService.CacheFolderName, "5");
        Directory.CreateDirectory(koboRoot);
        var epub = Path.Combine(koboRoot, "x.epub");
        var kepub = Path.Combine(koboRoot, "x" + KoboConversionService.KepubCacheExtension);
        await File.WriteAllTextAsync(epub, "e");
        await File.WriteAllTextAsync(kepub, "k");

        var directoryService = Substitute.For<IDirectoryService>();
        directoryService.LongTermCacheDirectory.Returns(cacheDir);
        await SetConversionCacheDirectory(unitOfWork, Path.Combine(cacheDir, KoboConversionService.CacheFolderName));
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

        Assert.False(File.Exists(epub));
        Assert.False(File.Exists(kepub));
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
                KoboConvertEpubTestFactory.WriteMinimalConvertEpub(output, 10);
                return Task.CompletedTask;
            });

        var directoryService = Substitute.For<IDirectoryService>();
        directoryService.LongTermCacheDirectory.Returns(cacheDir);
        await SetConversionCacheDirectory(unitOfWork, Path.Combine(cacheDir, KoboConversionService.CacheFolderName));
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
    public async Task GetOrConvertKepub_OverBudget_EnqueuesAndThrowsUnavailable()
    {
        KoboConversionService.ResetInFlightForTests();
        var (unitOfWork, _, _) = await CreateDatabase();
        await ConfigureKepubSettings(unitOfWork);

        var cacheDir = Path.Join(Path.GetTempPath(), "kavita-kobo-cache-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(cacheDir);

        var kepubify = Substitute.For<IKepubifyRunner>();
        kepubify.ConvertAsync(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<string>(),
                Arg.Any<CancellationToken>())
            .Returns(ci => Task.Delay(Timeout.Infinite, ci.ArgAt<CancellationToken>(3)));

        var scheduler = Substitute.For<IKoboConversionJobScheduler>();
        var directoryService = Substitute.For<IDirectoryService>();
        directoryService.LongTermCacheDirectory.Returns(cacheDir);
        await SetConversionCacheDirectory(unitOfWork, Path.Combine(cacheDir, KoboConversionService.CacheFolderName));
        directoryService.ExistOrCreate(Arg.Any<string>()).Returns(true);

        var service = CreateService(unitOfWork, directoryService, Substitute.For<IKoboArchiveEpubConverter>(),
            scheduler, kepubify: kepubify);

        var file = new MangaFileBuilder(_epubPath, MangaFormat.Epub, 10).WithBytes(100).Build();
        var ex = await Assert.ThrowsAsync<KavitaException>(() =>
            service.GetOrConvertKepubAsync(42, file, "Title", budgetSeconds: 1));
        Assert.Equal(KoboConversionService.ConvertUnavailableMessage, ex.Message);
        scheduler.Received(1).EnqueueBackgroundConvert(42);
    }

    [Fact]
    public async Task GetOrConvertKepub_WritesCache_AndDropsSyncedSet()
    {
        KoboConversionService.ResetInFlightForTests();
        var (unitOfWork, context, _) = await CreateDatabase();
        await ConfigureKepubSettings(unitOfWork);

        var library = new LibraryBuilder("Kepub Lib").WithAllowKoboSync(true).Build();
        context.Library.Add(library);
        var user = new AppUserBuilder("kepub-user", "kepub@test.com").WithLibrary(library).Build();
        context.AppUser.Add(user);
        await context.SaveChangesAsync();

        var chapter = new ChapterBuilder("1")
            .WithPages(10)
            .WithFile(new MangaFileBuilder(_epubPath, MangaFormat.Epub, 10).WithBytes(100).Build())
            .Build();
        var series = new SeriesBuilder("Kepub Series")
            .WithFormat(MangaFormat.Epub)
            .WithVolume(new VolumeBuilder(Parser.LooseLeafVolume).WithChapter(chapter).Build())
            .Build();
        series.Library = library;
        context.Series.Add(series);
        await context.SaveChangesAsync();

        chapter = await context.Chapter.Include(c => c.Files).SingleAsync(c => c.Id == chapter.Id);
        context.AppUserKoboSyncedChapter.Add(new AppUserKoboSyncedChapter
        {
            AppUserId = user.Id,
            ChapterId = chapter.Id,
        });
        await context.SaveChangesAsync();

        var cacheDir = Path.Join(Path.GetTempPath(), "kavita-kobo-cache-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(cacheDir);

        var kepubify = Substitute.For<IKepubifyRunner>();
        kepubify.ConvertAsync(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<string>(),
                Arg.Any<CancellationToken>())
            .Returns(ci =>
            {
                var output = ci.ArgAt<string>(2);
                Directory.CreateDirectory(Path.GetDirectoryName(output)!);
                File.WriteAllText(output, "kepub-bytes");
                return Task.CompletedTask;
            });

        var directoryService = Substitute.For<IDirectoryService>();
        directoryService.LongTermCacheDirectory.Returns(cacheDir);
        await SetConversionCacheDirectory(unitOfWork, Path.Combine(cacheDir, KoboConversionService.CacheFolderName));
        directoryService.ExistOrCreate(Arg.Any<string>()).Returns(ci =>
        {
            Directory.CreateDirectory(ci.ArgAt<string>(0));
            return true;
        });

        var service = CreateService(unitOfWork, directoryService, Substitute.For<IKoboArchiveEpubConverter>(),
            kepubify: kepubify);
        var source = chapter.Files.First();
        var path = await service.GetOrConvertKepubAsync(chapter.Id, source, "Title", budgetSeconds: 30);

        Assert.True(File.Exists(path));
        Assert.EndsWith(KoboConversionService.KepubCacheExtension, path);
        Assert.Equal(0, await context.AppUserKoboSyncedChapter.CountAsync(s => s.ChapterId == chapter.Id));

        // Cache hit does not re-run kepubify.
        var again = await service.GetOrConvertKepubAsync(chapter.Id, source, "Title", budgetSeconds: 1);
        Assert.Equal(path, again);
        await kepubify.Received(1).ConvertAsync(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<string>(),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task EnqueueKepubifyIfNeeded_EnqueuesWhenMissing_AndSkipsWhenCached()
    {
        KoboConversionService.ResetInFlightForTests();
        var (unitOfWork, _, _) = await CreateDatabase();
        await ConfigureKepubSettings(unitOfWork);

        var cacheDir = Path.Join(Path.GetTempPath(), "kavita-kobo-cache-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(cacheDir);
        var directoryService = Substitute.For<IDirectoryService>();
        directoryService.LongTermCacheDirectory.Returns(cacheDir);
        await SetConversionCacheDirectory(unitOfWork, Path.Combine(cacheDir, KoboConversionService.CacheFolderName));

        var scheduler = Substitute.For<IKoboConversionJobScheduler>();
        var service = CreateService(unitOfWork, directoryService, Substitute.For<IKoboArchiveEpubConverter>(),
            scheduler);
        var file = new MangaFileBuilder(_epubPath, MangaFormat.Epub, 10).WithBytes(100).Build();

        await service.EnqueueKepubifyIfNeededAsync(7, file);
        scheduler.Received(1).EnqueueBackgroundConvert(7);

        var fingerprint = KoboConversionService.ComputeFingerprint(file);
        var cached = service.GetKepubCacheFilePath(7, fingerprint);
        Directory.CreateDirectory(Path.GetDirectoryName(cached)!);
        await File.WriteAllTextAsync(cached, "kepub");

        scheduler.ClearReceivedCalls();
        await service.EnqueueKepubifyIfNeededAsync(7, file);
        scheduler.DidNotReceive().EnqueueBackgroundConvert(Arg.Any<int>());
    }

    [Fact]
    public async Task ConvertLibraryForKobo_WhenKepubEnabled_WarmsKepubForNativeEpubAndArchive()
    {
        KoboConversionService.ResetInFlightForTests();
        var (unitOfWork, context, _) = await CreateDatabase();
        await ConfigureKepubSettings(unitOfWork);

        var library = new LibraryBuilder("Warm Kepub Lib").WithAllowKoboSync(true).Build();
        context.Library.Add(library);
        await context.SaveChangesAsync();

        var cbzChapter = new ChapterBuilder("1")
            .WithPages(10)
            .WithFile(new MangaFileBuilder(_cbzPath, MangaFormat.Archive, 10).WithExtension(".cbz").Build())
            .Build();
        var epubChapter = new ChapterBuilder("1")
            .WithPages(10)
            .WithFile(new MangaFileBuilder(_epubPath, MangaFormat.Epub, 10).WithBytes(100).Build())
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

        var archiveCalls = 0;
        var converter = Substitute.For<IKoboArchiveEpubConverter>();
        converter.ConvertAsync(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns(ci =>
            {
                archiveCalls++;
                var output = ci.ArgAt<string>(1);
                KoboConvertEpubTestFactory.WriteMinimalConvertEpub(output, 10);
                return Task.CompletedTask;
            });

        var kepubCalls = 0;
        var kepubify = Substitute.For<IKepubifyRunner>();
        kepubify.ConvertAsync(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<string>(),
                Arg.Any<CancellationToken>())
            .Returns(ci =>
            {
                kepubCalls++;
                var output = ci.ArgAt<string>(2);
                // Archive KEPUB must match chapter.Pages; native EPUB KEPUB skips that check.
                KoboConvertEpubTestFactory.WriteMinimalConvertEpub(output, 10);
                return Task.CompletedTask;
            });

        var directoryService = Substitute.For<IDirectoryService>();
        directoryService.LongTermCacheDirectory.Returns(cacheDir);
        await SetConversionCacheDirectory(unitOfWork, Path.Combine(cacheDir, KoboConversionService.CacheFolderName));
        directoryService.ExistOrCreate(Arg.Any<string>()).Returns(ci =>
        {
            Directory.CreateDirectory(ci.ArgAt<string>(0));
            return true;
        });

        var service = CreateService(unitOfWork, directoryService, converter, kepubify: kepubify);
        await service.ConvertLibraryForKoboAsync(library.Id);

        Assert.Equal(1, archiveCalls);
        Assert.Equal(2, kepubCalls); // native EPUB + archive→EPUB then kepubify
        var kepubs = Directory.GetFiles(Path.Combine(cacheDir, KoboConversionService.CacheFolderName),
            "*" + KoboConversionService.KepubCacheExtension, SearchOption.AllDirectories);
        Assert.Equal(2, kepubs.Length);
    }

    [Fact]
    public async Task TryGetCachedKepubPath_ReturnsPath_WhenArtifactExists()
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

            Assert.Equal(expected, await service.TryGetCachedKepubPathAsync(99, file));
            Assert.Null(await service.TryGetCachedKepubPathAsync(100, file));
        }
        finally
        {
            if (Directory.Exists(cacheDir)) Directory.Delete(cacheDir, true);
        }
    }

    private static async Task SetConversionCacheDirectory(IUnitOfWork unitOfWork, string cacheRoot)
    {
        Directory.CreateDirectory(cacheRoot);
        var setting = await unitOfWork.DataContext.ServerSetting
            .FirstAsync(s => s.Key == ServerSettingKey.KoboConversionCacheDirectory);
        setting.Value = cacheRoot;
        await unitOfWork.CommitAsync();
    }

    private static async Task ConfigureCacheCaps(IUnitOfWork unitOfWork, long? epubMaxBytes, long? kepubMaxBytes)
    {
        var ds = new DirectoryService(Substitute.For<ILogger<DirectoryService>>(), new FileSystem());
        var settingsService = new SettingsService(unitOfWork, ds, Substitute.For<ILibraryWatcher>(),
            Substitute.For<ITaskScheduler>(), Substitute.For<ILogger<SettingsService>>(),
            Substitute.For<IOidcService>(), Substitute.For<ILoggingService>());
        var settings = await unitOfWork.SettingsRepository.GetSettingsDtoAsync();
        settings.KoboEpubCacheMaxBytes = epubMaxBytes;
        settings.KoboKepubCacheMaxBytes = kepubMaxBytes;
        await settingsService.UpdateSettings(settings);
    }

    private static async Task ConfigureKepubSettings(IUnitOfWork unitOfWork)
    {
        var ds = new DirectoryService(Substitute.For<ILogger<DirectoryService>>(), new FileSystem());
        var settingsService = new SettingsService(unitOfWork, ds, Substitute.For<ILibraryWatcher>(),
            Substitute.For<ITaskScheduler>(), Substitute.For<ILogger<SettingsService>>(),
            Substitute.For<IOidcService>(), Substitute.For<ILoggingService>());
        var settings = await unitOfWork.SettingsRepository.GetSettingsDtoAsync();
        settings.EnableKepubConversion = true;
        settings.KepubifyPath = "/usr/bin/kepubify";
        await settingsService.UpdateSettings(settings);
    }

    private static KoboConversionService CreateService(
        IUnitOfWork unitOfWork,
        IDirectoryService directoryService,
        IKoboArchiveEpubConverter converter,
        IKoboConversionJobScheduler? scheduler = null,
        IEventHub? eventHub = null,
        IKepubifyRunner? kepubify = null)
    {
        return new KoboConversionService(
            Substitute.For<ILogger<KoboConversionService>>(),
            directoryService,
            converter,
            kepubify ?? Substitute.For<IKepubifyRunner>(),
            scheduler ?? Substitute.For<IKoboConversionJobScheduler>(),
            unitOfWork,
            eventHub ?? Substitute.For<IEventHub>(),
            Substitute.For<IKoboLocationRematchService>());
    }
}
