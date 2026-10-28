using System;
using System.Diagnostics.CodeAnalysis;

namespace Kavita.Models.Misc;
#nullable enable

public sealed record FileCacheResult<T>
{
    /// <summary>
    /// When Unavailable, whatever the fetch returned (the cache rejected it), or default if it threw
    /// </summary>
    public T? Value { get; init; }
    public CacheSource Source { get; init; }
    /// <summary>
    /// When the cache file holding Value was written, null when Value was not cached
    /// </summary>
    public DateTime? CachedAtUtc { get; init; }

    [MemberNotNullWhen(true, nameof(Value))]
    public bool HasValue => Value is not null;
}
