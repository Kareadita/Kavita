using System;
using System.IO;

namespace Kavita.Common.Extensions;
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

    /// <summary>
    /// True when <paramref name="path"/> is strictly below <paramref name="folder"/>. Slashes and trailing slashes are
    /// normalized on both sides, so <c>M:</c>, <c>M:/</c> and <c>M:\</c> are all the folder itself and never inside it.
    /// A sibling that shares a prefix (<c>M:/Manga2</c> against <c>M:/Manga</c>) is not inside
    /// </summary>
    public static bool IsInsideFolder(this string? path, string? folder)
    {
        if (string.IsNullOrEmpty(path) || string.IsNullOrEmpty(folder)) return false;

        return TrimmedPath(path).StartsWith(TrimmedPath(folder) + '/', StringComparison.Ordinal);
    }

    /// <summary>
    /// <see cref="IsInsideFolder"/>, but the folder itself also counts
    /// </summary>
    public static bool IsSameOrInsideFolder(this string? path, string? folder)
    {
        if (string.IsNullOrEmpty(path) || string.IsNullOrEmpty(folder)) return false;

        var trimmedPath = TrimmedPath(path);
        var trimmedFolder = TrimmedPath(folder);
        return trimmedPath.Equals(trimmedFolder, StringComparison.Ordinal)
               || trimmedPath.StartsWith(trimmedFolder + '/', StringComparison.Ordinal);
    }

    // A Linux root "/" trims to "", which makes "/" the prefix every absolute path starts with
    private static string TrimmedPath(string path) => path.NormalizePath().TrimEnd('/');
}
