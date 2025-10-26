using System;
using System.Linq;
using System.Threading.Tasks;
using Flurl.Http;
using Kavita.Common;
using Kavita.Common.EnvironmentInfo;
using Microsoft.AspNetCore.StaticFiles;

namespace API.Extensions;
#nullable enable

public static class FlurlExtensions
{

    private static readonly FileExtensionContentTypeProvider FileTypeProvider = new ();

    /// <summary>
    /// Makes a head request to the url, and parses the first content type header to determine the content type
    /// </summary>
    /// <param name="url"></param>
    /// <returns></returns>
    public static async Task<string?> GetFileFormatAsync(this string url)
    {
        var headResponse = await url.AllowHttpStatus("2xx").HeadAsync();

        // TODO: Move to new Headers class after merge with progress branch
        var contentTypeHeader = headResponse.Headers.FirstOrDefault("Content-Type");
        if (string.IsNullOrEmpty(contentTypeHeader))
        {
            return null;
        }

        var contentType = contentTypeHeader.Split(";").FirstOrDefault();
        if (string.IsNullOrEmpty(contentType))
        {
            return null;
        }

        // The mappings have legacy mappings like .jpe => image/jpeg. We reverse to get the newer stuff first
        return FileTypeProvider.Mappings
            .Reverse()
            .FirstOrDefault(m => m.Value.Equals(contentType, StringComparison.OrdinalIgnoreCase))
            .Key?.TrimStart('.');
    }

    public static IFlurlRequest WithKavitaPlusHeaders(this string request, string license, string? anilistToken = null)
    {
        return request
            .WithHeader("Accept", "application/json")
            .WithHeader("User-Agent", "Kavita")
            .WithHeader("x-license-key", license)
            .WithHeader("x-installId", HashUtil.ServerToken())
            .WithHeader("x-anilist-token", anilistToken ?? string.Empty)
            .WithHeader("x-kavita-version", BuildInfo.Version)
            .WithHeader("Content-Type", "application/json")
            .WithTimeout(TimeSpan.FromSeconds(Configuration.DefaultTimeOutSecs));
    }

    public static IFlurlRequest WithBasicHeaders(this string request, string apiKey)
    {
        return request
            .WithHeader("Accept", "application/json")
            .WithHeader("User-Agent", "Kavita")
            .WithHeader("x-api-key", apiKey)
            .WithHeader("x-installId", HashUtil.ServerToken())
            .WithHeader("x-kavita-version", BuildInfo.Version)
            .WithHeader("Content-Type", "application/json")
            .WithTimeout(TimeSpan.FromSeconds(Configuration.DefaultTimeOutSecs));
    }
}
