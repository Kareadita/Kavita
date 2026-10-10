namespace FsProbe;

/// <summary>
/// mark saves every entry's signals; check re-reads them after someone changes the library from another machine
/// </summary>
public static class SnapshotProbe
{
    public static Snapshot Mark(string root, EnvironmentInfo environment)
    {
        var snapshot = new Snapshot { Environment = environment, MarkedTicks = DateTime.UtcNow.Ticks };
        snapshot.Entries.Add(ReadEntry(root, new DirectoryInfo(root), root));
        snapshot.Entries.AddRange(WriteTime.Enumerate(root).Select(info => ReadEntry(root, info, info.FullName)));
        Console.WriteLine($"  marked {snapshot.Entries.Count} entries");
        return snapshot;
    }

    public static CheckReport Check(string root, Snapshot snapshot, EnvironmentInfo environment)
    {
        var markedAt = new DateTime(snapshot.MarkedTicks, DateTimeKind.Utc);
        var report = new CheckReport
        {
            Environment = environment,
            MarkedUtc = markedAt.ToString("O"),
            MarkedEnvironmentPath = snapshot.Environment.Path,
        };

        var before = snapshot.Entries.ToDictionary(e => e.Path, StringComparer.Ordinal);
        var after = new Dictionary<string, SnapshotEntry>(StringComparer.Ordinal);
        var rootEntry = ReadEntry(root, new DirectoryInfo(root), root);
        after[rootEntry.Path] = rootEntry;
        foreach (var info in WriteTime.Enumerate(root))
        {
            var entry = ReadEntry(root, info, info.FullName);
            after[entry.Path] = entry;
        }

        foreach (var path in before.Keys.Union(after.Keys).Order(StringComparer.Ordinal))
        {
            before.TryGetValue(path, out var old);
            after.TryGetValue(path, out var now);

            var fields = old != null && now != null ? ChangedFields(old, now) : [];
            if (old != null && now != null && fields.Count == 0) continue;
            // A folder whose only change is its own time is the side effect of a change inside it, reported there
            if (old != null && now != null && now.IsFolder) continue;

            var change = new CheckChange
            {
                Path = path,
                Kind = old == null ? "added" : now == null ? "removed" : "modified",
                FieldsChanged = fields,
                Caught = Judge(root, path, old, now, after, markedAt),
            };
            report.Details.Add(change);
        }

        report.Changes = report.Details.Count;
        report.Caught = new CheckSummary
        {
            FolderOnly = report.Details.Count(d => d.Caught.FolderOnly),
            Current = report.Details.Count(d => d.Caught.Current),
            Candidate = report.Details.Count(d => d.Caught.Candidate),
            NamesAndSizes = report.Details.Count(d => d.Caught.NamesAndSizes),
            ChangeTime = report.Details.Count(d => d.Caught.ChangeTime == true),
        };

        Print(report);
        return report;
    }

    private static SnapshotEntry ReadEntry(string root, FileSystemInfo info, string fullPath)
    {
        var isFolder = info is DirectoryInfo;
        return new SnapshotEntry
        {
            Path = Path.GetRelativePath(root, fullPath).Replace('\\', '/'),
            IsFolder = isFolder,
            ListingTicks = info.LastWriteTimeUtc.Ticks,
            PerFileTicks = (isFolder ? Directory.GetLastWriteTimeUtc(fullPath) : File.GetLastWriteTimeUtc(fullPath)).Ticks,
            Size = info is FileInfo file ? file.Length : 0,
            ChangeTicks = ChangeTime.GetUtcTicks(fullPath),
        };
    }

    private static List<string> ChangedFields(SnapshotEntry old, SnapshotEntry now)
    {
        var fields = new List<string>();
        if (old.PerFileTicks != now.PerFileTicks) fields.Add("mtime");
        if (old.ListingTicks != now.ListingTicks) fields.Add("listing-mtime");
        if (old.Size != now.Size) fields.Add("size");
        if (old.ChangeTicks != now.ChangeTicks) fields.Add("change-time");
        return fields;
    }

    /// <summary>
    /// Judged on the parent folder the way Kavita's parent check works: the folder's own time plus the files directly in it
    /// </summary>
    private static Verdicts Judge(string root, string path, SnapshotEntry? old, SnapshotEntry? now, Dictionary<string, SnapshotEntry> after,
        DateTime markedAt)
    {
        var parentRelative = Path.GetDirectoryName(path)?.Replace('\\', '/');
        if (string.IsNullOrEmpty(parentRelative)) parentRelative = ".";
        var parentFull = Path.Combine(root, parentRelative);

        after.TryGetValue(parentRelative, out var parent);
        var folderOwn = parent != null ? new DateTime(parent.PerFileTicks, DateTimeKind.Utc) : DateTime.MaxValue;

        return new Verdicts
        {
            FolderOnly = Signals.IsAfter(folderOwn, markedAt),
            Current = Signals.IsAfter(WriteTime.Current(parentFull, SearchOption.TopDirectoryOnly), markedAt)
                      || (now?.IsFolder == true && Signals.IsAfter(folderOwn, markedAt)),
            Candidate = Signals.IsAfter(WriteTime.Candidate(parentFull, SearchOption.TopDirectoryOnly), markedAt)
                        || (now?.IsFolder == true && Signals.IsAfter(folderOwn, markedAt)),
            NamesAndSizes = old == null || now == null || old.Size != now.Size,
            ChangeTime = ChangeTime.Supported
                ? Signals.IsAfter(now?.ChangeTicks, markedAt) || Signals.IsAfter(parent?.ChangeTicks, markedAt)
                : null,
        };
    }

    private static void Print(CheckReport report)
    {
        Console.WriteLine($"  {report.Changes} changed entries since {report.MarkedUtc}");
        Console.WriteLine($"  caught by: folder time only {report.Caught.FolderOnly}, Kavita today {report.Caught.Current}, " +
                          $"candidate {report.Caught.Candidate}, names+sizes {report.Caught.NamesAndSizes}, change time {report.Caught.ChangeTime}");
        foreach (var change in report.Details.Take(30))
        {
            var c = change.Caught;
            Console.WriteLine($"  {change.Kind,-8} {change.Path}  [{string.Join(",", change.FieldsChanged)}]  " +
                              $"F={c.FolderOnly} K={c.Current} C={c.Candidate} T={c.ChangeTime?.ToString() ?? "?"}");
        }
    }
}
