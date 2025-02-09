using System;
using Flurl.Http;
using Kavita.Common;
using Kavita.Common.EnvironmentInfo;

namespace API.Extensions;

public static class FlurlExtensions
{
    public static IFlurlRequest WithKavitaPlusHeaders(this string request, string license)
    {
        return request
            .WithHeader("Accept", "application/json")
            .WithHeader("User-Agent", "Kavita")
            .WithHeader("x-license-key", license)
            .WithHeader("x-installId", HashUtil.ServerToken())
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
