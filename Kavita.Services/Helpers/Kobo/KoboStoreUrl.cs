using System;
using System.Linq;
using Microsoft.AspNetCore.Http;

namespace Kavita.Services.Helpers.Kobo;

public static class KoboStoreUrl
{
    private const string StoreHost = "storeapi.kobo.com";
    private const string ApiKeyQueryName = "apiKey";
    private static readonly Uri StoreBaseAddress = new($"https://{StoreHost}");

    public static Uri Build(PathString path, QueryString query, Uri? baseAddress = null)
    {
        var pathValue = path.ToUriComponent();
        var segments = pathValue.Split('/', 4);

        // Skips by segment, not key length: the authenticating key can arrive by header or query and differ from the route's
        var isKoboPath = segments.Length > 1 && segments[1].Equals("kobo", StringComparison.OrdinalIgnoreCase);
        var rest = isKoboPath
            ? (segments.Length == 4 ? segments[3].TrimStart('/') : string.Empty)
            : pathValue.TrimStart('/');

        return new UriBuilder(baseAddress ?? StoreBaseAddress)
        {
            Path = rest,
            Query = WithoutApiKey(query)
        }.Uri;
    }

    private static string WithoutApiKey(QueryString query)
    {
        var parts = (query.Value ?? string.Empty).TrimStart('?')
            .Split('&', StringSplitOptions.RemoveEmptyEntries)
            .Where(part => !IsApiKey(part));

        return string.Join('&', parts);
    }

    private static bool IsApiKey(string queryPart)
    {
        var name = queryPart.Split('=', 2)[0];

        return Uri.UnescapeDataString(name).Equals(ApiKeyQueryName, StringComparison.OrdinalIgnoreCase);
    }
}
