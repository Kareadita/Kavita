using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Hangfire;
using Kavita.Common.Helpers;
using Kavita.Models.DTOs.KavitaPlus.Scrobble;
using Kavita.Models.DTOs.KavitaPlus;
using Kavita.Models.DTOs.KavitaPlus.Account;
using Kavita.Models.DTOs.Scrobbling;
using Kavita.Models.Entities;
using Kavita.Models.Entities.Enums;
using Kavita.Models.Entities.Enums.UserPreferences;
using Kavita.Models.Entities.User;

namespace Kavita.API.Services.Plus;

public interface IScrobblingService
{
    Task<List<UserTokenInfoDto>> GetUserTokenInfo(CancellationToken ct = default);

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
    Task ScrobbleSeriesRatingUpdate(int userId, int seriesId, float rating, CancellationToken ct = default);

    Task ScrobbleChapterRatingUpdate(int userId, int seriesId, int chapterId, float rating, CancellationToken ct = default);

    /// <summary>
    /// NOP, until hardcover support has been worked out
    /// </summary>
    /// <param name="userId"></param>
    /// <param name="seriesId"></param>
    /// <param name="reviewTitle"></param>
    /// <param name="reviewBody"></param>
    /// <param name="ct"></param>
    /// <returns></returns>
    Task ScrobbleSeriesReviewUpdate(int userId, int seriesId, string? reviewTitle, string reviewBody, CancellationToken ct = default);

    Task ScrobbleChapterReviewUpdate(int userId, int seriesId, int chapterId, string? reviewTitle, string reviewBody, CancellationToken ct = default);

    /// <summary>
    /// Create, or update a non-processed, <see cref="ScrobbleEventType.ChapterRead"/> event, for the given series
    /// </summary>
    /// <param name="userId"></param>
    /// <param name="seriesId"></param>
    /// <param name="chapterId"></param>
    /// <param name="ct"></param>
    /// <returns></returns>
    Task ScrobbleReadingUpdate(int userId, int seriesId, int chapterId, CancellationToken ct = default);

    Task ScrobbleReadingUpdateForSeries(int userId, int seriesId, CancellationToken ct = default);

    Task ScrobbleReadingUpdateForVolume(int userId, int volumeId, CancellationToken ct = default);

    Task ScrobbleReadingUpdateForChapters(int userId, int seriesId, List<int> chapterIds, CancellationToken ct = default);

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

    /// <summary>
    /// Run all enabled <see cref="ReadStatusTransitionRule"/>s
    /// </summary>
    /// <param name="ct"></param>
    /// <returns></returns>
    [DisableConcurrentExecution(60 * 60 * 60)]
    [AutomaticRetry(Attempts = 1, OnAttemptsExceeded = AttemptsExceededAction.Delete)]
    Task RunReadStatusTransitionRules(CancellationToken ct = default);

    /// <summary>
    /// Runs <see cref="CreateEventsFromExistingHistory(ScrobbleProvider, int, CancellationToken)"/> in sequence
    /// </summary>
    /// <param name="scrobbleProviders"></param>
    /// <param name="userId"></param>
    /// <param name="ct"></param>
    /// <returns></returns>
    Task CreateEventsFromExistingHistory(List<ScrobbleProvider> scrobbleProviders, int userId, CancellationToken ct = default);
    /// <summary>
    /// This will backfill events from existing progress history, ratings, and want to read for users that have a valid license
    /// </summary>
    /// <param name="scrobbleProvider"></param>
    /// <param name="userId">Defaults to 0 meaning all users. Allows a userId to be set if a scrobble key is added to a user</param>
    /// <param name="ct"></param>

    Task CreateEventsFromExistingHistory(ScrobbleProvider scrobbleProvider, int userId = 0, CancellationToken ct = default);
    Task CreateEventsFromExistingHistoryForSeries(int seriesId, CancellationToken ct = default);
    Task ClearEventsForSeries(int userId, int seriesId, CancellationToken ct = default);
    Task<bool> RetryScrobbleAsync(int authUserId, KavitaPlusAuditEntryDto auditEntry, CancellationToken ct = default);

    /// <summary>
    /// Sync local information for each scrobble provider for all suers
    /// </summary>
    /// <param name="userId"></param>
    /// <param name="provider"></param>
    /// <param name="ct"></param>
    /// <returns></returns>
    Task SyncProviderInfo(int userId, ScrobbleProvider provider, CancellationToken ct = default);

    Task<List<int>> FilterLibrariesForProvider(ScrobbleProvider provider, int userId, List<int> libraryIds, CancellationToken ct = default);
}

public sealed record ScrobbleUpdateContext
{
    public required AppUser User { get; init; }
    public required Series Series { get; init; }
    public Chapter? Chapter { get; set; }
    public bool IsBackfill { get; init; } = false;

}

/// <summary>
/// Where a provider enforces its rate limit
/// </summary>
public enum RateScope
{
    /// <summary>
    /// The limit is shared across the whole server/instance, regardless of user (e.g. AniList)
    /// </summary>
    Server = 0,
    /// <summary>
    /// The limit is tied to the individual user's token (e.g. Hardcover)
    /// </summary>
    User = 1
}

/// <summary>
/// Per-provider throttling configuration. Drives how long to wait between requests and when to back off
/// to let a provider's rate budget rebuild.
/// </summary>
/// <param name="BaseInterval">Steady-state gap between successive requests to this provider</param>
/// <param name="Buffer">Additional safety padding added on top of <paramref name="BaseInterval"/></param>
/// <param name="LowRateThreshold">When the remaining budget is at or below this, back off by <paramref name="RebuildWait"/> instead of <see cref="BaseRate"/></param>
/// <param name="RebuildWait">How long to wait before the next request once the budget is low, to let it rebuild</param>
/// <param name="Scope">Whether the limit is shared server-wide or per-user</param>
public record RateProfile(
    TimeSpan BaseInterval,
    TimeSpan Buffer,
    int LowRateThreshold,
    TimeSpan RebuildWait,
    RateScope Scope)
{
    /// <summary>
    /// Effective steady-state delay between requests (interval plus safety buffer)
    /// </summary>
    public TimeSpan BaseRate => BaseInterval + Buffer;
}

public interface IScrobbleProviderService
{
    /// <summary>
    /// Per-provider rate limiting profile, used to pace and back off requests during a sync
    /// </summary>
    RateProfile RateProfile { get; }

    /// <summary>
    /// Create, or update a non-processed, <see cref="ScrobbleEventType.ScoreUpdated"/> event, for the given series
    /// </summary>
    /// <param name="ctx"></param>
    /// <param name="rating"></param>
    /// <param name="ct"></param>
    /// <returns></returns>
    Task ScrobbleRatingUpdate(ScrobbleUpdateContext ctx, float rating, CancellationToken ct = default);

    /// <summary>
    /// Leaves a review for the series or chapter
    /// </summary>
    /// <param name="ctx"></param>
    /// <param name="reviewTitle"></param>
    /// <param name="reviewBody"></param>
    /// <param name="ct"></param>
    /// <returns></returns>
    Task ScrobbleReviewUpdate(ScrobbleUpdateContext ctx, string? reviewTitle, string reviewBody, CancellationToken ct = default);

    /// <summary>
    /// Create, or update a non-processed, <see cref="ScrobbleEventType.ChapterRead"/> event, for the given series
    /// </summary>
    /// <param name="ctx"></param>
    /// <param name="ct"></param>
    /// <returns></returns>
    Task ScrobbleReadingUpdate(ScrobbleUpdateContext ctx, CancellationToken ct = default);

    /// <summary>
    /// Creates an <see cref="ScrobbleEventType.AddWantToRead"/> or <see cref="ScrobbleEventType.RemoveWantToRead"/> for
    /// the given series
    /// </summary>
    /// <param name="ctx"></param>
    /// <param name="onWantToRead"></param>
    /// <param name="ct"></param>
    /// <returns></returns>
    /// <remarks>Only the result of both WantToRead types is send to K+</remarks>
    Task ScrobbleWantToReadUpdate(ScrobbleUpdateContext ctx, bool onWantToRead, CancellationToken ct = default);

    /// <summary>
    ///  Creates an <see cref="ScrobbleEventType.ReadStatusUpdate"/> for the given series (/chapter)
    /// </summary>
    /// <param name="ctx"></param>
    /// <param name="status"></param>
    /// <param name="ruleKind">Which transition rule produced this, when fired from <see cref="IScrobblingService.RunReadStatusTransitionRules"/>. Pins onto the event for the delivery-time ledger write.</param>
    /// <param name="ruleHash">Snapshot of the rule's configuration hash at fire-time.</param>
    /// <param name="ct"></param>
    /// <returns></returns>
    Task ScrobbleReadStatusUpdates(ScrobbleUpdateContext ctx, ScrobbleReadStatus status,
        TransitionRuleKind? ruleKind = null, string? ruleHash = null, CancellationToken ct = default);

    /// <summary>
    /// Check if the token is valid and not expired (No api calls made)
    /// </summary>
    /// <param name="token"></param>
    /// <returns></returns>
    bool IsTokenValid(string token);
}

// TODO: Figure out a place to put this that doesn't cause dependency hell
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

    public static long? GetMalId(Series series)
    {
        if (series.MalId != 0) return series.MalId;

        return ExternalIdParser.GetMalId(series.Metadata.WebLinks) ?? series.ExternalSeriesMetadata?.MalId;
    }


    public static int? GetAniListId(Series seriesWithExternalMetadata)
    {
        if (seriesWithExternalMetadata.AniListId != 0) return seriesWithExternalMetadata.AniListId;

        var aniListId = ExternalIdParser.GetAniListId(seriesWithExternalMetadata.Metadata.WebLinks);
        return aniListId ?? seriesWithExternalMetadata.ExternalSeriesMetadata?.AniListId;
    }


    public static string CreateUrl(string url, long? id)
    {
        return id is null or 0 ? string.Empty : $"{url}{id}/";
    }
}
