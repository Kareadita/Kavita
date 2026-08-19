using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;

namespace Kavita.Common.Helpers;
#nullable enable

/// <summary>
/// A slug parsed out of a public hardcover.app url, and if that url pointed at a single book rather than a series
/// </summary>
public sealed record HardcoverUrlSlug(string Slug, bool IsStandAlone);

/// <summary>
/// Handles all things parsing of External Ids (weblinks, not set checks, anilist:X)
/// </summary>
public static class ExternalIdParser
{
    private const string AniListWeblinkWebsite = "https://anilist.co/manga/";
    private const string MalWeblinkWebsite = "https://myanimelist.net/manga/";
    private const string GoogleBooksWeblinkWebsite = "https://books.google.com/books?id=";
    private const string MangaDexWeblinkWebsite = "https://mangadex.org/title/";
    private const string AniListStaffWebsite = "https://anilist.co/staff/";
    private const string AniListCharacterWebsite = "https://anilist.co/character/";
    private const string HardcoverStaffWebsite = "https://hardcover.app/id/authors/";
    private const string HardcoverSeriesWebsite = "https://hardcover.app/id/series/";
    private const string HardcoverBookWebsite = "https://hardcover.app/id/book/";
    private const string MangaBakaWebsite = "https://mangabaka.org/";

    /// <summary>
    /// Hardcover's public, slug-based URLs (as pasted by a user), distinct from the internal numeric-id
    /// <see cref="HardcoverSeriesWebsite"/>/<see cref="HardcoverBookWebsite"/> links Kavita generates itself.
    /// The value is if the url points at a single book, rather than a series
    /// </summary>
    private static readonly Dictionary<string, bool> HardcoverPublicWebsites = new()
    {
        {"https://hardcover.app/books/", true},
        {"https://hardcover.app/series/", false},
    };


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
        {HardcoverStaffWebsite, 0},
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

    public static int GetMangaBakaId(string? weblinks)
    {
        return ExtractId<int?>(weblinks, MangaBakaWebsite) ?? 0;
    }

    #region Header-based Parsing
    public static bool TryParseAniListHeader(string? text, out int id) =>
        TryParseHeader(text, "ANILIST", out id) || TryParseHeader(text, "AL", out id);

    public static bool TryParseHardcoverHeader(string? text, out string id) =>
        TryParseHeader(text, "HARDCOVER", out id);

    public static bool TryParseMangaBakaHeader(string? text, out long id) =>
        TryParseHeader(text, "MANGABAKA", out id) || TryParseHeader(text, "MB", out id);

    public static bool TryParseMalHeader(string? text, out int id) =>
        TryParseHeader(text, "MAL", out id);

    public static int? ParseAniListHeader(string? text) =>
        TryParseHeader<int>(text, "ANILIST", out var id) ? id : null;

    public static string? ParseHardcoverHeader(string? text) =>
        TryParseHeader<string>(text, "HARDCOVER", out var id) ? id : null;

    public static long? ParseMangaBakaHeader(string? text) =>
        TryParseHeader<long>(text, "MANGABAKA", out var id) ? id : null;

    public static int? ParseMalHeader(string? text) =>
        TryParseHeader<int>(text, "MAL", out var id) ? id : null;

    private static bool TryParseHeader<T>(string? text, string header, out T id)
        where T : IParsable<T>
    {
        id = default!;
        if (string.IsNullOrWhiteSpace(text)) return false;
        if (!text.StartsWith(header + ":", StringComparison.InvariantCultureIgnoreCase)) return false;

        var valuePart = text.Split(':', 2)[1];
        return T.TryParse(valuePart, CultureInfo.InvariantCulture, out id!);
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
    /// Extracts the slug from a public hardcover.app book/series URL (e.g. https://hardcover.app/books/{slug}),
    /// along with if the url pointed at a single book or at a series
    /// </summary>
    /// <remarks>Returns null for the numeric-id links Kavita generates itself, as those carry an id and not a slug</remarks>
    public static HardcoverUrlSlug? GetHardcoverSlugFromUrl(string? text)
    {
        if (string.IsNullOrWhiteSpace(text)) return null;

        var trimmed = text.Trim();

        var website = HardcoverPublicWebsites.Keys.FirstOrDefault(w => trimmed.StartsWith(w, StringComparison.OrdinalIgnoreCase));
        if (website == null) return null;

        var slug = trimmed[website.Length..].Split('/', '?', '#')[0];
        if (string.IsNullOrEmpty(slug)) return null;

        // The legacy https://hardcover.app/series/id/{id} links Kavita used to generate would otherwise be read as
        // the slug "id". The current HardcoverSeriesWebsite layout doesn't start with a public url
        if (slug.Equals("id", StringComparison.OrdinalIgnoreCase)) return null;

        return new HardcoverUrlSlug(slug, HardcoverPublicWebsites[website]);
    }

    public static string GetHardcoverStaffId(string? url)
    {
        try
        {
            return ExtractId<string?>(url, HardcoverStaffWebsite) ?? string.Empty;
        }
        catch (Exception)
        {
            return string.Empty;
        }
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
