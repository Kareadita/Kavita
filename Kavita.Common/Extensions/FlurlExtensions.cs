using System;
using Flurl.Http;
using Kavita.Common.Constants;
using Kavita.Common.EnvironmentInfo;
using Microsoft.Net.Http.Headers;

namespace Kavita.Common.Extensions;

public static class FlurlExtensions
{
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
