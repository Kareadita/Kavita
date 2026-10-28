using System;
using System.Diagnostics.CodeAnalysis;
using Kavita.Models.DTOs.Misc;

namespace Kavita.Models.Misc;
#nullable enable

public sealed record GithubResult<T>
{
    public T? Value { get; init; }
    public CacheSource Source { get; init; }
    /// <summary>
    /// When Value was fetched from GitHub, null when Unavailable
    /// </summary>
    public DateTime? FetchedAtUtc { get; init; }
    /// <summary>
    /// Set while GitHub calls are paused after a rate limit or failure
    /// </summary>
    public DateTime? RetryAtUtc { get; init; }
    /// <summary>
    /// From the response headers when Source is Live, otherwise empty
    /// </summary>
    public GithubRateLimitDto RateLimit { get; init; } = new();

    [MemberNotNullWhen(true, nameof(Value))]
    public bool HasValue => Value is not null;
}
