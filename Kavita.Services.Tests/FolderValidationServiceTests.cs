using System.IO.Abstractions;
using System.IO.Compression;
using Hangfire;
using Hangfire.InMemory;
using Kavita.API.Database;
using Kavita.API.Services;
using Kavita.API.Services.SignalR;
using Kavita.Models.DTOs.SignalR;
using Kavita.Models.Scanner;
using Kavita.Services.Scanner;
using Kavita.Services.Tests.Helpers;
using Microsoft.Extensions.Logging;
using NSubstitute;

namespace Kavita.Services.Tests;

[Collection(HangfireStorageCollection.Name)]
public class FolderValidationServiceTests : IDisposable
{
    private readonly string _folder = Path.Join(Path.GetTempPath(), "FolderValidationServiceTests", Guid.NewGuid().ToString());
    private readonly DateTime _firstChangeUtc = DateTime.UtcNow.AddSeconds(-30);
    private readonly IEventHub _eventHub = Substitute.For<IEventHub>();
    private readonly FolderValidationService _service;

    public FolderValidationServiceTests()
    {
        JobStorage.Current = new InMemoryStorage();
        Directory.CreateDirectory(_folder);
        var directoryService = new DirectoryService(Substitute.For<ILogger<DirectoryService>>(), new FileSystem());
        var archiveService = new ArchiveService(Substitute.For<ILogger<ArchiveService>>(), directoryService,
            Substitute.For<IImageService>(), Substitute.For<IMediaErrorService>());
        _service = new FolderValidationService(Substitute.For<IUnitOfWork>(), archiveService, directoryService, _eventHub,
            Substitute.For<ILogger<FolderValidationService>>());
    }

    public void Dispose()
    {
        Directory.Delete(_folder, true);
    }

    [Fact]
    public void CompleteArchive_Passes()
    {
        WriteArchive(Path.Join(_folder, "c001.cbz"));

        Assert.Empty(_service.FindFilesStillWriting(_folder, true, _firstChangeUtc));
    }

    [Fact]
    public void HalfWrittenArchive_IsStillWriting()
    {
        var path = WriteHalfArchive(Path.Join(_folder, "Vol 1", "c001.cbz"));

        Assert.Equal([path], _service.FindFilesStillWriting(_folder, true, _firstChangeUtc));
    }

    [Fact]
    public void HalfWrittenArchive_WrittenLongBeforeTheChange_IsNotOpened()
    {
        var path = WriteHalfArchive(Path.Join(_folder, "c001.cbz"));
        File.SetLastWriteTimeUtc(path, _firstChangeUtc.AddMinutes(-10));

        Assert.Empty(_service.FindFilesStillWriting(_folder, true, _firstChangeUtc));
    }

    [Fact]
    public void ArchiveHeldOpenForWriting_IsStillWriting()
    {
        // Linux has no mandatory locks, an open never fails there
        if (!OperatingSystem.IsWindows()) return;

        var path = Path.Join(_folder, "c001.cbz");
        WriteArchive(path);
        using var writer = new FileStream(path, FileMode.Open, FileAccess.Write, FileShare.Read);

        Assert.Equal([Parser.NormalizePath(path)], _service.FindFilesStillWriting(_folder, true, _firstChangeUtc));
    }

    [Fact]
    public async Task ValidateFolder_FileStillWriting_RetryKeepsTheFirstChange()
    {
        WriteHalfArchive(Path.Join(_folder, "c001.cbz"));

        await _service.ValidateFolder(Change(), _firstChangeUtc);

        var retry = Assert.Single(ScanJobQueue.Read().FolderJobs);
        Assert.NotNull(retry.RunAtUtc);
        Assert.Equal(_firstChangeUtc, retry.FirstChangeUtc);
        Assert.Equal(1, ScanRescheduledCount());
    }

    [Fact]
    public async Task ValidateFolder_PastTheCap_QueuesTheScanWithoutChecking()
    {
        WriteHalfArchive(Path.Join(_folder, "c001.cbz"));

        await _service.ValidateFolder(Change(), DateTime.UtcNow.AddMinutes(-11));

        var scan = Assert.Single(ScanJobQueue.Read().FolderJobs);
        Assert.Null(scan.FirstChangeUtc);
    }

    private ScanFolderRequest Change()
    {
        var folder = Parser.NormalizePath(_folder);
        return new ScanFolderRequest(1, folder, folder, true, false);
    }

    private int ScanRescheduledCount()
    {
        return _eventHub.ReceivedCalls().Count(c => c.GetMethodInfo().Name == nameof(IEventHub.SendMessageAsync)
                                                    && (string) c.GetArguments()[0]! == MessageFactory.ScanRescheduled);
    }

    private static void WriteArchive(string path)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        using var archive = ZipFile.Open(path, ZipArchiveMode.Create);
        for (var page = 1; page <= 5; page++)
        {
            using var entry = archive.CreateEntry($"{page:000}.jpg", CompressionLevel.NoCompression).Open();
            entry.Write(new byte[64 * 1024]);
        }
    }

    private static string WriteHalfArchive(string path)
    {
        WriteArchive(path);
        var bytes = File.ReadAllBytes(path);
        File.WriteAllBytes(path, bytes[..(bytes.Length / 2)]);
        return Parser.NormalizePath(path);
    }
}
