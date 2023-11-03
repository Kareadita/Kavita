using System.IO;

namespace API.Extensions;
#nullable enable

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
