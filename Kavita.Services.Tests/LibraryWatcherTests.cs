using System.IO.Abstractions;
using System.IO.Abstractions.TestingHelpers;
using Hangfire;
using Hangfire.InMemory;
using Kavita.API.Database;
using Kavita.API.Services;
using Kavita.Database.Tests;
using Kavita.Models.DTOs;
using Kavita.Models.DTOs.Settings;
using Kavita.Models.Entities.Enums;
using Kavita.Services.Scanner;
using Kavita.Services.Tests.Helpers;
using Microsoft.Extensions.Logging;
using NSubstitute;

namespace Kavita.Services.Tests;

[Collection(HangfireStorageCollection.Name)]
public class LibraryWatcherTests : AbstractFsTest, IDisposable
{
    private readonly string _root = Path.Join(Path.GetTempPath(), "LibraryWatcherTests", Guid.NewGuid().ToString());
    private readonly List<string> _libraryFolders = [];
    private readonly HashSet<string> _unreachable = [];
    private readonly HashSet<string> _goneAfterExistsCheck = [];

    public LibraryWatcherTests()
    {
        JobStorage.Current = new InMemoryStorage();
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
            return (IEnumerable<LibraryDto>) [new LibraryDto { FolderWatchMode = FolderWatchMode.Automatic, Folders = _libraryFolders }];
        });

        var ds = Substitute.For<IDirectoryService>();
        ds.FileSystem.Returns(new FileSystem());
        ds.Exists(Arg.Any<string>()).Returns(c =>
        {
            var path = Parser.NormalizePath(c.Arg<string>());
            return (Directory.Exists(path) || _goneAfterExistsCheck.Contains(path)) && !_unreachable.Contains(path);
        });

        return new LibraryWatcher(ds, unitOfWork, Substitute.For<ILogger<LibraryWatcher>>(), Substitute.For<ITaskScheduler>());
    }

    private sealed record QueuedChange(string Path, bool IsDirectory);

    private static List<QueuedChange> QueuedChanges()
    {
        return JobStorage.Current.GetMonitoringApi().EnqueuedJobs(TaskScheduler.DefaultQueue, 0, int.MaxValue)
            .Select(j => j.Value.Job)
            .Where(job => job?.Method.Name == nameof(LibraryWatcher.ProcessChange))
            .Select(job => new QueuedChange(Parser.NormalizePath((string) job.Args[0]!), (bool) job.Args[1]!))
            .ToList();
    }

    /// <summary>
    /// Watcher events arrive on another thread a few ms after the write
    /// </summary>
    private static async Task<List<QueuedChange>> WaitForChangesTo(string path)
    {
        var normalized = Parser.NormalizePath(path);
        var deadline = DateTime.UtcNow.AddSeconds(5);
        while (DateTime.UtcNow < deadline && QueuedChanges().All(c => c.Path != normalized))
        {
            await Task.Delay(50);
        }

        await Task.Delay(200);
        return QueuedChanges().Where(c => c.Path == normalized).ToList();
    }

    private string SeriesFolder(string name)
    {
        var folder = Path.Join(_libraryFolders[0], "Batman", name);
        Directory.CreateDirectory(folder);
        return folder;
    }

    [Fact]
    public async Task Created_File_IsNotADirectoryChange()
    {
        var file = Path.Join(SeriesFolder(string.Empty), "Batman c001.cbz");
        await CreateRealWatcher().StartWatchersAsync();

        await File.WriteAllTextAsync(file, "c001");

        var changes = await WaitForChangesTo(file);
        Assert.NotEmpty(changes);
        Assert.All(changes, c => Assert.False(c.IsDirectory));
    }

    [Fact]
    public async Task Changed_FolderWithDot_IsADirectoryChange()
    {
        var folder = SeriesFolder("Vol. 1");
        await CreateRealWatcher().StartWatchersAsync();

        Directory.SetLastWriteTimeUtc(folder, DateTime.UtcNow.AddMinutes(-1));

        var changes = await WaitForChangesTo(folder);
        Assert.Contains(new QueuedChange(Parser.NormalizePath(folder), true), changes);
    }

    [Fact]
    public async Task Deleted_File_EnqueuesItsChange()
    {
        var file = Path.Join(SeriesFolder(string.Empty), "Batman c001.cbz");
        await File.WriteAllTextAsync(file, "c001");
        await CreateRealWatcher().StartWatchersAsync();

        File.Delete(file);

        Assert.Contains(new QueuedChange(Parser.NormalizePath(file), false), await WaitForChangesTo(file));
    }

    [Fact]
    public async Task Deleted_FolderWithDot_IsADirectoryChange()
    {
        var folder = SeriesFolder("Vol. 1");
        await CreateRealWatcher().StartWatchersAsync();

        Directory.Delete(folder);

        Assert.Contains(new QueuedChange(Parser.NormalizePath(folder), true), await WaitForChangesTo(folder));
    }

    private static async Task<WatcherErrorAction[]> RaiseErrorsAtOnce(LibraryWatcher libraryWatcher,
        IEnumerable<FileSystemWatcher> watchers)
    {
        return await Task.WhenAll(watchers
            .Select(w => Task.Run(() => libraryWatcher.HandleErrorAsync(w, new IOException("error")))));
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
