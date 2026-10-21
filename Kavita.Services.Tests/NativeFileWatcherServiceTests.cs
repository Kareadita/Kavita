using Hangfire;
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
    public async Task ThreeErrorsOnOneFolder_DowngradesThatFolder()
    {
        var time = new ManualTime();
        var nativeFileWatcher = CreateNativeWatcher(time);
        var actions = new List<WatcherErrorAction>();
        foreach (var folder in new[] { 0, 1, 0, 1, 0 })
        {
            await nativeFileWatcher.WatchAsync(LibraryFolders);
            actions.Add(await nativeFileWatcher.HandleErrorAsync(nativeFileWatcher.Watchers[folder], new IOException("overflow")));
            time.Advance(TimeSpan.FromSeconds(61));
        }

        Assert.Equal([WatcherErrorAction.RestartNow, WatcherErrorAction.RestartNow, WatcherErrorAction.RestartNow,
            WatcherErrorAction.RestartNow, WatcherErrorAction.Downgrade], actions);
    }

    /// <summary>
    /// Each restart is immediate, so one large copy overflows the new watcher again while its events are still arriving
    /// </summary>
    [Fact]
    public async Task ErrorsFromOneBurst_CountOnce()
    {
        var time = new ManualTime();
        var nativeFileWatcher = CreateNativeWatcher(time);
        var actions = new List<WatcherErrorAction>();
        for (var i = 0; i < 3; i++)
        {
            await nativeFileWatcher.WatchAsync(LibraryFolders);
            actions.Add(await nativeFileWatcher.HandleErrorAsync(nativeFileWatcher.Watchers[0], new IOException("overflow")));
            time.Advance(TimeSpan.FromMilliseconds(150));
        }

        Assert.All(actions, a => Assert.Equal(WatcherErrorAction.RestartNow, a));
    }

    [Fact]
    public async Task NetworkDrops_DoNotCountTowardDowngrade()
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

    private static async Task WaitUntil(Func<bool> condition)
    {
        var deadline = DateTime.UtcNow.AddSeconds(5);
        while (DateTime.UtcNow < deadline && !condition())
        {
            await Task.Delay(50);
        }
    }

    /// <summary>
    /// The scanner's read folders use forward slashes, Windows events backslashes
    /// </summary>
    [Fact]
    public async Task CreatedFile_RecordsItsFolderAndParent()
    {
        var folder = Parser.NormalizePath(SeriesFolder("Vol 1"));
        var parent = Parser.NormalizePath(Path.Join(LibraryFolders[0], "Batman"));
        var nativeFileWatcher = await StartWatching();
        var before = DateTime.UtcNow;

        await File.WriteAllTextAsync(Path.Join(folder, "Batman v01.cbz"), "v01");

        await WaitUntil(() => nativeFileWatcher.SawChange(folder, before));
        Assert.True(nativeFileWatcher.SawChange(folder, before));
        Assert.True(nativeFileWatcher.SawChange(parent, before));
        Assert.False(nativeFileWatcher.SawChange(LibraryFolders[0], before));
    }

    /// <summary>
    /// Files written into a new folder before inotify watches it raise nothing
    /// </summary>
    [Fact]
    public async Task CreatedFolder_CoversEverythingBelowIt()
    {
        var nativeFileWatcher = await StartWatching();
        var before = DateTime.UtcNow;
        var created = Path.Join(LibraryFolders[0], "Big Series");

        Directory.CreateDirectory(created);

        var nested = Parser.NormalizePath(Path.Join(created, "Vol 1", "Extras"));
        await WaitUntil(() => nativeFileWatcher.SawChange(nested, before));
        Assert.True(nativeFileWatcher.SawChange(nested, before));
    }

    /// <summary>
    /// NTFS releases a folder's held back Changed when the folder is listed, so a scan would raise the event it is audited on
    /// </summary>
    [Fact]
    public async Task ListingAFolderChangedWhileUnwatched_IsNotAnEvent()
    {
        var folder = Parser.NormalizePath(SeriesFolder(string.Empty));
        await File.WriteAllTextAsync(Path.Join(folder, "Batman c001.cbz"), "c001");
        var nativeFileWatcher = await StartWatching();
        var before = DateTime.UtcNow.AddMinutes(-5);

        Directory.GetFileSystemEntries(folder);
        await Task.Delay(1500);

        Assert.False(nativeFileWatcher.SawChange(folder, before));
    }

    [Fact]
    public async Task Prune_ForgetsOldEventsAndMovesWatchingSince()
    {
        var folder = Parser.NormalizePath(SeriesFolder(string.Empty));
        var startedAround = DateTime.UtcNow;
        var nativeFileWatcher = await StartWatching();
        var before = DateTime.UtcNow;
        await File.WriteAllTextAsync(Path.Join(folder, "Batman c001.cbz"), "c001");
        await WaitUntil(() => nativeFileWatcher.SawChange(folder, before));
        Assert.InRange(nativeFileWatcher.WatchingSince(folder)!.Value, startedAround, before);

        var horizon = DateTime.UtcNow.AddSeconds(1);
        nativeFileWatcher.Prune(LibraryFolders[0], horizon);

        Assert.False(nativeFileWatcher.SawChange(folder, before));
        Assert.Equal(horizon, nativeFileWatcher.WatchingSince(folder));
        Assert.Null(nativeFileWatcher.WatchingSince(Parser.NormalizePath(TestRoot)));
    }

    private sealed class ManualTime : TimeProvider
    {
        private DateTimeOffset _now = new(2026, 10, 20, 12, 0, 0, TimeSpan.Zero);
        public override DateTimeOffset GetUtcNow() => _now;
        public void Advance(TimeSpan by) => _now += by;
    }
}
