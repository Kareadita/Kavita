using System.IO.Abstractions;
using Kavita.API.Database;
using Kavita.API.Repositories;
using Kavita.API.Services;
using Kavita.API.Services.Scanner;
using Kavita.API.Services.SignalR;
using Kavita.Models.Builders;
using Kavita.Models.DTOs.Settings;
using Kavita.Models.DTOs.SignalR;
using Kavita.Models.DTOs.SignalR.Bodies;
using Kavita.Models.Entities;
using Kavita.Models.Entities.Enums;
using Kavita.Models.Scanner;
using Kavita.Services.Scanner;
using Microsoft.Extensions.Logging;
using NSubstitute;
using static Kavita.Models.Entities.LibraryFolderWatchState;

namespace Kavita.Services.Tests;

public class FolderWatchServiceAuditTests : IDisposable
{
    private static readonly MountInfo LocalMount = new("/", "/dev/sda1", "ext4");

    private readonly string _testRoot = Path.Join(Path.GetTempPath(), "FolderWatchServiceAuditTests", Guid.NewGuid().ToString());
    private readonly string _libraryFolder;
    private readonly string _seriesFolder;
    private readonly INativeFileWatcherService _nativeFileWatcher = Substitute.For<INativeFileWatcherService>();
    private readonly IFolderPollerService _folderPoller = Substitute.For<IFolderPollerService>();
    private readonly IEventHub _eventHub = Substitute.For<IEventHub>();
    private readonly Library _library;
    private readonly List<Library> _libraries = [];
    private readonly DateTime _scanStartedUtc = DateTime.UtcNow;
    /// <summary>
    /// When the stub watcher last saw an event in the series folder, null for never
    /// </summary>
    private DateTime? _eventUtc;
    private DateTime? _watchingSince;

    public FolderWatchServiceAuditTests()
    {
        _libraryFolder = Parser.NormalizePath(Path.Join(_testRoot, "Manga"));
        _seriesFolder = Parser.NormalizePath(Path.Join(_libraryFolder, "Batman"));
        Directory.CreateDirectory(_seriesFolder);
        _watchingSince = _scanStartedUtc.AddHours(-2);

        _nativeFileWatcher.SawChange(_seriesFolder, Arg.Any<DateTime>()).Returns(c => _eventUtc >= c.ArgAt<DateTime>(1));
        _nativeFileWatcher.WatchingSince(Arg.Any<string>()).Returns(_ => _watchingSince);
        _nativeFileWatcher.When(w => w.Prune(Arg.Any<string>(), Arg.Any<DateTime>())).Do(c =>
        {
            var cutoff = c.ArgAt<DateTime>(1);
            if (_watchingSince < cutoff)
            {
                _watchingSince = cutoff;
            }
        });

        _library = new LibraryBuilder("Manga").Build();
        _library.FolderWatchMode = FolderWatchMode.Automatic;
        _library.Folders = [new FolderPath { Path = _libraryFolder }];
        _library.FolderWatchState = new LibraryFolderWatchState
        {
            Mounts = [new MountEntry { Fingerprint = LocalMount.Fingerprint, Result = MountResult.Trial }],
        };
        _libraries.Add(_library);
    }

    private FolderWatchService CreateFolderWatchService()
    {
        var unitOfWork = Substitute.For<IUnitOfWork>();
        unitOfWork.SettingsRepository.GetSettingsDtoAsync().Returns(new ServerSettingDto { EnableFolderWatching = true });
        unitOfWork.LibraryRepository.GetLibraryForIdAsync(1, Arg.Any<LibraryIncludes>(), Arg.Any<CancellationToken>()).Returns(_library);
        unitOfWork.LibraryRepository.GetLibrariesAsync(Arg.Any<LibraryIncludes>(), Arg.Any<bool>(), Arg.Any<CancellationToken>())
            .Returns(_ => (IEnumerable<Library>) _libraries);

        var mountDetector = Substitute.For<IMountDetectorService>();
        mountDetector.Detect(Arg.Any<string>()).Returns(LocalMount);
        var directoryService = Substitute.For<IDirectoryService>();
        directoryService.FileSystem.Returns(new FileSystem());

        return new FolderWatchService(unitOfWork, Substitute.For<ILogger<FolderWatchService>>(), Substitute.For<ITaskScheduler>(),
            mountDetector, _nativeFileWatcher, _folderPoller, directoryService, _eventHub);
    }

    private Task FallbackEventsSent(int count, Func<FolderWatchFallbackEventBodyDto, bool> matches)
    {
        return _eventHub.Received(count).SendMessageAsync(MessageFactory.Info,
            Arg.Is<SignalRMessageDto>(m => IsFallback(m, matches)));
    }

    private static bool IsFallback(SignalRMessageDto message, Func<FolderWatchFallbackEventBodyDto, bool> matches)
    {
        return message.Body is FolderWatchFallbackEventBodyDto body && matches(body);
    }

    private void ChangedBeforeScan(TimeSpan before)
    {
        Directory.SetLastWriteTimeUtc(_seriesFolder, _scanStartedUtc - before);
    }

    private Task Audit(bool isLibraryScan = false)
    {
        return CreateFolderWatchService().AuditScanAsync(1, [_seriesFolder], _scanStartedUtc, isLibraryScan);
    }

    private MountEntry Mount => Assert.Single(_library.FolderWatchState.Mounts);

    /// <summary>
    /// Saving Failed is not enough: the miss must restart watching, or the library stays on a watcher that misses changes
    /// </summary>
    [Fact]
    public async Task NoEvent_IsMiss_SetsFailedAndPolls()
    {
        ChangedBeforeScan(TimeSpan.FromHours(1));

        await Audit();

        Assert.Equal(MountResult.Failed, Mount.Result);
        _folderPoller.Received().Poll(Arg.Is<IReadOnlyList<PolledFolder>>(folders => folders.Count == 1));
    }

    /// <summary>
    /// A watcher's own scan starts about 30 s after the change, inside the window where a pending scan is skipped
    /// </summary>
    [Fact]
    public async Task WatcherScan_TInsideCap_IsSuccessNotSkip()
    {
        ChangedBeforeScan(TimeSpan.FromSeconds(30));
        _eventUtc = _scanStartedUtc.AddSeconds(-30);

        await Audit();

        Assert.Equal(1, Mount.Successes);
    }

    /// <summary>
    /// A change from before the watcher started, or under 2 min after, could not have been seen, so it is never a miss
    /// </summary>
    [Theory]
    [InlineData(31)]
    [InlineData(29)]
    public async Task TBeforeOrJustAfterWatchingSince_IsSkipped(int minutesBeforeScan)
    {
        _watchingSince = _scanStartedUtc.AddMinutes(-30);
        ChangedBeforeScan(TimeSpan.FromMinutes(minutesBeforeScan));

        await Audit();

        Assert.Equal(MountResult.Trial, Mount.Result);
        Assert.Equal(0, Mount.Successes);
    }

    /// <summary>
    /// Watcher scans alone never leave Trial: they only read folders the watcher saw, so only a library scan can catch a miss
    /// </summary>
    [Fact]
    public async Task ThreeSuccessesAndCleanLibraryScan_MovesToWatching()
    {
        ChangedBeforeScan(TimeSpan.FromHours(1));
        _eventUtc = _scanStartedUtc.AddHours(-1);

        for (var i = 0; i < 3; i++)
        {
            await Audit();
        }
        Assert.Equal(MountResult.Trial, Mount.Result);

        await CreateFolderWatchService().AuditScanAsync(1, [], _scanStartedUtc, true);

        Assert.Equal(MountResult.Watching, Mount.Result);
        Assert.Equal(3, Mount.Successes);
        Assert.Equal(1, Mount.AuditedScans);
    }

    /// <summary>
    /// The admin chose File Watcher, so a miss keeps watching; only watcher errors move it to polling
    /// </summary>
    [Fact]
    public async Task FileWatcherMode_MissKeepsWatching()
    {
        _library.FolderWatchMode = FolderWatchMode.FileWatcher;
        ChangedBeforeScan(TimeSpan.FromHours(1));

        await Audit();

        Assert.Equal(MountResult.Failed, Mount.Result);
        Assert.Equal(FolderWatchMethod.FileWatcher, _library.FolderWatchState.WatchMethod(_library.FolderWatchMode));
        await _nativeFileWatcher.DidNotReceive().WatchAsync(Arg.Any<IReadOnlyList<string>>());
    }

    /// <summary>
    /// Pruning at the scan start would drop the events of changes whose watcher scan is still waiting, and that scan's success with them
    /// </summary>
    [Fact]
    public async Task LibraryScan_PrunesEventsBeforeTheScanCap()
    {
        await CreateFolderWatchService().AuditScanAsync(1, [], _scanStartedUtc, true);

        _nativeFileWatcher.Received().Prune(_libraryFolder, _scanStartedUtc - TaskScheduler.WatcherScanCap);
    }

    /// <summary>
    /// The scan's own prune cutoff is the far edge of the miss window, so pruning before judging would skip every miss
    /// </summary>
    [Fact]
    public async Task LibraryScan_Miss_IsNotHiddenByItsOwnPrune()
    {
        ChangedBeforeScan(TimeSpan.FromHours(1));

        await Audit(isLibraryScan: true);

        Assert.Equal(MountResult.Failed, Mount.Result);
    }

    [Fact]
    public async Task Miss_SendsFallbackEvent()
    {
        ChangedBeforeScan(TimeSpan.FromHours(1));

        await Audit();

        await FallbackEventsSent(1, body => body.Reason == FolderWatchFallbackReason.MissedChange && body.Folder == _seriesFolder);
    }

    [Fact]
    public async Task DowngradeFolder_SendsFallbackEventPerLibraryThatFellBack()
    {
        var alreadyPolling = new LibraryBuilder("Comics").Build();
        alreadyPolling.FolderWatchMode = FolderWatchMode.Automatic;
        alreadyPolling.Folders = [new FolderPath { Path = _libraryFolder }];
        alreadyPolling.FolderWatchState = new LibraryFolderWatchState
        {
            Mounts = [new MountEntry { Fingerprint = LocalMount.Fingerprint, Result = MountResult.Failed }],
        };
        _libraries.Add(alreadyPolling);

        await CreateFolderWatchService().DowngradeFolder(_libraryFolder, DowngradeReason.Errors);

        await FallbackEventsSent(1, body => body.LibraryName == "Manga" && body.Reason == FolderWatchFallbackReason.Errors);
        await FallbackEventsSent(0, body => body.LibraryName == "Comics");
    }

    public void Dispose()
    {
        if (Directory.Exists(_testRoot))
        {
            Directory.Delete(_testRoot, true);
        }
        GC.SuppressFinalize(this);
    }
}
