using System;
using System.Collections.Generic;
using System.Globalization;
using System.Threading;
using System.Threading.Tasks;
using Hangfire;
using Kavita.Models.DTOs.Scrobbling;
using Kavita.Models.Entities;
using Kavita.Models.Entities.Enums;

namespace Kavita.API.Services.Plus;

public interface IScrobblingService
{
    /// <summary>
    /// An automated job that will run against all user's tokens and validate if they are still active
    /// </summary>
    /// <param name="ct"></param>
    /// <remarks>This service can validate without license check as the task which calls will be guarded</remarks>
    /// <returns></returns>
    Task CheckExternalAccessTokens(CancellationToken ct = default);

    /// <summary>
    /// Checks if the token has expired with <see cref="TokenService.HasTokenExpired"/>, if it has double checks with K+,
    /// otherwise return false.
    /// </summary>
    /// <param name="userId"></param>
    /// <param name="provider"></param>
    /// <param name="ct"></param>
    /// <returns></returns>
    /// <remarks>Returns true if there is no license present</remarks>
    Task<bool> HasTokenExpired(int userId, ScrobbleProvider provider, CancellationToken ct = default);

    /// <summary>
    /// Create, or update a non-processed, <see cref="ScrobbleEventType.ScoreUpdated"/> event, for the given series
    /// </summary>
    /// <param name="userId"></param>
    /// <param name="seriesId"></param>
    /// <param name="rating"></param>
    /// <param name="ct"></param>
    /// <returns></returns>
    Task ScrobbleRatingUpdate(int userId, int seriesId, float rating, CancellationToken ct = default);

    /// <summary>
    /// NOP, until hardcover support has been worked out
    /// </summary>
    /// <param name="userId"></param>
    /// <param name="seriesId"></param>
    /// <param name="reviewTitle"></param>
    /// <param name="reviewBody"></param>
    /// <param name="ct"></param>
    /// <returns></returns>
    Task ScrobbleReviewUpdate(int userId, int seriesId, string? reviewTitle, string reviewBody, CancellationToken ct = default);

    /// <summary>
    /// Create, or update a non-processed, <see cref="ScrobbleEventType.ChapterRead"/> event, for the given series
    /// </summary>
    /// <param name="userId"></param>
    /// <param name="seriesId"></param>
    /// <param name="ct"></param>
    /// <returns></returns>
    Task ScrobbleReadingUpdate(int userId, int seriesId, CancellationToken ct = default);

    /// <summary>
    /// Creates an <see cref="ScrobbleEventType.AddWantToRead"/> or <see cref="ScrobbleEventType.RemoveWantToRead"/> for
    /// the given series
    /// </summary>
    /// <param name="userId"></param>
    /// <param name="seriesId"></param>
    /// <param name="onWantToRead"></param>
    /// <param name="ct"></param>
    /// <returns></returns>
    /// <remarks>Only the result of both WantToRead types is send to K+</remarks>
    Task ScrobbleWantToReadUpdate(int userId, int seriesId, bool onWantToRead, CancellationToken ct = default);

    /// <summary>
    /// Removed all processed events that are at least 7 days old
    /// </summary>
    /// <param name="ct"></param>
    /// <returns></returns>
    [DisableConcurrentExecution(60 * 60 * 60)]
    [AutomaticRetry(Attempts = 3, OnAttemptsExceeded = AttemptsExceededAction.Delete)]
    public Task ClearProcessedEvents(CancellationToken ct = default);

    /// <summary>
    /// Makes K+ requests for all non-processed events until rate limits are reached
    /// </summary>
    /// <param name="ct"></param>
    /// <returns></returns>
    [DisableConcurrentExecution(60 * 60 * 60)]
    [AutomaticRetry(Attempts = 3, OnAttemptsExceeded = AttemptsExceededAction.Delete)]
    Task ProcessUpdatesSinceLastSync(CancellationToken ct = default);

    Task CreateEventsFromExistingHistory(int userId = 0, CancellationToken ct = default);
    Task CreateEventsFromExistingHistoryForSeries(int seriesId, CancellationToken ct = default);
    Task ClearEventsForSeries(int userId, int seriesId, CancellationToken ct = default);
}

public static class ScrobblingHelper
{
    public const string AniListWeblinkWebsite = "https://anilist.co/manga/";
    public const string MalWeblinkWebsite = "https://myanimelist.net/manga/";
    public const string MalStaffWebsite = "https://myanimelist.net/people/";
    public const string MalCharacterWebsite = "https://myanimelist.net/character/";
    public const string GoogleBooksWeblinkWebsite = "https://books.google.com/books?id=";
    public const string MangaDexWeblinkWebsite = "https://mangadex.org/title/";
    public const string AniListStaffWebsite = "https://anilist.co/staff/";
    public const string AniListCharacterWebsite = "https://anilist.co/character/";
    public const string HardcoverStaffWebsite = "https://hardcover.app/authors/";

    private static readonly Dictionary<string, int> WeblinkExtractionMap = new()
    {
        {AniListWeblinkWebsite, 0},
        {MalWeblinkWebsite, 0},
        {GoogleBooksWeblinkWebsite, 0},
        {MangaDexWeblinkWebsite, 0},
        {AniListStaffWebsite, 0},
        {AniListCharacterWebsite, 0},
    };

    private static bool IsAniListReviewValid(string reviewTitle, string reviewBody)
    {
        return string.IsNullOrEmpty(reviewTitle) || string.IsNullOrEmpty(reviewBody) || (reviewTitle.Length < 2200 ||
            reviewTitle.Length > 120 ||
            reviewTitle.Length < 20);
    }

    public static long? GetMalId(Series series)
    {
        var malId = ExtractId<long?>(series.Metadata.WebLinks, MalWeblinkWebsite);
        return malId ?? series.ExternalSeriesMetadata?.MalId;
    }

    public static long? GetMalId(string weblinks)
    {
        return ExtractId<long?>(weblinks, MalWeblinkWebsite);
    }

    public static int? GetAniListId(Series seriesWithExternalMetadata)
    {
        var aniListId = ExtractId<int?>(seriesWithExternalMetadata.Metadata.WebLinks, AniListWeblinkWebsite);
        return aniListId ?? seriesWithExternalMetadata.ExternalSeriesMetadata?.AniListId;
    }

    public static int? GetAniListId(string weblinks)
    {
        return ExtractId<int?>(weblinks, AniListWeblinkWebsite);
    }

    /// <summary>
    /// Extract an Id from a given weblink
    /// </summary>
    /// <param name="webLinks"></param>
    /// <param name="website"></param>
    /// <returns></returns>
    public static T? ExtractId<T>(string webLinks, string website)
    {
        var index = WeblinkExtractionMap[website];
        foreach (var webLink in webLinks.Split(','))
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

    public static string CreateUrl(string url, long? id)
    {
        return id is null or 0 ? string.Empty : $"{url}{id}/";
    }

}
