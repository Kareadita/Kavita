using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Globalization;
using System.Linq;

namespace Kavita.Common.Helpers;
#nullable enable

/// <summary>
/// Handles all things parsing of External Ids (weblinks, not set checks, anilist:X)
/// </summary>
public static class ExternalIdParser
{
    private const string AniListWeblinkWebsite = "https://anilist.co/manga/";
    private const string MalWeblinkWebsite = "https://myanimelist.net/manga/";
    private const string MalStaffWebsite = "https://myanimelist.net/people/";
    private const string MalCharacterWebsite = "https://myanimelist.net/character/";
    private const string GoogleBooksWeblinkWebsite = "https://books.google.com/books?id=";
    private const string MangaDexWeblinkWebsite = "https://mangadex.org/title/";
    private const string AniListStaffWebsite = "https://anilist.co/staff/";
    private const string AniListCharacterWebsite = "https://anilist.co/character/";
    private const string HardcoverStaffWebsite = "https://hardcover.app/authors/";
    private const string HardcoverSeriesWebsite = "https://hardcover.app/series/id/";
    private const string HardcoverBookWebsite = "https://hardcover.app/book/id/";
    private const string MangaBakaWebsite = "https://mangabaka.org/";


    /// <summary>
    /// The 4050 implies this is a Series (TPB/Series) and 4000 implies single issue
    /// </summary>
    /// <remarks>
    /// ComicVine has a unique structure:
    /// <c>https://comicvine.gamespot.com/batman-the-caped-crusader/4050-112794/</c> (Series)
    /// <c>https://comicvine.gamespot.com/batman-the-caped-crusader-6-volume-6/4000-907546/</c> (Issue)
    /// </remarks>
    private const string ComicVineWeblinkWebsite = "https://comicvine.gamespot.com/";

    private static readonly Dictionary<string, int> WeblinkExtractionMap = new()
    {
        {AniListWeblinkWebsite, 0},
        {MalWeblinkWebsite, 0},
        {GoogleBooksWeblinkWebsite, 0},
        {MangaDexWeblinkWebsite, 0},
        {AniListStaffWebsite, 0},
        {AniListCharacterWebsite, 0},
        {ComicVineWeblinkWebsite, 1},
        {HardcoverSeriesWebsite, 0},
        {HardcoverBookWebsite, 0},
        {MangaBakaWebsite, 0},
    };

    public static long? GetMalId(string? weblinks)
    {
        return ExtractId<long?>(weblinks, MalWeblinkWebsite);
    }

    /// <summary>
    /// Attempts to parse ComicVine Id from the weblinks. Returns id and true if Series/Volume Id.
    /// </summary>
    /// <param name="weblinks"></param>
    /// <returns></returns>
    public static Tuple<string?, bool> GetComicVineId(string? weblinks)
    {
        var extractedId = ExtractId<string?>(weblinks, ComicVineWeblinkWebsite);
        if (string.IsNullOrEmpty(extractedId)) return Tuple.Create<string?, bool>(null, false);

        return Tuple.Create<string?, bool>(extractedId.Split('-')[1], extractedId.StartsWith("4050"));
    }

    public static int? GetAniListId(string? weblinks)
    {
        return ExtractId<int?>(weblinks, AniListWeblinkWebsite);
    }

    public static int GetAniListCharacterId(string? url)
    {
        return ExtractId<int?>(url, AniListCharacterWebsite) ?? 0;
    }

    public static int GetAniListStaffId(string? url)
    {
        return ExtractId<int?>(url, AniListStaffWebsite) ?? 0;
    }

    public static string? GetGoogleBooksId(string? weblinks)
    {
        return ExtractId<string?>(weblinks, GoogleBooksWeblinkWebsite);
    }

    public static string? GetMangaDexId(string? weblinks)
    {
        return ExtractId<string?>(weblinks, MangaDexWeblinkWebsite);
    }

    public static long GetMangaBakaId(string? weblinks)
    {
        return ExtractId<long?>(weblinks, MangaBakaWebsite) ?? 0;
    }

    #region Header-based Parsing
    public static bool TryParseAniListHeader(string? text, out int id) =>
        TryParseHeader(text, "ANILIST", out id);

    public static bool TryParseHardcoverHeader(string? text, out string id) =>
        TryParseHeader(text, "HARDCOVER", out id);

    public static bool TryParseMangaBakaHeader(string? text, out long id) =>
        TryParseHeader(text, "MANGABAKA", out id);

    public static bool TryParseMalHeader(string? text, out int id) =>
        TryParseHeader(text, "MAL", out id);

    public static int? ParseAniListHeader(string? text) => ParseHeader<int>(text, "ANILIST");

    public static string? ParseHardcoverHeader(string? text) => ParseHeader<string>(text, "HARDCOVER");

    public static long? ParseMangaBakaHeader(string? text) => ParseHeader<long>(text, "MANGABAKA");

    public static int? ParseMalHeader(string? text) => ParseHeader<int>(text, "MAL");

    private static T? ParseHeader<T>(string? text, string header)
        where T : IParsable<T>
    {
        if (string.IsNullOrWhiteSpace(text)) return default;
        if (!text.StartsWith(header + ":", StringComparison.InvariantCultureIgnoreCase)) return default;
        var valuePart = text.Split(':', 2)[1];

        return T.TryParse(valuePart, CultureInfo.InvariantCulture, out var result) ? result : default;
    }

    private static bool TryParseHeader<T>(string? text, string header, out T id)
        where T : IParsable<T>
    {
        var result = ParseHeader<T>(text, header);
        if (result is not null)
        {
            id = result;
            return true;
        }
        id = default!;
        return false;
    }

    #endregion
    public static int GetHardcoverSeriesId(string? weblinks)
    {
        return ExtractId<int?>(weblinks, HardcoverSeriesWebsite) ?? 0;
    }

    public static int GetHardcoverBookId(string? weblinks)
    {
        return ExtractId<int?>(weblinks, HardcoverBookWebsite) ?? 0;
    }

    /// <summary>
    /// Extract an ID from a given weblink
    /// </summary>
    /// <param name="webLinks"></param>
    /// <param name="website"></param>
    /// <returns></returns>
    private static T? ExtractId<T>(string? webLinks, string website)
    {
        if (string.IsNullOrEmpty(webLinks)) return default;

        var index = WeblinkExtractionMap[website];
        foreach (var webLink in webLinks.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
        {
            if (!webLink.StartsWith(website)) continue;

            var tokens = webLink.Split(website)[1].Split('/');
            var value = tokens[index];

            if (typeof(T) == typeof(int?))
            {
                if (int.TryParse(value, CultureInfo.InvariantCulture, out var intValue)) return (T)(object)intValue;
            }
            else if (typeof(T) == typeof(int))
            {
                if (int.TryParse(value, CultureInfo.InvariantCulture, out var intValue)) return (T)(object)intValue;

                return default;
            }
            else if (typeof(T) == typeof(long?))
            {
                if (long.TryParse(value, CultureInfo.InvariantCulture, out var longValue)) return (T)(object)longValue;
            }
            else if (typeof(T) == typeof(string))
            {
                return (T)(object)value;
            }
        }

        return default;
    }


    /// <summary>
    /// Generate a URL from a given ID and website
    /// </summary>
    /// <typeparam name="T">Type of the ID (e.g., int, long, string)</typeparam>
    /// <param name="id">The ID to embed in the URL</param>
    /// <param name="website">The base website URL</param>
    /// <returns>The generated URL or null if the website is not supported</returns>
    public static string? GenerateUrl<T>(T id, string website)
    {
        if (!WeblinkExtractionMap.ContainsKey(website))
        {
            return null; // Unsupported website
        }

        if (Equals(id, default(T)))
        {
            throw new ArgumentNullException(nameof(id), "ID cannot be null.");
        }

        // Ensure the type of the ID matches supported types
        if (typeof(T) == typeof(int) || typeof(T) == typeof(long) || typeof(T) == typeof(string))
        {
            return $"{website}{id}";
        }

        throw new ArgumentException("Unsupported ID type. Supported types are int, long, and string.", nameof(id));
    }
}
