using System.IO;

namespace Kavita.Common.Extensions;

public static class PathExtensions
{
    public static string GetFullPathWithoutExtension(this string filepath)
    {
        if (string.IsNullOrEmpty(filepath)) return filepath;
        var extension = Path.GetExtension(filepath);
        if (string.IsNullOrEmpty(extension)) return filepath;
        return Path.GetFullPath(filepath.Replace(extension, string.Empty));
    }
}
