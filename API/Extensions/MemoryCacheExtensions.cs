using System.Reflection;
using Microsoft.Extensions.Caching.Memory;

namespace API.Extensions;

public static class MemoryCacheExtensions
{
    public static void RemoveByPrefix(this IMemoryCache memoryCache, string prefix)
    {
        if (memoryCache is not MemoryCache concreteMemoryCache) return;

        var cacheEntriesCollectionInfo = typeof(MemoryCache)
            .GetProperty("EntriesCollection", BindingFlags.NonPublic | BindingFlags.Instance);

        var cacheEntriesCollection = cacheEntriesCollectionInfo?.GetValue(concreteMemoryCache) as dynamic;

        if (cacheEntriesCollection == null) return;
        foreach (var cacheItem in cacheEntriesCollection)
        {
            // Check if the cache key starts with the given prefix
            if (cacheItem.GetType().GetProperty("Key").GetValue(cacheItem) is string cacheItemKey && cacheItemKey.StartsWith(prefix))
            {
                concreteMemoryCache.Remove(cacheItemKey);
            }
        }
    }

    public static void Clear(this IMemoryCache memoryCache)
    {
        if (memoryCache is MemoryCache concreteMemoryCache)
        {
            concreteMemoryCache.Clear();
        }
    }
}
