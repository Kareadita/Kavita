using System;
using API.Constants;
using System.Linq;
using System.Threading.Tasks;
using Flurl.Http;
using Kavita.Common;
using Kavita.Common.EnvironmentInfo;
using Microsoft.Net.Http.Headers;
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
        IFlurlResponse? headResponse;
        try
        {
            headResponse = await url.AllowHttpStatus("2xx").HeadAsync();
        }
        catch
        {
            /* Ignore exceptions */
            return null;
        }

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
            .WithHeader(HeaderNames.Accept, "application/json")
            .WithHeader(HeaderNames.UserAgent, "Kavita")
            .WithHeader(Headers.LicenseKey, license)
            .WithHeader(Headers.InstallId, HashUtil.ServerToken())
            .WithHeader(Headers.AnilistToken, anilistToken ?? string.Empty)
            .WithHeader(Headers.KavitaVersion, BuildInfo.Version)
            .WithHeader(HeaderNames.ContentType, "application/json")
            .WithTimeout(TimeSpan.FromSeconds(Configuration.DefaultTimeOutSecs));
    }

    public static IFlurlRequest WithBasicHeaders(this string request, string apiKey)
    {
        return request
            .WithHeader(HeaderNames.Accept, "application/json")
            .WithHeader(HeaderNames.UserAgent, "Kavita")
            .WithHeader(Headers.ApiKey, apiKey)
            .WithHeader(Headers.InstallId, HashUtil.ServerToken())
            .WithHeader(Headers.KavitaVersion, BuildInfo.Version)
            .WithHeader(HeaderNames.ContentType, "application/json")
            .WithTimeout(TimeSpan.FromSeconds(Configuration.DefaultTimeOutSecs));
    }
}
