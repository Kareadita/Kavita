using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.IO.Abstractions;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Kavita.API.Services.Scanner;
using Kavita.Models.Scanner;
using Microsoft.Extensions.Logging;

namespace Kavita.Services.Scanner;

/// <summary>
/// Finds changes on mounts file system events cannot see by reading folder times, one loop per mount.
/// Each library folder is walked once into a <see cref="FolderList"/> of every folder and its last write time. Each pass
/// then reads every folder's time again: a folder's time moves when an entry directly in it is added, removed or renamed,
/// so a moved time hands that folder to the scanner. A file rewritten in place does not move it.
/// The wait between passes is 10x the pass time (at least 60 s), so a slow share is read less often
/// </summary>
public class FolderPollerService(IFileSystem fileSystem, ILogger<FolderPollerService> logger) : IFolderPollerService
{
    private static readonly TimeSpan MinWait = TimeSpan.FromSeconds(60);
    private const int WaitPerPass = 10;
    private const string UnknownMount = "unknown";

    private readonly Lock _lock = new();
    private readonly Dictionary<string, MountLoop> _loops = new();
    private readonly Dictionary<string, FolderList> _lists = new();
    private readonly List<Task> _stopping = [];

    private sealed record MountLoop(IReadOnlyList<string> Folders, CancellationTokenSource Cancel, Task Task);

    public static TimeSpan WaitAfter(TimeSpan pass) => TimeSpan.FromTicks(Math.Max(MinWait.Ticks, pass.Ticks * WaitPerPass));

    private static string Normalize(string path)
    {
        var normalized = Parser.NormalizePath(path);
        return normalized.Length > 1 ? normalized.TrimEnd('/') : normalized;
    }

    internal FolderList? ListFor(string folder)
    {
        lock (_lock)
        {
            return _lists.GetValueOrDefault(Normalize(folder));
        }
    }

    public void Poll(IReadOnlyList<PolledFolder> folders)
    {
        var byMount = folders
            .GroupBy(f => f.Mount?.Fingerprint ?? UnknownMount)
            .ToDictionary(g => g.Key, g => g.Select(f => Normalize(f.Path)).Distinct().Order().ToList());

        lock (_lock)
        {
            foreach (var (fingerprint, loop) in _loops.ToList())
            {
                if (byMount.TryGetValue(fingerprint, out var wanted) && wanted.SequenceEqual(loop.Folders)) continue;

                StopLoop(fingerprint, loop);
            }

            var polled = byMount.Values.SelectMany(f => f).ToHashSet();
            foreach (var folder in _lists.Keys.Where(f => !polled.Contains(f)).ToList())
            {
                _lists.Remove(folder);
            }

            foreach (var (fingerprint, wanted) in byMount)
            {
                if (_loops.ContainsKey(fingerprint)) continue;

                var lists = wanted.Select(GetOrAddList).ToList();
                var cancel = new CancellationTokenSource();
                var stopping = _stopping.ToArray();
                _loops[fingerprint] = new MountLoop(wanted, cancel, Task.Run(() => RunLoopAsync(lists, stopping, cancel.Token)));
            }
        }
    }

    public void Stop()
    {
        lock (_lock)
        {
            foreach (var (fingerprint, loop) in _loops.ToList())
            {
                StopLoop(fingerprint, loop);
            }
            _lists.Clear();
        }
    }

    /// <remarks>Caller must hold <see cref="_lock"/></remarks>
    private FolderList GetOrAddList(string folder)
    {
        if (_lists.TryGetValue(folder, out var list)) return list;

        list = new FolderList(folder, fileSystem);
        _lists[folder] = list;
        return list;
    }

    /// <remarks>Caller must hold <see cref="_lock"/></remarks>
    private void StopLoop(string fingerprint, MountLoop loop)
    {
        loop.Cancel.Cancel();
        _loops.Remove(fingerprint);
        _stopping.RemoveAll(t => t.IsCompleted);
        _stopping.Add(loop.Task);
    }

    private async Task RunLoopAsync(IReadOnlyList<FolderList> lists, Task[] stopping, CancellationToken ct)
    {
        try
        {
            // A loop stopped by this restart may still be reading one of these lists; it ends at its next check
            foreach (var task in stopping)
            {
                await task.WaitAsync(ct);
            }

            foreach (var list in lists.Where(l => !l.Walked))
            {
                var sw = Stopwatch.StartNew();
                list.Walk(ct);
                logger.LogInformation("[FolderPollerService] {Folder}: polling {Count} folders, walk took {Seconds:F1} s",
                    list.Root, list.Count, sw.Elapsed.TotalSeconds);
            }

            var wait = MinWait;
            TimeSpan? loggedWait = null;
            var unreachable = new HashSet<string>();
            while (true)
            {
                await Task.Delay(wait, ct);

                var sw = Stopwatch.StartNew();
                foreach (var list in lists)
                {
                    RunPass(list, unreachable, ct);
                }

                var pass = sw.Elapsed;
                wait = WaitAfter(pass);
                if (loggedWait == null || Math.Abs((wait - loggedWait.Value).Ticks) > loggedWait.Value.Ticks / 2)
                {
                    loggedWait = wait;
                    logger.LogInformation(
                        "[FolderPollerService] {Folders}: each pass reads {Count} folder times ({PassSeconds:F1} s), then waits {WaitSeconds:F0} s. " +
                        "New, removed or renamed files are seen within about {DetectSeconds:F0} s, and scanned once validation finds them complete. " +
                        "Files replaced in place are seen at the next scheduled library scan",
                        string.Join(", ", lists.Select(l => l.Root)), lists.Sum(l => l.Count), pass.TotalSeconds, wait.TotalSeconds,
                        (wait + pass).TotalSeconds);
                }
            }
        }
        catch (OperationCanceledException) when (ct.IsCancellationRequested)
        {
#pragma warning disable S6667
            logger.LogDebug("[FolderPollerService] Stopped polling {Folders}", string.Join(", ", lists.Select(l => l.Root)));
#pragma warning restore S6667
        }
    }

    private void RunPass(FolderList list, HashSet<string> unreachable, CancellationToken ct)
    {
        FolderList.PassResult result;
        try
        {
            result = list.Pass(ct);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            logger.LogWarning(ex, "[FolderPollerService] Could not read folder times under {Folder}, trying again next pass", list.Root);
            return;
        }

        if (!result.Reachable)
        {
            if (unreachable.Add(list.Root))
            {
                logger.LogWarning("[FolderPollerService] {Folder} cannot be reached, keeping its folder list until it is back", list.Root);
            }
            return;
        }

        if (unreachable.Remove(list.Root))
        {
            logger.LogInformation("[FolderPollerService] {Folder} can be reached again", list.Root);
        }

        foreach (var folder in result.Handed)
        {
            logger.LogDebug("[FolderPollerService] {Folder} changed", folder);
            FolderWatchService.EnqueueChange(folder, true);
        }
    }

    /// <summary>
    /// Every folder under one library folder with its last write time, in memory
    /// </summary>
    internal sealed class FolderList(string root, IFileSystem fileSystem)
    {
        private static readonly EnumerationOptions ListOptions = new() { IgnoreInaccessible = true };

        private readonly Dictionary<string, DateTime> _folders = new();

        /// <param name="Reachable">False when the library folder itself cannot be read, so nothing below it was compared</param>
        /// <param name="Handed">Folders to scan: a changed folder, or for the library folder each added or removed folder in it</param>
        internal readonly record struct PassResult(bool Reachable, IReadOnlyList<string> Handed);

        private readonly record struct ChildChanges(List<string> Added, List<string> Removed);

        public string Root { get; } = Normalize(root);
        public bool Walked { get; private set; }
        public int Count => _folders.Count;

        public void Walk(CancellationToken ct)
        {
            _folders.Clear();
            AddTree(fileSystem.DirectoryInfo.New(Root), ct);
            Walked = true;
        }

        public PassResult Pass(CancellationToken ct)
        {
            var rootDirectory = fileSystem.DirectoryInfo.New(Root);
            if (!rootDirectory.Exists) return new PassResult(false, []);

            var handed = new List<string>();
            foreach (var (folder, known) in _folders.ToList())
            {
                ct.ThrowIfCancellationRequested();
                var directory = folder == Root ? rootDirectory : fileSystem.DirectoryInfo.New(folder);
                // A removed folder is found through its parent, whose time moved
                if (!directory.Exists || directory.LastWriteTimeUtc == known) continue;

                _folders[folder] = directory.LastWriteTimeUtc;
                var changes = CompareChildren(directory, ct);
                // Handing the library folder itself would scan the whole library, so hand only the series folders that came or went
                if (folder == Root)
                {
                    handed.AddRange(changes.Added);
                    handed.AddRange(changes.Removed);
                }
                else
                {
                    handed.Add(folder);
                }
            }

            return new PassResult(true, handed.Distinct().ToList());
        }

        private ChildChanges CompareChildren(IDirectoryInfo directory, CancellationToken ct)
        {
            var folder = Normalize(directory.FullName);
            var current = ListChildren(directory).ToDictionary(d => Normalize(d.FullName));
            var known = _folders.Keys.Where(k => IsChildOf(k, folder)).ToHashSet();

            var added = current.Keys.Where(c => !known.Contains(c)).ToList();
            foreach (var path in added)
            {
                AddTree(current[path], ct);
            }

            var removed = known.Where(k => !current.ContainsKey(k)).ToList();
            foreach (var path in removed)
            {
                foreach (var gone in _folders.Keys.Where(k => k == path || k.StartsWith(path + "/", StringComparison.Ordinal)).ToList())
                {
                    _folders.Remove(gone);
                }
            }

            return new ChildChanges(added, removed);
        }

        private void AddTree(IDirectoryInfo top, CancellationToken ct)
        {
            if (!top.Exists) return;

            var pending = new Stack<IDirectoryInfo>([top]);
            while (pending.Count > 0)
            {
                ct.ThrowIfCancellationRequested();
                var directory = pending.Pop();
                _folders[Normalize(directory.FullName)] = directory.LastWriteTimeUtc;

                List<IDirectoryInfo> children;
                try
                {
                    children = ListChildren(directory).ToList();
                }
                catch (Exception ex) when (directory != top && ex is IOException or UnauthorizedAccessException)
                {
                    continue;
                }

                foreach (var child in children)
                {
                    pending.Push(child);
                }
            }
        }

        private static IEnumerable<IDirectoryInfo> ListChildren(IDirectoryInfo directory) =>
            directory.EnumerateDirectories("*", ListOptions).Where(d => !Parser.IsExcludedFolderName(d.Name));

        private static bool IsChildOf(string path, string parent) =>
            path.StartsWith(parent + "/", StringComparison.Ordinal) && path.IndexOf('/', parent.Length + 1) < 0;
    }
}
