using System;
using System.Collections.Generic;
using System.Linq;
using Kavita.Common.Extensions;
using Kavita.Models.Parser;

namespace Kavita.Services.Scanner;

/// <summary>
/// Decides if a folder changed by comparing its listing with the files the database holds for it
/// </summary>
public static class FolderChangeCheck
{
    /// <param name="OwnerLastScanned">Null for a failed file, which has no scan time to guard against</param>
    private readonly record struct OwnedFile(KnownFile File, DateTime? OwnerLastScanned);

    /// <summary>
    /// True when the listing has exactly the known files, each with the stored size and write time (to the second),
    /// and each written in an earlier second than its series' last scan. A file with no stored write time skips the time match
    /// </summary>
    /// <param name="onDisk">The folder listing</param>
    /// <param name="owners">Series with files in the folder</param>
    /// <param name="failedFiles">Files that failed on an earlier scan, known while their size and write time match</param>
    /// <param name="isInScope">Which of the owners' folders the listing covers</param>
    /// <param name="writeTimesToBackfill">Gets (file id, listing time) for each file with no stored write time, only when unchanged</param>
    public static bool IsUnchanged(IList<FileStamp> onDisk, IEnumerable<SeriesModified> owners, IEnumerable<FailedFile> failedFiles,
        Func<string, bool> isInScope, IDictionary<int, DateTime> writeTimesToBackfill)
    {
        var known = new Dictionary<string, OwnedFile>();
        foreach (var owner in owners)
        {
            foreach (var file in owner.FilesByFolder.Where(f => isInScope(f.Key)).SelectMany(f => f.Value))
            {
                known.TryAdd(file.Path, new OwnedFile(file, owner.LastScanned));
            }
        }

        // Add failed files after the owners, so a stored file wins over a stale failure row for the same path
        // Without this, size match would always fail and we'd have to eat a rescan of each folder that has a failed file
        foreach (var failed in failedFiles.Where(f => isInScope(f.Path.FolderOf())))
        {
            known.TryAdd(failed.Path, new OwnedFile(new KnownFile(0, failed.Path, failed.Bytes, failed.LastWriteTimeUtc), null));
        }

        // First Check: File counts match
        if (known.Count != onDisk.Count) return false;

        var backfill = new List<KeyValuePair<int, DateTime>>();
        foreach (var stamp in onDisk)
        {
            if (!known.TryGetValue(Parser.NormalizePath(stamp.Path), out var owned)) return false;
            if (owned.File.Bytes != stamp.Bytes) return false; // Second Check: File Size Match

            var stored = owned.File.LastWriteTimeUtc ?? stamp.LastWriteTimeUtc;
            if (!IsSameWriteTime(stored, stamp.LastWriteTimeUtc)) return false; // Third Check: LastWriteTime check

            // A time in the same second as the scan can hide a same-size rewrite later in that second
            if (owned.OwnerLastScanned is { } lastScanned && !IsWrittenBeforeScan(stamp.LastWriteTimeUtc, lastScanned)) return false;

            if (owned.File.LastWriteTimeUtc == null)
            {
                backfill.Add(new KeyValuePair<int, DateTime>(owned.File.Id, stamp.LastWriteTimeUtc));
            }
        }

        foreach (var (id, writeTime) in backfill)
        {
            writeTimesToBackfill[id] = writeTime;
        }

        return true;
    }

    public static bool IsSameWriteTime(DateTime storedUtc, DateTime onDiskUtc)
    {
        return storedUtc.Truncate(TimeSpan.TicksPerSecond) == onDiskUtc.Truncate(TimeSpan.TicksPerSecond);
    }

    /// <summary>
    /// Same second counts as changed, the write may have landed after the folder was read
    /// </summary>
    /// <param name="lastScanned">Local time, as stored on the series</param>
    public static bool IsWrittenBeforeScan(DateTime writeTimeUtc, DateTime lastScanned)
    {
        return lastScanned.Truncate(TimeSpan.TicksPerSecond) > writeTimeUtc.ToLocalTime().Truncate(TimeSpan.TicksPerSecond);
    }
}
