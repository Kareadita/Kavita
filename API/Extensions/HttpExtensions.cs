using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net.NetworkInformation;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using API.Helpers;
using Microsoft.AspNetCore.Http;
using Microsoft.Net.Http.Headers;

namespace API.Extensions;
#nullable enable

public static class HttpExtensions
{
    public static void AddPaginationHeader(this HttpResponse response, int currentPage,
        int itemsPerPage, int totalItems, int totalPages)
    {
        var paginationHeader = new PaginationHeader(currentPage, itemsPerPage, totalItems, totalPages);
        var options = new JsonSerializerOptions()
        {
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase
        };

        response.Headers.Append("Pagination", JsonSerializer.Serialize(paginationHeader, options));
        response.Headers.Append("Access-Control-Expose-Headers", "Pagination");
    }

    /// <summary>
    /// Calculates SHA256 hash for a byte[] and sets as ETag. Ensures Cache-Control: private header is added.
    /// </summary>
    /// <param name="response"></param>
    /// <param name="content">If byte[] is null or empty, will only add cache-control</param>
    public static void AddCacheHeader(this HttpResponse response, byte[] content)
    {
        if (content is not {Length: > 0}) return;
        response.Headers.Append(HeaderNames.ETag, string.Concat(SHA256.HashData(content).Select(x => x.ToString("X2"))));
        response.Headers.CacheControl =  $"private,max-age=100";
    }

    /// <summary>
    /// Calculates SHA256 hash for a cover image filename and sets as ETag. Ensures Cache-Control: private header is added.
    /// </summary>
    /// <param name="response"></param>
    /// <param name="filename"></param>
    /// <param name="maxAge">Maximum amount of seconds to set for Cache-Control</param>
    public static void AddCacheHeader(this HttpResponse response, string filename, int maxAge = 10)
    {
        if (filename is not {Length: > 0}) return;
        var hashContent = filename + File.GetLastWriteTimeUtc(filename);
        response.Headers.Append("ETag", string.Concat(SHA256.HashData(Encoding.UTF8.GetBytes(hashContent)).Select(x => x.ToString("X2"))));
        if (maxAge != 10)
        {
            response.Headers.CacheControl =  $"max-age={maxAge}";
        }
    }

    /// <summary>
    /// Retrieves the supported image types extensions from the Accept header of the HTTP request.
    /// </summary>
    /// <param name="request">The HTTP request.</param>
    /// <returns>A list of supported image types extensions.</returns>
    public static List<string> SupportedImageTypesFromRequest(this HttpRequest request)
    {
        var acceptHeader = request.Headers["Accept"];
        string[] spl1 = acceptHeader.ToString().Split(';');
        acceptHeader = spl1[0];
        string[] split = acceptHeader.ToString().Split(',');
        List<string> defaultExtensions = new List<string>();
        defaultExtensions.Add("jpeg");
        defaultExtensions.Add("jpg");
        defaultExtensions.Add("png");
        defaultExtensions.Add("gif");
        defaultExtensions.Add("webp");
        foreach (string v in split)
        {
            if (v.StartsWith("image/", StringComparison.InvariantCultureIgnoreCase))
            {
                string n = v.Substring(6).ToLowerInvariant();
                if (n == "svg+xml")
                    n = "svg";
                if (n == "jp2")
                    defaultExtensions.Add("j2k");
                if (n == "j2k")
                    defaultExtensions.Add("jp2");
                if (n == "heif")
                    defaultExtensions.Add("heic");
                if (n == "heic")
                    defaultExtensions.Add("heif");
                if (n.StartsWith("*"))
                    continue;
                if (!defaultExtensions.Contains(n))
                    defaultExtensions.Add(n);
            }
        }
        return defaultExtensions;
    }
}
