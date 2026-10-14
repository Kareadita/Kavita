namespace ProbeCommon;

public static class FileListing
{
    // IgnoreInaccessible so one unreadable folder does not end the probe; Kavita's call throws instead
    public static EnumerationOptions Options(bool recurse) => new()
    {
        RecurseSubdirectories = recurse,
        IgnoreInaccessible = true,
        AttributesToSkip = 0,
        MatchType = MatchType.Win32,
    };

    public static IEnumerable<FileSystemInfo> Enumerate(string folderPath, bool recurse = true) =>
        new DirectoryInfo(folderPath).EnumerateFileSystemInfos("*.*", Options(recurse));
}
