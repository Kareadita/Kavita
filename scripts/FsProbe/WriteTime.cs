namespace FsProbe;

public static class WriteTime
{
    // IgnoreInaccessible so one unreadable folder does not end the probe; Kavita's call throws instead
    private static EnumerationOptions Options(bool recurse) => new()
    {
        RecurseSubdirectories = recurse,
        IgnoreInaccessible = true,
        AttributesToSkip = 0,
        MatchType = MatchType.Win32,
    };

    /// <summary>
    /// Copy of DirectoryService.GetLastWriteTime as of S5, in UTC: list the entries, then one call per entry
    /// </summary>
    public static DateTime Current(string folderPath, SearchOption searchOption = SearchOption.AllDirectories)
    {
        if (!Directory.Exists(folderPath)) return DateTime.MaxValue;

        var fileEntries = searchOption == SearchOption.AllDirectories
            ? Directory.GetFileSystemEntries(folderPath, "*.*", Options(true))
            : Directory.GetFiles(folderPath, "*.*", Options(false));
        if (fileEntries.Length == 0) return DateTime.MaxValue;

        var maxFiles = fileEntries.Max(File.GetLastWriteTimeUtc);
        var directoryLastWriteTime = Directory.GetLastWriteTimeUtc(folderPath);

        return directoryLastWriteTime > maxFiles ? directoryLastWriteTime : maxFiles;
    }

    /// <summary>
    /// S6 candidate: the timestamp comes from the directory listing
    /// </summary>
    public static DateTime Candidate(string folderPath, SearchOption searchOption = SearchOption.AllDirectories)
    {
        var dir = new DirectoryInfo(folderPath);
        if (!dir.Exists) return DateTime.MaxValue;

        IEnumerable<FileSystemInfo> entries = searchOption == SearchOption.AllDirectories
            ? dir.EnumerateFileSystemInfos("*.*", Options(true))
            : dir.EnumerateFiles("*.*", Options(false));

        var any = false;
        var maxFiles = DateTime.MinValue;
        foreach (var entry in entries)
        {
            any = true;
            var time = entry.LastWriteTimeUtc;
            if (time > maxFiles) maxFiles = time;
        }
        if (!any) return DateTime.MaxValue;

        var directoryLastWriteTime = dir.LastWriteTimeUtc;
        return directoryLastWriteTime > maxFiles ? directoryLastWriteTime : maxFiles;
    }

    public static IEnumerable<FileSystemInfo> Enumerate(string folderPath, bool recurse = true) =>
        new DirectoryInfo(folderPath).EnumerateFileSystemInfos("*.*", Options(recurse));

    public static DateTime TruncateToSecond(DateTime time) =>
        time == DateTime.MaxValue ? time : new DateTime(time.Ticks - time.Ticks % TimeSpan.TicksPerSecond, time.Kind);

    public static string Iso(long ticks) => new DateTime(ticks, DateTimeKind.Utc).ToString("O");
}
