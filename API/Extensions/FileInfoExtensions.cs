using System;
using System.IO;

namespace API.Extensions;
#nullable enable

public static class FileInfoExtensions
{
    /// <summary>
    /// Checks if the last write time of the file is after the passed date
    /// </summary>
    /// <param name="fileInfo"></param>
    /// <param name="comparison"></param>
    /// <returns></returns>
    public static bool HasFileBeenModifiedSince(this FileInfo fileInfo, DateTime comparison)
    {
        return DateTime.Compare(fileInfo.LastWriteTime, comparison) > 0;
    }
}
