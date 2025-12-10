using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Caching.Hybrid;

namespace API.Tests;

public class FakeHybridCache : HybridCache
{
    private readonly Dictionary<string, object?> _cache = new();
    private readonly Dictionary<string, HashSet<string>> _tagToKeys = new();
    private readonly Dictionary<string, HashSet<string>> _keyToTags = new();

    public override async ValueTask<T> GetOrCreateAsync<TState, T>(
        string key,
        TState state,
        Func<TState, CancellationToken, ValueTask<T>> factory,
        HybridCacheEntryOptions? options = null,
        IEnumerable<string>? tags = null,
        CancellationToken cancellationToken = default)
    {
        if (_cache.TryGetValue(key, out var value))
            return (T)value!;

        var result = await factory(state, cancellationToken);
        _cache[key] = result;

        // Track tags if provided
        if (tags != null)
        {
            foreach (var tag in tags)
            {
                if (!_tagToKeys.ContainsKey(tag))
                    _tagToKeys[tag] = [];
                _tagToKeys[tag].Add(key);

                if (!_keyToTags.ContainsKey(key))
                    _keyToTags[key] = [];
                _keyToTags[key].Add(tag);
            }
        }

        return result;
    }

    public override ValueTask SetAsync<T>(
        string key,
        T value,
        HybridCacheEntryOptions? options = null,
        IEnumerable<string>? tags = null,
        CancellationToken cancellationToken = default)
    {
        _cache[key] = value;

        // Track tags if provided
        if (tags != null)
        {
            foreach (var tag in tags)
            {
                if (!_tagToKeys.ContainsKey(tag))
                    _tagToKeys[tag] = new HashSet<string>();
                _tagToKeys[tag].Add(key);

                if (!_keyToTags.ContainsKey(key))
                    _keyToTags[key] = new HashSet<string>();
                _keyToTags[key].Add(tag);
            }
        }

        return ValueTask.CompletedTask;
    }

    public override ValueTask RemoveAsync(
        string key,
        CancellationToken cancellationToken = default)
    {
        _cache.Remove(key);

        // Clean up tag mappings
        if (_keyToTags.TryGetValue(key, out var tags))
        {
            foreach (var tag in tags)
            {
                if (_tagToKeys.TryGetValue(tag, out var keys))
                {
                    keys.Remove(key);
                    if (keys.Count == 0)
                        _tagToKeys.Remove(tag);
                }
            }
            _keyToTags.Remove(key);
        }

        return ValueTask.CompletedTask;
    }

    public override ValueTask RemoveByTagAsync(
        string tag,
        CancellationToken cancellationToken = default)
    {
        // Handle wildcard - remove all cache entries
        if (tag == "*")
        {
            _cache.Clear();
            _tagToKeys.Clear();
            _keyToTags.Clear();
            return ValueTask.CompletedTask;
        }

        // Remove all keys associated with this tag
        if (_tagToKeys.TryGetValue(tag, out var keys))
        {
            var keysToRemove = keys.ToList(); // Copy to avoid modification during iteration
            foreach (var key in keysToRemove)
            {
                _cache.Remove(key);

                // Clean up key's tag references
                if (_keyToTags.TryGetValue(key, out var keyTags))
                {
                    keyTags.Remove(tag);
                    if (keyTags.Count == 0)
                        _keyToTags.Remove(key);
                }
            }

            _tagToKeys.Remove(tag);
        }

        return ValueTask.CompletedTask;
    }

    // Helper methods for testing
    public void Seed<T>(string key, T value, params string[] tags)
    {
        _cache[key] = value;

        if (tags?.Length > 0)
        {
            foreach (var tag in tags)
            {
                if (!_tagToKeys.ContainsKey(tag))
                    _tagToKeys[tag] = new HashSet<string>();
                _tagToKeys[tag].Add(key);

                if (!_keyToTags.ContainsKey(key))
                    _keyToTags[key] = new HashSet<string>();
                _keyToTags[key].Add(tag);
            }
        }
    }

    public void Clear()
    {
        _cache.Clear();
        _tagToKeys.Clear();
        _keyToTags.Clear();
    }

    public bool ContainsKey(string key) => _cache.ContainsKey(key);

    public int Count => _cache.Count;

    public IEnumerable<string> GetKeysForTag(string tag) =>
        _tagToKeys.TryGetValue(tag, out var keys) ? keys : Enumerable.Empty<string>();
}
