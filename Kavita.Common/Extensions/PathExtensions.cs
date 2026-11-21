using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

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

    /// <summary>
    /// The deepest of <paramref name="folders"/> that is <paramref name="path"/> or holds it, null when none does.
    /// Library roots can nest (<c>B:/</c> and <c>B:/Fiction</c>)
    /// </summary>
    public static string? DeepestContainingFolder(this string? path, IEnumerable<string> folders)
    {
        return folders.Where(path.IsSameOrInsideFolder).MaxBy(folder => folder.Length);
    }

    /// <summary>
    /// The folder that directly holds a file, from a normalized path. <c>/a.cbz</c> gives <c>/</c>, a bare file name gives an empty string
    /// </summary>
    public static string FolderOf(this string path)
    {
        var lastSlash = path.LastIndexOf('/');
        return lastSlash switch
        {
            < 0 => string.Empty,
            0 => "/",
            _ => path[..lastSlash],
        };
    }

    /// <summary>
    /// Reduces untrusted input to a bare file name: <c>../../config/kavita.db</c> gives <c>kavita.db</c>.
    /// Returns empty when no file name is left, for example <c>..</c> or <c>folder/</c>
    /// </summary>
    public static string SanitizeFileName(this string? path)
    {
        if (string.IsNullOrEmpty(path)) return string.Empty;

        // Linux does not treat '\' as a separator, so a Windows-style name would otherwise keep its folders
        var name = Path.GetFileName(path.Replace('\\', '/'));
        if (name is "." or "..") return string.Empty;

        var invalid = Path.GetInvalidFileNameChars();
        return string.Concat(name.Where(c => !char.IsControl(c) && !invalid.Contains(c)));
    }

    /// <summary>
    /// The decoded path of an absolute URL, without the query: <c>https://x/a%20b.cbl?dl=1</c> gives <c>/a b.cbl</c>.
    /// Empty when the string is not an absolute URL
    /// </summary>
    public static string UrlFilePath(this string? url)
    {
        if (!Uri.TryCreate(url, UriKind.Absolute, out var uri)) return string.Empty;

        return Uri.UnescapeDataString(uri.AbsolutePath);
    }

    // A Linux root "/" trims to "", which makes "/" the prefix every absolute path starts with
    private static string TrimmedPath(string path) => path.NormalizePath().TrimEnd('/');
}
