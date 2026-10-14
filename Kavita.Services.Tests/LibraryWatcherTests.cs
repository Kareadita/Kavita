using System.IO.Abstractions.TestingHelpers;
using Kavita.API.Database;
using Kavita.API.Services;
using Kavita.Database.Tests;
using Kavita.Models.DTOs;
using Kavita.Models.DTOs.Settings;
using Kavita.Services.Scanner;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using NSubstitute;

namespace Kavita.Services.Tests;

public class LibraryWatcherTests : AbstractFsTest, IDisposable
{
    private readonly string _root = Path.Join(Path.GetTempPath(), "LibraryWatcherTests", Guid.NewGuid().ToString());
    private readonly List<string> _libraryFolders = [];
    private readonly HashSet<string> _unreachable = [];
    private readonly HashSet<string> _goneAfterExistsCheck = [];

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
            return (Directory.Exists(path) || _goneAfterExistsCheck.Contains(path)) && !_unreachable.Contains(path);
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

    private static string GetRootedFolder(string[] libraryFolders, string filePath)
    {
        return CreateWatcher().GetFolder(Root + filePath, libraryFolders.Select(f => Root + f));
    }

    [Theory]
    [InlineData(new[] {"Fiction"}, "Fiction/Author/Book.epub", "Fiction/Author")]
    [InlineData(new[] {"Fiction"}, "Fiction/Author/Series/Book.epub", "Fiction/Author")]
    [InlineData(new[] {""}, "Accel World/Accel World v01.cbz", "Accel World")]
    [InlineData(new[] {""}, "Accel World v01.cbz", "")]
    [InlineData(new[] {"Fiction"}, "Other/Book.epub", "")]
    public void GetFolder_ReturnsTopFolderUnderLibraryRoot(string[] libraryFolders, string filePath, string expected)
    {
        var expectedFolder = expected == string.Empty ? string.Empty : Root + expected;
        Assert.Equal(expectedFolder, GetRootedFolder(libraryFolders, filePath));
    }

    [Theory]
    [InlineData(new[] {"Fiction", "Fiction2"}, "Fiction2/Author/Book.epub", "Fiction2/Author")]
    [InlineData(new[] {"", "Fiction"}, "Fiction/Author/Book.epub", "Fiction/Author")]
    public void GetFolder_PicksTheLibraryRootTheFileIsIn(string[] libraryFolders, string filePath, string expected)
    {
        Assert.Equal(Root + expected, GetRootedFolder(libraryFolders, filePath));
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

    [Fact]
    public async Task Start_FolderGoneAfterExistsCheck_WatchesTheRestAndSchedulesARetry()
    {
        var gone = Parser.NormalizePath(Path.Join(_root, "Gone"));
        _goneAfterExistsCheck.Add(gone);
        _libraryFolders.Insert(0, gone);
        var libraryWatcher = CreateRealWatcher();

        var retry = await libraryWatcher.StartWatchersAsync();

        Assert.Equal(5, LibraryWatcher.Watchers.Count);
        Assert.Equal(TimeSpan.FromMinutes(5), retry);
    }
}
