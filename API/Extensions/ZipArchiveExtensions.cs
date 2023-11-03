using System.IO;
using System.IO.Compression;
using System.Linq;

namespace API.Extensions;
#nullable enable

public static class ZipArchiveExtensions
{
    /// <summary>
    /// Checks if archive has one or more files. Excludes directory entries.
    /// </summary>
    /// <param name="archive"></param>
    /// <returns></returns>
    public static bool HasFiles(this ZipArchive archive)
    {
        return archive.Entries.Any(x => Path.HasExtension(x.FullName));
    }
}
