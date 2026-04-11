using System;
using System.Collections.Generic;
using System.Linq;
using Kavita.Models.DTOs.ReadingLists.CBL.Internal;

namespace Kavita.Models.Extensions;
#nullable enable

public static class CblExternalDbProviderExtensions
{
    private static readonly Dictionary<CblExternalDbProvider, string[]> ProviderNames = new()
    {
        [CblExternalDbProvider.ComicVine] = ["cv", "comicvine"],
        [CblExternalDbProvider.Metron] = ["metron"],
        [CblExternalDbProvider.GrandComicsDatabase] = ["gcd", "grandcomicsdatabase"],
        [CblExternalDbProvider.Kavita] = ["kavita"],
        [CblExternalDbProvider.AniList] = ["anilist"],
        [CblExternalDbProvider.Mal] = ["mal"],
        [CblExternalDbProvider.Hardcover] = ["hardcover"],
        [CblExternalDbProvider.Unknown] = ["unknown"],
    };

    private static readonly Lazy<Dictionary<string, CblExternalDbProvider>> NameToProvider = new(() =>
        ProviderNames
            .SelectMany(kvp => kvp.Value.Select(name => (name, kvp.Key)))
            .ToDictionary(x => x.name, x => x.Key, StringComparer.OrdinalIgnoreCase));

    public static CblExternalDbProvider FromName(string? name)
    {
        if (string.IsNullOrEmpty(name)) return CblExternalDbProvider.Unknown;
        return NameToProvider.Value.GetValueOrDefault(name, CblExternalDbProvider.Unknown);
    }

    /// <summary>
    /// Returns the Name for the CBL Import/Export (Short-name)
    /// </summary>
    /// <param name="provider"></param>
    /// <returns></returns>
    /// <exception cref="ArgumentOutOfRangeException"></exception>
    public static string ToShortName(this CblExternalDbProvider provider)
    {
        return ProviderNames.TryGetValue(provider, out var names)
            ? names[0]
            : throw new ArgumentOutOfRangeException(nameof(provider));
    }
}
