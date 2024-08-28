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

    private static void AddExtension(List<string> extensions, string extension)
    {
        if (string.IsNullOrEmpty(extension))
            return;
        if (!extensions.Contains(extension))
            extensions.Add(extension);
    }

    /// <summary>
    /// Retrieves the supported image types extensions from the Accept header of the HTTP request.
    /// </summary>
    /// <param name="request">The HTTP request.</param>
    /// <returns>A list of supported image types extensions.</returns>
    public static List<string> SupportedImageTypesFromRequest(this HttpRequest request)
    {
        var acceptHeader = request.Headers["Accept"].ToString();
        string[] spl1 = acceptHeader.Split(';');
        acceptHeader = spl1[0];
        string[] split = acceptHeader.Split(',');
        List<string> supportedExtensions = new List<string>();

        //Add default extensions supported by all browsers.
        supportedExtensions.Add("jpeg");
        supportedExtensions.Add("jpg");
        supportedExtensions.Add("png");
        supportedExtensions.Add("gif");
        supportedExtensions.Add("webp");
        //Browser add specific image mime types, when the image type is not a global standard.
        //Let's reuse that to identify the additional image types supported by the browser.
        foreach (string v in split)
        {
            if (v.StartsWith("image/", StringComparison.InvariantCultureIgnoreCase))
            {
                string n = v.Substring(6).ToLowerInvariant();
                if (n.StartsWith("*"))
                    continue;
                if (n == "svg+xml")
                    n = "svg";
                if (n == "jp2")
                    AddExtension(supportedExtensions, "j2k");
                if (n == "j2k")
                    AddExtension(supportedExtensions, "jp2");
                if (n == "heif")
                    AddExtension(supportedExtensions, "heic");
                if (n == "heic")
                    AddExtension(supportedExtensions, "heif");
                AddExtension(supportedExtensions, n);
            }
        }
        return supportedExtensions;
    }
}
