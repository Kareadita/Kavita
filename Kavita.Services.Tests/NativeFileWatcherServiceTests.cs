using Hangfire;
using Kavita.API.Services.Scanner;
using Kavita.Services.Scanner;
using Kavita.Services.Tests.Helpers;
using static Kavita.Services.Scanner.NativeFileWatcherService;

namespace Kavita.Services.Tests;

[Collection(HangfireStorageCollection.Name)]
public class NativeFileWatcherServiceTests : WatchedLibraryFoldersTest
{
    private sealed record QueuedChange(string Path, bool IsDirectory);

    private static List<QueuedChange> QueuedChanges()
    {
        return JobStorage.Current.GetMonitoringApi().EnqueuedJobs(TaskScheduler.DefaultQueue, 0, int.MaxValue)
            .Select(j => j.Value.Job)
            .Where(job => job?.Method.Name == nameof(FolderWatchService.ProcessChange))
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
        var folder = Path.Join(LibraryFolders[0], "Batman", name);
        Directory.CreateDirectory(folder);
        return folder;
    }

    private async Task<NativeFileWatcherService> StartWatching()
    {
        var nativeFileWatcher = CreateNativeWatcher();
        await nativeFileWatcher.WatchAsync(LibraryFolders);
        return nativeFileWatcher;
    }

    [Fact]
    public async Task Created_File_IsNotADirectoryChange()
    {
        var file = Path.Join(SeriesFolder(string.Empty), "Batman c001.cbz");
        await StartWatching();

        await File.WriteAllTextAsync(file, "c001");

        var changes = await WaitForChangesTo(file);
        Assert.NotEmpty(changes);
        Assert.All(changes, c => Assert.False(c.IsDirectory));
    }

    [Fact]
    public async Task Changed_Folder_EnqueuesNothing()
    {
        var folder = SeriesFolder("Vol. 1");
        await StartWatching();

        Directory.SetLastWriteTimeUtc(folder, DateTime.UtcNow.AddMinutes(-1));

        await Task.Delay(1000);
        Assert.DoesNotContain(QueuedChanges(), c => c.Path == Parser.NormalizePath(folder));
    }

    [Fact]
    public async Task Renamed_FileToAnotherType_EnqueuesItsFolder()
    {
        var folder = SeriesFolder(string.Empty);
        var file = Path.Join(folder, "Batman c001.cbz");
        await File.WriteAllTextAsync(file, "c001");
        await StartWatching();

        File.Move(file, file + ".bak");

        Assert.Contains(new QueuedChange(Parser.NormalizePath(folder), true), await WaitForChangesTo(folder));
    }

    [Fact]
    public async Task Renamed_Folder_EnqueuesItsNewPath()
    {
        var folder = SeriesFolder(string.Empty);
        var renamed = Path.Join(LibraryFolders[0], "Batman Returns");
        await StartWatching();

        Directory.Move(folder, renamed);

        Assert.Contains(new QueuedChange(Parser.NormalizePath(renamed), true), await WaitForChangesTo(renamed));
    }

    [Fact]
    public async Task Deleted_File_EnqueuesItsChange()
    {
        var file = Path.Join(SeriesFolder(string.Empty), "Batman c001.cbz");
        await File.WriteAllTextAsync(file, "c001");
        await StartWatching();

        File.Delete(file);

        Assert.Contains(new QueuedChange(Parser.NormalizePath(file), false), await WaitForChangesTo(file));
    }

    [Fact]
    public async Task Deleted_FolderWithDot_IsADirectoryChange()
    {
        var folder = SeriesFolder("Vol. 1");
        await StartWatching();

        Directory.Delete(folder);

        Assert.Contains(new QueuedChange(Parser.NormalizePath(folder), true), await WaitForChangesTo(folder));
    }

    private static async Task<WatcherErrorAction[]> RaiseErrorsAtOnce(NativeFileWatcherService nativeFileWatcher,
        IEnumerable<FileSystemWatcher> watchers)
    {
        return await Task.WhenAll(watchers
            .Select(w => Task.Run(() => nativeFileWatcher.HandleErrorAsync(w, new IOException("error")))));
    }

    [Fact]
    public async Task NetworkDrop_ErrorOnEachDroppedFolder_StopsAllAndRetriesOnce()
    {
        var nativeFileWatcher = await StartWatching();
        Unreachable.UnionWith(LibraryFolders.Take(4));

        var actions = await RaiseErrorsAtOnce(nativeFileWatcher, nativeFileWatcher.Watchers.Take(4));

        Assert.Single(actions, a => a == WatcherErrorAction.FolderUnreachable);
        Assert.Equal(3, actions.Count(a => a == WatcherErrorAction.Ignore));
        Assert.Empty(nativeFileWatcher.Watchers);
    }

    [Fact]
    public async Task Overflow_ErrorsOnEveryWatcher_RestartsOnce()
    {
        var nativeFileWatcher = await StartWatching();

        var actions = await RaiseErrorsAtOnce(nativeFileWatcher, nativeFileWatcher.Watchers);

        Assert.Single(actions, a => a == WatcherErrorAction.RestartNow);
        Assert.Equal(4, actions.Count(a => a == WatcherErrorAction.Ignore));
    }

    [Fact]
    public async Task ThirdOverflowWithin10Minutes_Suspends()
    {
        var nativeFileWatcher = CreateNativeWatcher();
        var actions = new List<WatcherErrorAction>();
        for (var i = 0; i < 3; i++)
        {
            await nativeFileWatcher.WatchAsync(LibraryFolders);
            actions.Add(await nativeFileWatcher.HandleErrorAsync(nativeFileWatcher.Watchers[0], new IOException("overflow")));
        }

        Assert.Equal([WatcherErrorAction.RestartNow, WatcherErrorAction.RestartNow, WatcherErrorAction.Suspend], actions);
    }

    [Fact]
    public async Task NetworkDrops_DoNotCountTowardSuspend()
    {
        var nativeFileWatcher = CreateNativeWatcher();
        for (var i = 0; i < 3; i++)
        {
            Unreachable.Clear();
            await nativeFileWatcher.WatchAsync(LibraryFolders);
            Unreachable.Add(LibraryFolders[0]);
            Assert.Equal(WatcherErrorAction.FolderUnreachable,
                await nativeFileWatcher.HandleErrorAsync(nativeFileWatcher.Watchers[0], new IOException("network")));
        }

        Unreachable.Clear();
        await nativeFileWatcher.WatchAsync(LibraryFolders);
        Assert.Equal(WatcherErrorAction.RestartNow,
            await nativeFileWatcher.HandleErrorAsync(nativeFileWatcher.Watchers[0], new IOException("overflow")));
    }

    /// <summary>
    /// The decrement has to reach the singleton's counter, so it must be scheduled with Schedule&lt;INativeFileWatcherService&gt;.
    /// Hangfire builds a new instance for a job stored under a class, and BackgroundJob.Schedule(() => ...) stores the
    /// captured object's class even when the variable is typed as the interface
    /// </summary>
    [Fact]
    public void Overflow_SchedulesTheDecrementOnTheInterface()
    {
        ScheduleRecovery(WatcherErrorAction.RestartNow);

        var decrement = JobStorage.Current.GetMonitoringApi().ScheduledJobs(0, int.MaxValue)
            .Select(j => j.Value.Job)
            .Single(job => job.Method.Name == nameof(NativeFileWatcherService.UpdateLastBufferOverflow));
        Assert.Equal(typeof(INativeFileWatcherService), decrement.Type);
    }
}
