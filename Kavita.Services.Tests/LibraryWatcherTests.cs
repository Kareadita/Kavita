using System.IO.Abstractions.TestingHelpers;
using Kavita.API.Database;
using Kavita.API.Services;
using Kavita.Models.DTOs;
using Kavita.Models.DTOs.Settings;
using Kavita.Services.Scanner;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using NSubstitute;

namespace Kavita.Services.Tests;

public class LibraryWatcherTests : IDisposable
{
    private readonly string _root = Path.Join(Path.GetTempPath(), "LibraryWatcherTests", Guid.NewGuid().ToString());
    private readonly List<string> _libraryFolders = [];
    private readonly HashSet<string> _unreachable = [];

    public LibraryWatcherTests()
    {
        LibraryWatcher.ResetErrorCounters();
        for (var i = 0; i < 5; i++)
        {
            var folder = Path.Join(_root, $"Library{i}");
            Directory.CreateDirectory(folder);
            _libraryFolders.Add(Parser.NormalizePath(folder));
        }
    }

    public void Dispose()
    {
        CreateRealWatcher().StopWatching();
        LibraryWatcher.ResetErrorCounters();
        if (Directory.Exists(_root))
        {
            Directory.Delete(_root, true);
        }
    }

    /// <summary>
    /// The library lookup yields before returning, so two starts can interleave the way two Hangfire jobs do
    /// </summary>
    private LibraryWatcher CreateRealWatcher()
    {
        var unitOfWork = Substitute.For<IUnitOfWork>();
        unitOfWork.SettingsRepository.GetSettingsDtoAsync()
            .Returns(new ServerSettingDto { EnableFolderWatching = true });
        unitOfWork.LibraryRepository.GetLibraryDtosAsync().Returns(async _ =>
        {
            await Task.Delay(50);
            return (IEnumerable<LibraryDto>) [new LibraryDto { FolderWatching = true, Folders = _libraryFolders }];
        });

        var ds = Substitute.For<IDirectoryService>();
        ds.Exists(Arg.Any<string>()).Returns(c =>
        {
            var path = Parser.NormalizePath(c.Arg<string>());
            return Directory.Exists(path) && !_unreachable.Contains(path);
        });

        return new LibraryWatcher(ds, unitOfWork, Substitute.For<ILogger<LibraryWatcher>>(),
            Substitute.For<IHostEnvironment>(), Substitute.For<ITaskScheduler>());
    }

    private static LibraryWatcher CreateWatcher()
    {
        var ds = new DirectoryService(Substitute.For<ILogger<DirectoryService>>(), new MockFileSystem());
        return new LibraryWatcher(ds, Substitute.For<IUnitOfWork>(), Substitute.For<ILogger<LibraryWatcher>>(),
            Substitute.For<IHostEnvironment>(), Substitute.For<ITaskScheduler>());
    }

    private static async Task<WatcherErrorAction[]> RaiseErrorsAtOnce(LibraryWatcher libraryWatcher,
        IEnumerable<FileSystemWatcher> watchers)
    {
        return await Task.WhenAll(watchers
            .Select(w => Task.Run(() => libraryWatcher.HandleErrorAsync(w, new IOException("error")))));
    }

    [Theory]
    [InlineData(new[] {"B:/Fiction"}, "B:/Fiction/Author/Book.epub", "B:/Fiction/Author")]
    [InlineData(new[] {"B:/Fiction"}, "B:/Fiction/Author/Series/Book.epub", "B:/Fiction/Author")]
    [InlineData(new[] {"M:/"}, "M:/Accel World/Accel World v01.cbz", "M:/Accel World")]
    [InlineData(new[] {"M:/"}, "M:/Accel World v01.cbz", "")]
    [InlineData(new[] {"B:/Fiction"}, "C:/Other/Book.epub", "")]
    public void GetFolder_ReturnsTopFolderUnderLibraryRoot(string[] libraryFolders, string filePath, string expected)
    {
        // GetParentDirectoryName goes through System.IO, which reads B:/ as a relative path off Windows
        if (!OperatingSystem.IsWindows()) return;

        Assert.Equal(expected, CreateWatcher().GetFolder(filePath, libraryFolders));
    }

    [Theory]
    [InlineData(new[] {"B:/Fiction", "B:/Fiction2"}, "B:/Fiction2/Author/Book.epub", "B:/Fiction2/Author")]
    [InlineData(new[] {"B:/", "B:/Fiction"}, "B:/Fiction/Author/Book.epub", "B:/Fiction/Author")]
    public void GetFolder_PicksTheLibraryRootTheFileIsIn(string[] libraryFolders, string filePath, string expected)
    {
        if (!OperatingSystem.IsWindows()) return;

        Assert.Equal(expected, CreateWatcher().GetFolder(filePath, libraryFolders));
    }

    [Fact]
    public async Task TwoRestartsAtOnce_LeaveOneWatcherPerFolder()
    {
        await Task.WhenAll(CreateRealWatcher().RestartWatching(), CreateRealWatcher().RestartWatching());

        Assert.Equal(5, LibraryWatcher.Watchers.Count);
    }

    [Fact]
    public async Task NetworkDrop_ErrorOnEachDroppedFolder_StopsAllAndRetriesOnce()
    {
        var libraryWatcher = CreateRealWatcher();
        await libraryWatcher.StartWatchersAsync();
        _unreachable.UnionWith(_libraryFolders.Take(4));

        var actions = await RaiseErrorsAtOnce(libraryWatcher, LibraryWatcher.Watchers.Take(4));

        Assert.Single(actions, a => a == WatcherErrorAction.FolderUnreachable);
        Assert.Equal(3, actions.Count(a => a == WatcherErrorAction.Ignore));
        Assert.Empty(LibraryWatcher.Watchers);
    }

    [Fact]
    public async Task Overflow_ErrorsOnEveryWatcher_RestartsOnce()
    {
        var libraryWatcher = CreateRealWatcher();
        await libraryWatcher.StartWatchersAsync();

        var actions = await RaiseErrorsAtOnce(libraryWatcher, LibraryWatcher.Watchers);

        Assert.Single(actions, a => a == WatcherErrorAction.RestartNow);
        Assert.Equal(4, actions.Count(a => a == WatcherErrorAction.Ignore));
    }

    [Fact]
    public async Task ThirdOverflowWithin10Minutes_Suspends()
    {
        var libraryWatcher = CreateRealWatcher();
        var actions = new List<WatcherErrorAction>();
        for (var i = 0; i < 3; i++)
        {
            await libraryWatcher.StartWatchersAsync();
            actions.Add(await libraryWatcher.HandleErrorAsync(LibraryWatcher.Watchers[0], new IOException("overflow")));
        }

        Assert.Equal([WatcherErrorAction.RestartNow, WatcherErrorAction.RestartNow, WatcherErrorAction.Suspend], actions);
    }

    [Fact]
    public async Task NetworkDrops_DoNotCountTowardSuspend()
    {
        var libraryWatcher = CreateRealWatcher();
        for (var i = 0; i < 3; i++)
        {
            _unreachable.Clear();
            await libraryWatcher.StartWatchersAsync();
            _unreachable.Add(_libraryFolders[0]);
            Assert.Equal(WatcherErrorAction.FolderUnreachable,
                await libraryWatcher.HandleErrorAsync(LibraryWatcher.Watchers[0], new IOException("network")));
        }

        _unreachable.Clear();
        await libraryWatcher.StartWatchersAsync();
        Assert.Equal(WatcherErrorAction.RestartNow,
            await libraryWatcher.HandleErrorAsync(LibraryWatcher.Watchers[0], new IOException("overflow")));
    }

    [Fact]
    public async Task Start_WithUnreachableFolder_WatchesTheRestAndSchedulesOneRetry()
    {
        _unreachable.Add(_libraryFolders[0]);
        var libraryWatcher = CreateRealWatcher();

        var firstRetry = await libraryWatcher.StartWatchersAsync();
        var secondRetry = await libraryWatcher.StartWatchersAsync();

        Assert.Equal(4, LibraryWatcher.Watchers.Count);
        Assert.Equal(TimeSpan.FromMinutes(5), firstRetry);
        Assert.Null(secondRetry);
    }
}
