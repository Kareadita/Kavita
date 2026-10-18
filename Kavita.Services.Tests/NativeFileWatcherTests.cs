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
public class NativeFileWatcherTests : AbstractFsTest, IDisposable
{
    private readonly string _root = Path.Join(Path.GetTempPath(), "NativeFileWatcherTests", Guid.NewGuid().ToString());
    private readonly List<string> _libraryFolders = [];
    private readonly HashSet<string> _unreachable = [];
    private readonly HashSet<string> _goneAfterExistsCheck = [];

    public NativeFileWatcherTests()
    {
        JobStorage.Current = new InMemoryStorage();
        NativeFileWatcher.ResetErrorCounters();
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
        NativeFileWatcher.ResetErrorCounters();
        if (Directory.Exists(_root))
        {
            Directory.Delete(_root, true);
        }
    }

    /// <summary>
    /// The library lookup yields before returning, so two starts can interleave the way two Hangfire jobs do
    /// </summary>
    private NativeFileWatcher CreateRealWatcher()
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

        return new NativeFileWatcher(ds, unitOfWork, Substitute.For<ILogger<NativeFileWatcher>>(), Substitute.For<ITaskScheduler>());
    }

    private sealed record QueuedChange(string Path, bool IsDirectory);

    private static List<QueuedChange> QueuedChanges()
    {
        return JobStorage.Current.GetMonitoringApi().EnqueuedJobs(TaskScheduler.DefaultQueue, 0, int.MaxValue)
            .Select(j => j.Value.Job)
            .Where(job => job?.Method.Name == nameof(NativeFileWatcher.ProcessChange))
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

    private static async Task<WatcherErrorAction[]> RaiseErrorsAtOnce(NativeFileWatcher nativeFileWatcher,
        IEnumerable<FileSystemWatcher> watchers)
    {
        return await Task.WhenAll(watchers
            .Select(w => Task.Run(() => nativeFileWatcher.HandleErrorAsync(w, new IOException("error")))));
    }

    [Fact]
    public async Task TwoRestartsAtOnce_LeaveOneWatcherPerFolder()
    {
        await Task.WhenAll(CreateRealWatcher().RestartWatching(), CreateRealWatcher().RestartWatching());

        Assert.Equal(5, NativeFileWatcher.Watchers.Count);
    }

    [Fact]
    public async Task NetworkDrop_ErrorOnEachDroppedFolder_StopsAllAndRetriesOnce()
    {
        var nativeFileWatcher = CreateRealWatcher();
        await nativeFileWatcher.StartWatchersAsync();
        _unreachable.UnionWith(_libraryFolders.Take(4));

        var actions = await RaiseErrorsAtOnce(nativeFileWatcher, NativeFileWatcher.Watchers.Take(4));

        Assert.Single(actions, a => a == WatcherErrorAction.FolderUnreachable);
        Assert.Equal(3, actions.Count(a => a == WatcherErrorAction.Ignore));
        Assert.Empty(NativeFileWatcher.Watchers);
    }

    [Fact]
    public async Task Overflow_ErrorsOnEveryWatcher_RestartsOnce()
    {
        var nativeFileWatcher = CreateRealWatcher();
        await nativeFileWatcher.StartWatchersAsync();

        var actions = await RaiseErrorsAtOnce(nativeFileWatcher, NativeFileWatcher.Watchers);

        Assert.Single(actions, a => a == WatcherErrorAction.RestartNow);
        Assert.Equal(4, actions.Count(a => a == WatcherErrorAction.Ignore));
    }

    [Fact]
    public async Task ThirdOverflowWithin10Minutes_Suspends()
    {
        var nativeFileWatcher = CreateRealWatcher();
        var actions = new List<WatcherErrorAction>();
        for (var i = 0; i < 3; i++)
        {
            await nativeFileWatcher.StartWatchersAsync();
            actions.Add(await nativeFileWatcher.HandleErrorAsync(NativeFileWatcher.Watchers[0], new IOException("overflow")));
        }

        Assert.Equal([WatcherErrorAction.RestartNow, WatcherErrorAction.RestartNow, WatcherErrorAction.Suspend], actions);
    }

    [Fact]
    public async Task NetworkDrops_DoNotCountTowardSuspend()
    {
        var nativeFileWatcher = CreateRealWatcher();
        for (var i = 0; i < 3; i++)
        {
            _unreachable.Clear();
            await nativeFileWatcher.StartWatchersAsync();
            _unreachable.Add(_libraryFolders[0]);
            Assert.Equal(WatcherErrorAction.FolderUnreachable,
                await nativeFileWatcher.HandleErrorAsync(NativeFileWatcher.Watchers[0], new IOException("network")));
        }

        _unreachable.Clear();
        await nativeFileWatcher.StartWatchersAsync();
        Assert.Equal(WatcherErrorAction.RestartNow,
            await nativeFileWatcher.HandleErrorAsync(NativeFileWatcher.Watchers[0], new IOException("overflow")));
    }

    [Fact]
    public async Task Start_WithUnreachableFolder_WatchesTheRestAndSchedulesOneRetry()
    {
        _unreachable.Add(_libraryFolders[0]);
        var nativeFileWatcher = CreateRealWatcher();

        var firstRetry = await nativeFileWatcher.StartWatchersAsync();
        var secondRetry = await nativeFileWatcher.StartWatchersAsync();

        Assert.Equal(4, NativeFileWatcher.Watchers.Count);
        Assert.Equal(TimeSpan.FromMinutes(5), firstRetry);
        Assert.Null(secondRetry);
    }

    [Fact]
    public async Task Start_FolderGoneAfterExistsCheck_WatchesTheRestAndSchedulesARetry()
    {
        var gone = Parser.NormalizePath(Path.Join(_root, "Gone"));
        _goneAfterExistsCheck.Add(gone);
        _libraryFolders.Insert(0, gone);
        var nativeFileWatcher = CreateRealWatcher();

        var retry = await nativeFileWatcher.StartWatchersAsync();

        Assert.Equal(5, NativeFileWatcher.Watchers.Count);
        Assert.Equal(TimeSpan.FromMinutes(5), retry);
    }
}
