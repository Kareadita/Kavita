using System;
using Flurl.Http;
using Kavita.Models.DTOs.Misc;

namespace Kavita.Services.Extensions;

public static class FlurlGithubExtensions
{
    public static IFlurlRequest WithGithubHeaders(this string url)
    {
        return url
            .WithHeader("User-Agent", "Kavita")
            .WithHeader("Accept", "application/vnd.github.v3+json");
    }

    /// <summary>
    /// Extracts GitHub rate limit info from response headers.
    /// </summary>
    public static GithubRateLimitDto GetRateLimit(this IFlurlResponse response)
    {
        var remaining = response.Headers.FirstOrDefault("X-RateLimit-Remaining");
        var limit = response.Headers.FirstOrDefault("X-RateLimit-Limit");
        var resetEpoch = response.Headers.FirstOrDefault("X-RateLimit-Reset");

        DateTime? resetsAt = null;
        if (long.TryParse(resetEpoch, out var epoch))
        {
            resetsAt = DateTimeOffset.FromUnixTimeSeconds(epoch).UtcDateTime;
        }

        return new GithubRateLimitDto
        {
            Remaining = int.TryParse(remaining, out var r) ? r : null,
            Limit = int.TryParse(limit, out var l) ? l : null,
            ResetsAtUtc = resetsAt
        };
    }
}
