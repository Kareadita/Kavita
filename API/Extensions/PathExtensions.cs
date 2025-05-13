using System;
using System.Globalization;
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

    public static ReadOnlySpan<char> GetFirstSegmentSpan(this string urlKey)
    {
        var idx = urlKey.IndexOf('/');
        return idx < 0 ? urlKey.AsSpan() : urlKey.AsSpan()[..idx];
    }

    public static ReadOnlySpan<char> GetLastSegmentSpan(this string urlKey)
    {
        var idx = urlKey.LastIndexOf('/');
        return idx < 0 ? urlKey.AsSpan() : urlKey.AsSpan().Slice(idx + 1);
    }

    public static int? ParseInt(this ReadOnlySpan<char> s)
    {
        if (int.TryParse(s, NumberStyles.Any, CultureInfo.InvariantCulture, out var value))
            return value;
        return default;
    }
}
