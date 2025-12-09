using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Caching.Hybrid;

namespace API.Tests;

public class FakeHybridCacheWithTracking : FakeHybridCache
{
    public IList<(string Key, object State, CancellationToken CancellationToken)> GetOrCreateAsyncCalls { get; } = [];
    public IList<(string Key, CancellationToken CancellationToken)> SetAsyncCalls { get; } = [];

    public override async ValueTask<T> GetOrCreateAsync<TState, T>(
        string key,
        TState state,
        Func<TState, CancellationToken, ValueTask<T>> factory,
        HybridCacheEntryOptions? options = null,
        IEnumerable<string>? tags = null,
        CancellationToken cancellationToken = default)
    {
        GetOrCreateAsyncCalls.Add((key, state!, cancellationToken));
        return await base.GetOrCreateAsync(key, state, factory, options, tags, cancellationToken);
    }

    public override ValueTask SetAsync<T>(
        string key,
        T value,
        HybridCacheEntryOptions? options = null,
        IEnumerable<string>? tags = null,
        CancellationToken cancellationToken = default)
    {
        SetAsyncCalls.Add((key, cancellationToken));
        return base.SetAsync(key, value, options, tags, cancellationToken);
    }
}
