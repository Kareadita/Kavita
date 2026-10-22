using System.Collections.Concurrent;
using System.Diagnostics;
using System.Globalization;
using System.IO.Compression;

namespace WatchProbe;

/// <summary>
/// Copies a large file into the watched folder several ways and records, while each copy runs, what two watchers report
/// and whether the half-written file can be told apart: its creation time, its write time, and an open that denies writers
/// </summary>
public static class CopyProbe
{
    public const string Stream = "stream";
    public const string FileCopy = "file-copy";
    public const string Robocopy = "robocopy";
    public const string Shell = "shell";

    public static readonly IReadOnlyList<string> AllMethods = [Stream, FileCopy, Robocopy, Shell];

    private const string KavitaWatcher = "kavita";
    private const string SizeWatcher = "kavita+size";
    // Win32 ERROR_SHARING_VIOLATION (32): another process has the file open in a way the requested share mode denies
    private const int SharingViolation = unchecked((int) 0x80070020);
    private static readonly TimeSpan SampleEvery = TimeSpan.FromMilliseconds(200);
    private static readonly TimeSpan SampleAfter = TimeSpan.FromSeconds(3);

    private sealed record RawEvent(string Watcher, string Kind, TimeSpan At, string Path);

    private sealed record TargetState(bool Exists, long Bytes, DateTime? CreationUtc, DateTime? LastWriteUtc, bool InUse, string? OpenError,
        bool? ZipEnd, string? ZipEndError);

    public static IReadOnlyList<string> MethodsFor(IReadOnlyList<string> requested) =>
        OperatingSystem.IsWindows() ? requested : requested.Where(m => m is Stream or FileCopy).ToList();

    /// <summary>
    /// A real .cbz, entries stored uncompressed, so the end-of-archive check has an end to find
    /// </summary>
    public static string CreateSource(int megabytes)
    {
        var path = Path.Combine(Path.GetTempPath(), $"watchprobe-source-{megabytes}mb.cbz");
        if (File.Exists(path) && HasZipEnd(path, out _) == true) return path;

        Console.WriteLine($"Writing a {megabytes} MB source .cbz to {path} (not timed)...");
        const int entryMegabytes = 8;
        var buffer = new byte[entryMegabytes * 1024 * 1024];
        using (var zip = ZipFile.Open(path, ZipArchiveMode.Create))
        {
            for (var i = 0; i * entryMegabytes < megabytes; i++)
            {
                Random.Shared.NextBytes(buffer);
                using var entry = zip.CreateEntry($"page{i:D4}.jpg", CompressionLevel.NoCompression).Open();
                entry.Write(buffer);
            }
        }
        return path;
    }

    /// <summary>
    /// Whether the file ends with a zip end of central directory record whose central directory ends right before it.
    /// Null when the file cannot be read at all
    /// </summary>
    public static bool? HasZipEnd(string path, out string? error)
    {
        const int recordLength = 22;
        const int maxComment = 65535;
        error = null;
        try
        {
            // ReadWrite share: read alongside a writer that allows it, the in-use check is separate
            using var stream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite | FileShare.Delete);
            var length = stream.Length;
            if (length < recordLength) return false;

            var tailLength = (int) Math.Min(length, recordLength + maxComment);
            var tail = new byte[tailLength];
            stream.Seek(-tailLength, SeekOrigin.End);
            stream.ReadExactly(tail);

            for (var i = tailLength - recordLength; i >= 0; i--)
            {
                if (tail[i] != 0x50 || tail[i + 1] != 0x4B || tail[i + 2] != 0x05 || tail[i + 3] != 0x06) continue;

                var recordStart = length - tailLength + i;
                var directorySize = BitConverter.ToUInt32(tail, i + 12);
                var directoryOffset = BitConverter.ToUInt32(tail, i + 16);
                if (directoryOffset == uint.MaxValue)
                {
                    // Zip64: the real offsets sit in the zip64 record, its locator is the 20 bytes before this one
                    return i >= 20 && tail[i - 20] == 0x50 && tail[i - 19] == 0x4B && tail[i - 18] == 0x06 && tail[i - 17] == 0x07;
                }

                // A signature inside random data does not count: the record must close the central directory it names
                if (directoryOffset + (long) directorySize == recordStart) return true;
            }

            return false;
        }
        catch (Exception ex)
        {
            error = $"{ex.GetType().Name} 0x{ex.HResult:X8}";
            return null;
        }
    }

    public static CopyReport Run(EnvironmentInfo environment, string source, double seconds, IReadOnlyList<string> methods)
    {
        var sourceInfo = new FileInfo(source);
        var report = new CopyReport
        {
            Environment = environment,
            Source = source,
            SourceBytes = sourceInfo.Length,
            SourceLastWriteUtc = Utc(sourceInfo.LastWriteTimeUtc),
        };

        var clock = Stopwatch.StartNew();
        var events = new ConcurrentQueue<RawEvent>();
        using var kavita = StartWatcher(environment.Path, KavitaWatcher, null, clock, events);
        using var withSize = StartWatcher(environment.Path, SizeWatcher,
            NotifyFilters.LastWrite | NotifyFilters.FileName | NotifyFilters.DirectoryName | NotifyFilters.Size, clock, events);

        foreach (var method in methods)
        {
            var run = RunOne(environment.Path, method, sourceInfo, seconds, clock, events);
            report.Runs.Add(run);
            PrintRun(run);
        }

        return report;
    }

    public static void PrintSummary(CopyReport report)
    {
        Console.WriteLine();
        Console.WriteLine($"Summary: {report.SourceBytes / (1024 * 1024)} MB source, last written {report.SourceLastWriteUtc}");
        foreach (var run in report.Runs)
        {
            PrintRun(run);
        }
    }

    private static void PrintRun(CopyRun run)
    {
        if (run.Error != null)
        {
            Console.WriteLine($"  {run.Method,-10} failed: {run.Error}");
            return;
        }

        var watchers = string.Join(", ", run.Watchers.Select(w =>
            $"{w.Name} {w.Events} events ({w.Middle} mid-copy, longest gap {Seconds(w.LongestGapSeconds)})"));
        Console.WriteLine($"  {run.Method,-10} {run.CopySeconds,6:F1} s | created {Seconds(run.CreationOffsetSeconds)} after start | " +
                          $"write time kept: {run.LastWriteKeptFromSource} | in use during copy: {run.InUseDuringCopy}, after: {run.InUseAfterCopy}");
        Console.WriteLine($"  {"",-10} zip end: first at {Seconds(run.ZipEndFirstSeconds)}, seen mid-copy: {run.ZipEndDuringCopy}, after copy: {run.ZipEndAfterCopy}");
        Console.WriteLine($"  {"",-10} {watchers}");
    }

    private static CopyRun RunOne(string root, string method, FileInfo source, double seconds, Stopwatch clock,
        ConcurrentQueue<RawEvent> events)
    {
        var folder = Path.Combine(root, $"watchprobe-copy-{method}");
        var target = Path.Combine(folder, source.Name);
        var run = new CopyRun { Method = method, Target = target };

        try
        {
            if (Directory.Exists(folder))
            {
                Directory.Delete(folder, true);
            }
            Directory.CreateDirectory(folder);
            Thread.Sleep(1000);

            Console.WriteLine($"Copying with {method}...");
            var start = clock.Elapsed;
            var startUtc = DateTime.UtcNow;
            var copy = Task.Run(() => Copy(method, source, folder, target, seconds));

            TargetState? last = null;
            var copyEnd = (TimeSpan?) null;
            while (copyEnd == null || clock.Elapsed - copyEnd < SampleAfter)
            {
                if (copyEnd == null && copy.IsCompleted)
                {
                    copyEnd = clock.Elapsed;
                    copy.GetAwaiter().GetResult();
                }

                var state = Read(target);
                var at = clock.Elapsed - start;
                if (state != last)
                {
                    run.Samples.Add(ToSample(state, at));
                    last = state;
                }
                if (state.ZipEnd == true)
                {
                    run.ZipEndFirstSeconds ??= Round(at.TotalSeconds);
                    if (copyEnd == null)
                    {
                        run.ZipEndDuringCopy = true;
                    }
                }
                if (copyEnd != null)
                {
                    run.ZipEndAfterCopy = state.ZipEnd;
                }
                if (state.InUse)
                {
                    run.LastInUseSeconds = Round(at.TotalSeconds);
                    if (copyEnd == null)
                    {
                        run.InUseDuringCopy = true;
                    }
                    else
                    {
                        run.InUseAfterCopy = true;
                    }
                }

                Thread.Sleep(SampleEvery);
            }

            var end = copyEnd.Value - start;
            run.CopySeconds = Round(end.TotalSeconds);

            var final = new FileInfo(target);
            if (final.Exists)
            {
                run.CreationOffsetSeconds = Round((final.CreationTimeUtc - startUtc).TotalSeconds);
                run.LastWriteKeptFromSource = Math.Abs((final.LastWriteTimeUtc - source.LastWriteTimeUtc).TotalSeconds) < 2;
            }

            var mine = events
                .Where(e => e.At >= start && string.Equals(e.Path, target, StringComparison.OrdinalIgnoreCase))
                .Select(e => new CopyEvent { Watcher = e.Watcher, Kind = e.Kind, Seconds = Round((e.At - start).TotalSeconds), Path = e.Path })
                .OrderBy(e => e.Seconds)
                .ToList();
            run.Events = mine;
            run.Watchers = [Stats(KavitaWatcher, mine, end), Stats(SizeWatcher, mine, end)];
        }
        catch (Exception ex)
        {
            run.Error = $"{ex.GetType().Name}: {ex.Message}";
        }
        finally
        {
            TryDelete(folder);
        }

        return run;
    }

    private static void Copy(string method, FileInfo source, string folder, string target, double seconds)
    {
        switch (method)
        {
            case Stream:
                CopyThrottled(source, target, seconds);
                break;
            case FileCopy:
                File.Copy(source.FullName, target, true);
                break;
            case Robocopy:
            {
                // /IPG is the pause in ms after each 64 KB block
                var blocks = Math.Max(1, source.Length / (64 * 1024));
                var gap = Math.Max(1, (int) (seconds * 1000 / blocks));
                var exit = RunProcess("robocopy", $"\"{source.DirectoryName}\" \"{folder}\" \"{source.Name}\" /IPG:{gap} /NJH /NJS /NP /R:0 /W:0");
                // robocopy exits 0-7 on success, 8 and up on failure
                if (exit >= 8) throw new InvalidOperationException($"robocopy exited {exit}");
                break;
            }
            case Shell:
            {
                // Shell.Application.CopyHere is the copy engine Explorer uses; it returns at once, so the script waits for the file.
                // 1556 = no progress window (4), yes to all (16), no folder prompt (512), no error dialog (1024)
                var script = $"$s = New-Object -ComObject Shell.Application; $s.NameSpace('{folder}').CopyHere('{source.FullName}', 1556); " +
                             $"while (-not (Test-Path -LiteralPath '{target}') -or (Get-Item -LiteralPath '{target}').Length -lt {source.Length}) {{ Start-Sleep -Milliseconds 100 }}";
                var exit = RunProcess("powershell", $"-NoProfile -NonInteractive -Command \"{script}\"");
                if (exit != 0) throw new InvalidOperationException($"powershell exited {exit}");
                break;
            }
            default:
                throw new ArgumentOutOfRangeException(nameof(method), method, null);
        }
    }

    private static void CopyThrottled(FileInfo source, string target, double seconds)
    {
        const int chunk = 1024 * 1024;
        var pause = TimeSpan.FromSeconds(seconds / Math.Max(1, source.Length / chunk));
        var buffer = new byte[chunk];
        using var input = source.OpenRead();
        using var output = new FileStream(target, FileMode.Create, FileAccess.Write, FileShare.Read);
        int read;
        while ((read = input.Read(buffer)) > 0)
        {
            output.Write(buffer, 0, read);
            output.Flush();
            Thread.Sleep(pause);
        }
    }

    private static int RunProcess(string fileName, string arguments)
    {
        using var process = Process.Start(new ProcessStartInfo(fileName, arguments)
        {
            UseShellExecute = false,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            CreateNoWindow = true,
        })!;
        process.StandardOutput.ReadToEnd();
        process.WaitForExit();
        return process.ExitCode;
    }

    private static TargetState Read(string target)
    {
        var info = new FileInfo(target);
        if (!info.Exists) return new TargetState(false, 0, null, null, false, null, null, null);

        var inUse = false;
        string? openError = null;
        try
        {
            using var _ = new FileStream(target, FileMode.Open, FileAccess.Read, FileShare.Read);
        }
        catch (IOException ex) when (ex.HResult == SharingViolation)
        {
            inUse = true;
        }
        catch (Exception ex)
        {
            openError = $"{ex.GetType().Name} 0x{ex.HResult:X8}";
        }

        var zipEnd = HasZipEnd(target, out var zipEndError);
        return new TargetState(true, info.Length, info.CreationTimeUtc, info.LastWriteTimeUtc, inUse, openError, zipEnd, zipEndError);
    }

    private static CopySample ToSample(TargetState state, TimeSpan at) => new()
    {
        Seconds = Round(at.TotalSeconds),
        Exists = state.Exists,
        Bytes = state.Bytes,
        CreationUtc = state.CreationUtc is { } c ? Utc(c) : null,
        LastWriteUtc = state.LastWriteUtc is { } w ? Utc(w) : null,
        InUse = state.InUse,
        OpenError = state.OpenError,
        ZipEnd = state.ZipEnd,
        ZipEndError = state.ZipEndError,
    };

    private static CopyWatcherStats Stats(string watcher, List<CopyEvent> events, TimeSpan copyEnd)
    {
        var mine = events.Where(e => e.Watcher == watcher).Select(e => e.Seconds).ToList();
        var end = copyEnd.TotalSeconds;
        // The quiet stretch a debounce would see during the copy: from the start, between events, until the copy ends
        var marks = mine.Where(s => s <= end).Prepend(0).Append(end).OrderBy(s => s).ToList();
        var longestGap = marks.Zip(marks.Skip(1), (a, b) => b - a).DefaultIfEmpty(0).Max();

        return new CopyWatcherStats
        {
            Name = watcher,
            Events = mine.Count,
            Middle = mine.Count(s => s > 1 && s < end),
            LongestGapSeconds = Round(longestGap),
        };
    }

    private static FileSystemWatcher StartWatcher(string root, string name, NotifyFilters? filters, Stopwatch clock,
        ConcurrentQueue<RawEvent> events)
    {
        var watcher = new FileSystemWatcher(root);
        if (filters is { } f)
        {
            watcher.NotifyFilter = f;
        }
        watcher.Changed += (_, e) => events.Enqueue(new RawEvent(name, e.ChangeType.ToString(), clock.Elapsed, e.FullPath));
        watcher.Created += (_, e) => events.Enqueue(new RawEvent(name, e.ChangeType.ToString(), clock.Elapsed, e.FullPath));
        watcher.Deleted += (_, e) => events.Enqueue(new RawEvent(name, e.ChangeType.ToString(), clock.Elapsed, e.FullPath));
        watcher.Renamed += (_, e) => events.Enqueue(new RawEvent(name, "Renamed", clock.Elapsed, e.FullPath));
        watcher.Filter = "*.*";
        watcher.IncludeSubdirectories = true;
        watcher.EnableRaisingEvents = true;
        return watcher;
    }

    private static void TryDelete(string folder)
    {
        try
        {
            if (Directory.Exists(folder))
            {
                Directory.Delete(folder, true);
            }
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine($"Could not delete {folder}: {ex.Message}");
        }
    }

    private static double Round(double value) => Math.Round(value, 2);

    private static string Seconds(double? value) => value is { } v ? $"{v:F1} s" : "?";

    private static string Utc(DateTime value) => value.ToUniversalTime().ToString("O", CultureInfo.InvariantCulture);
}
