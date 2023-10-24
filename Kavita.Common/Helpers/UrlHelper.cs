namespace Kavita.Common.Helpers;

#nullable enable
public static class UrlHelper
{
    public static bool StartsWithHttpOrHttps(string? url)
    {
        if (string.IsNullOrEmpty(url)) return false;
        return url.StartsWith("http://") || url.StartsWith("https://");
    }

    public static string? EnsureStartsWithHttpOrHttps(string? url)
    {
        if (string.IsNullOrEmpty(url)) return url;
        if (!url.StartsWith("http://") && !url.StartsWith("https://"))
        {
            // URL doesn't start with "http://" or "https://", so add "http://"
            return "http://" + url;
        }

        return url;
    }

    public static string? EnsureEndsWithSlash(string? url)
    {
        if (string.IsNullOrEmpty(url)) return url;

        return !url.EndsWith('/')
            ? $"{url}/"
            : url;

    }

    public static string? EnsureStartsWithSlash(string? url)
    {
        if (string.IsNullOrEmpty(url)) return url;
        return !url.StartsWith('/')
            ? $"/{url}"
            : url;
    }

    public static string? RemoveEndingSlash(string? url)
    {
        if (string.IsNullOrEmpty(url)) return url;
        if (url.EndsWith('/')) return url.Substring(0, url.Length - 1);
        return url;
    }
}
