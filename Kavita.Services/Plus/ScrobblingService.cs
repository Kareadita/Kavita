using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Flurl.Http;
using Hangfire;
using Kavita.API.Database;
using Kavita.API.Repositories;
using Kavita.API.Services;
using Kavita.API.Services.Plus;
using Kavita.API.Services.SignalR;
using Kavita.Common;
using Kavita.Common.Extensions;
using Kavita.Common.Helpers;
using Kavita.Models.Constants;
using Kavita.Models.DTOs.Filtering.v2;
using Kavita.Models.DTOs.Filtering.v2.Requests;
using Kavita.Models.DTOs.KavitaPlus;
using Kavita.Models.DTOs.KavitaPlus.Account;
using Kavita.Models.DTOs.KavitaPlus.Scrobble;
using Kavita.Models.DTOs.Scrobbling;
using Kavita.Models.DTOs.SignalR;
using Kavita.Models.Entities;
using Kavita.Models.Entities.Enums;
using Kavita.Models.Entities.Enums.Audit;
using Kavita.Models.Entities.Enums.KavitaPlus;
using Kavita.Models.Entities.Enums.UserPreferences;
using Kavita.Models.Entities.Metadata;
using Kavita.Models.Entities.Scrobble;
using Kavita.Models.Entities.User;
using Kavita.Services.Plus.ScrobbleService;
using Kavita.Services.Scanner;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace Kavita.Services.Plus;

/// <summary>
/// Context used when syncing scrobble events. Do NOT reuse between syncs
/// </summary>
public class ScrobbleSyncContext
{
    public required List<ScrobbleEvent> ReadEvents {get; init;}
    public required List<ScrobbleEvent> RatingEvents {get; init;}
    public required List<ScrobbleEvent> ReviewEvents {get; init;}
    public required List<ScrobbleEvent> ReadStatusEvents {get; init;}
    /// <remarks>Do not use this as events to send to K+, use <see cref="Decisions"/></remarks>
    public required List<ScrobbleEvent> AddToWantToRead {get; init;}
    /// <remarks>Do not use this as events to send to K+, use <see cref="Decisions"/></remarks>
    public required List<ScrobbleEvent> RemoveWantToRead {get; init;}
    /// <summary>
    /// Final events list if all AddTo- and RemoveWantToRead would be processed sequentially
    /// </summary>
    public required List<ScrobbleEvent> Decisions {get; init;}
    /// <summary>
    /// K+ license
    /// </summary>
    public required string License { get; init; }
    /// <summary>
    /// Per-provider throttle profiles, resolved once at the start of the sync
    /// </summary>
    public required IReadOnlyDictionary<ScrobbleProvider, RateProfile> RateProfiles { get; init; }

    /// <summary>
    /// Rate gates keyed by scope. Server-scoped providers share a single gate (UserId is null);
    /// user-scoped providers get one gate per user.
    /// </summary>
    private readonly Dictionary<(int? UserId, ScrobbleProvider Provider), RateGate> _rateGates = [];

    /// <summary>
    /// Resolves (creating if needed) the rate gate for a user/provider, keyed by the provider's scope
    /// </summary>
    public RateGate GetRateGate(int userId, ScrobbleProvider provider)
    {
        var profile = RateProfiles[provider];
        var key = (profile.Scope == RateScope.Server ? (int?) null : userId, provider);

        if (_rateGates.TryGetValue(key, out var gate)) return gate;

        gate = new RateGate(profile);
        _rateGates[key] = gate;

        return gate;
    }

    /// <summary>
    /// Resolves (creating if needed) the rate gate for the scope an event belongs to
    /// </summary>
    public RateGate GetRateGate(ScrobbleEvent evt) => GetRateGate(evt.AppUserId, evt.ScrobbleProvider);

    /// <summary>
    /// Minimum spacing between ANY two K+ requests, regardless of provider/scope
    /// </summary>
    public static readonly TimeSpan GlobalRequestFloor = TimeSpan.FromMilliseconds(250);

    private DateTime _nextGlobalAllowedUtc = DateTime.MinValue;

    /// <summary>
    /// How long to wait to honor the global request floor (never negative)
    /// </summary>
    public TimeSpan GetGlobalFloorWait()
    {
        var wait = _nextGlobalAllowedUtc - DateTime.UtcNow;
        return wait > TimeSpan.Zero ? wait : TimeSpan.Zero;
    }

    /// <summary>
    /// Records that a K+ request is firing now, arming the global floor for the next one
    /// </summary>
    public void RecordGlobalRequest() => _nextGlobalAllowedUtc = DateTime.UtcNow + GlobalRequestFloor;

    /// <summary>
    /// All users being scrobbled for
    /// </summary>
    public List<AppUser> Users { get; set; } = [];
    /// <summary>
    /// Amount of already processed events
    /// </summary>
    public int ProgressCounter { get; set; }

    public IEnumerable<ScrobbleEvent> GetEventsToProcess()
    {
        return ReadEvents
            .Concat(RatingEvents)
            .Concat(ReviewEvents)
            .Concat(ReadStatusEvents)
            .Concat(Decisions);
    }

    public List<ScrobbleProvider> ProvidersForUser(int userId)
    {
        return GetEventsToProcess()
            .Where(e => e.AppUserId == userId)
            .Select(e => e.ScrobbleProvider)
            .Distinct()
            .ToList();
    }

    /// <summary>
    /// Sum of all events to process
    /// </summary>
    public int TotalCount => ReadEvents.Count + RatingEvents.Count + ReviewEvents.Count + ReadStatusEvents.Count + Decisions.Count;

    /// <summary>
    /// Tracks the remaining rate budget and the next-allowed-request time for a single rate scope
    /// (a server-wide provider, or one user's provider token). Encapsulates all pacing/backoff decisions
    /// so the processing loop reads as: has budget? wait my turn, post, record the result.
    /// </summary>
    public sealed class RateGate(RateProfile profile)
    {
        private DateTime _nextAllowedUtc = DateTime.MinValue;
        private int _rateLeft;

        /// <summary>
        /// True once the initial budget has been fetched from K+. Lets server-scoped gates shared
        /// between users avoid duplicate lookups.
        /// </summary>
        public bool IsSeeded { get; private set; }

        /// <summary>
        /// Whether there is any budget left to attempt a request
        /// </summary>
        public bool HasRateLeft() => _rateLeft > 0;

        /// <summary>
        /// How long to wait before the next request may be sent for this scope (never negative)
        /// </summary>
        public TimeSpan GetWaitTime()
        {
            var wait = _nextAllowedUtc - DateTime.UtcNow;
            return wait > TimeSpan.Zero ? wait : TimeSpan.Zero;
        }

        /// <summary>
        /// Seeds the initial remaining budget (from K+) without scheduling a wait
        /// </summary>
        public void Seed(int rateLeft)
        {
            _rateLeft = rateLeft;
            IsSeeded = true;
        }

        /// <summary>
        /// Records the budget returned by a request and schedules when the next one may fire.
        /// Backs off for <see cref="RateProfile.RebuildWait"/> when the budget is at or below the threshold.
        /// </summary>
        public void RecordResult(int rateLeft)
        {
            _rateLeft = rateLeft;
            var delay = rateLeft <= profile.LowRateThreshold ? profile.RebuildWait : profile.BaseRate;
            _nextAllowedUtc = DateTime.UtcNow + delay;
        }
    }
}

public class ScrobblingService : IScrobblingService
{
    private readonly IUnitOfWork _unitOfWork;
    private readonly IEventHub _eventHub;
    private readonly ILogger<ScrobblingService> _logger;
    private readonly ILicenseService _licenseService;
    private readonly ILocalizationService _localizationService;
    private readonly IEmailService _emailService;
    private readonly IKavitaPlusApiService _kavitaPlusApiService;
    private readonly IServiceProvider _serviceProvider;
    private readonly IKavitaPlusAuditService _auditService;
    private readonly IScrobbleRuleService _ruleService;

    public const string AniListWeblinkWebsite = ScrobblingHelper.AniListWeblinkWebsite;
    public const string MalWeblinkWebsite = ScrobblingHelper.MalWeblinkWebsite;
    public const string MalStaffWebsite = ScrobblingHelper.MalStaffWebsite;
    public const string MalCharacterWebsite = ScrobblingHelper.MalCharacterWebsite;
    public const string AniListStaffWebsite = ScrobblingHelper.AniListStaffWebsite;
    public const string AniListCharacterWebsite = ScrobblingHelper.AniListCharacterWebsite;
    public const string HardcoverStaffWebsite = ScrobblingHelper.HardcoverStaffWebsite;

    private const SeriesIncludes ScrobbleSeriesIncludes = SeriesIncludes.Library | SeriesIncludes.ExternalMetadata | SeriesIncludes.Metadata;

    // When adjusting these, also adjust in ManageScrobbleProvidersComponent in the UI
    private static readonly IList<ScrobbleProvider> BookProviders = [
        ScrobbleProvider.Hardcover
    ];
    private static readonly IList<ScrobbleProvider> LightNovelProviders =
    [
        ScrobbleProvider.AniList, ScrobbleProvider.Hardcover, ScrobbleProvider.MangaBaka
    ];
    private static readonly IList<ScrobbleProvider> ComicProviders = Array.Empty<ScrobbleProvider>();
    private static readonly IList<ScrobbleProvider> MangaProviders = [
        ScrobbleProvider.AniList, ScrobbleProvider.MangaBaka, ScrobbleProvider.Mal
    ];

    private const string RateLimitHitErrorMessage = "Scrobbling rate limit hit";
    private const string UnknownSeriesErrorMessage = "Series cannot be matched for Scrobbling";
    private const string AccessTokenErrorMessage = "Access Token needs to be rotated to continue scrobbling";
    private const string InvalidKPlusLicenseErrorMessage = "Kavita+ subscription no longer active";
    private const string ReviewFailedErrorMessage = "Review was unable to be saved due to upstream requirements";
    private const string BadPayLoadErrorMessage = "Bad payload from Scrobble Provider";

    /// <summary>
    /// Everything but Kavita (internal)
    /// </summary>
    public static readonly List<ScrobbleProvider> AllScrobbleProviders =
        Enum.GetValues<ScrobbleProvider>().Where(k => k != ScrobbleProvider.Kavita && k != ScrobbleProvider.Cbr).ToList();


    public ScrobblingService(IUnitOfWork unitOfWork, IEventHub eventHub, ILogger<ScrobblingService> logger,
        ILicenseService licenseService, ILocalizationService localizationService, IEmailService emailService,
        IKavitaPlusApiService kavitaPlusApiService, IServiceProvider serviceProvider, IKavitaPlusAuditService auditService,
        IScrobbleRuleService ruleService)
    {
        _unitOfWork = unitOfWork;
        _eventHub = eventHub;
        _logger = logger;
        _licenseService = licenseService;
        _localizationService = localizationService;
        _emailService = emailService;
        _kavitaPlusApiService = kavitaPlusApiService;
        _serviceProvider = serviceProvider;
        _auditService = auditService;
        _ruleService = ruleService;

        FlurlConfiguration.ConfigureClientForUrl(Configuration.KavitaPlusApiUrl);
    }

    public async Task<List<UserTokenInfoDto>> GetUserTokenInfo(CancellationToken ct = default)
    {
        var users = await _unitOfWork.UserRepository.GetAllUsersAsync(ct: ct);

        return users.Select(user => new UserTokenInfoDto
        {
            UserId = user.Id,
            Username = user.UserName ?? string.Empty,
            Tokens = user.ScrobbleProviders.Select(kv => new TokenValidityInfoDto
            {
                Provider = kv.Key,
                ValidUntilUtc = kv.Value.ValidUntilUtc,
            }).ToList(),
        }).ToList();
    }

    #region Access token checks

    /// <summary>
    /// An automated job that will run against all user's tokens and validate if they are still active
    /// </summary>
    /// <param name="ct"></param>
    /// <remarks>This service can validate without license check as the task which calls will be guarded</remarks>
    public async Task CheckExternalAccessTokens(CancellationToken ct = default)
    {
        var users = await _unitOfWork.UserRepository.GetAllUsersAsync(ct: ct);
        foreach (var user in users)
        {
            foreach (var (provider, settings) in user.ScrobbleProviders)
            {
                if (string.IsNullOrEmpty(settings.AuthenticationToken)) continue;

                if (settings.ValidUntilUtc.Equals(DateTime.MinValue) || settings.LastSyncedUtc.Equals(DateTime.MinValue)) continue;

                var providerService = _serviceProvider.GetRequiredKeyedService<IScrobbleProviderService>(provider);

                var tokenExpiry = settings.ValidUntilUtc;

                // Send early reminder 5 days before token expiry
                if (await ShouldSendEarlyReminder(user.Id, tokenExpiry))
                {
                    await _emailService.SendTokenExpiringSoonEmail(user.Id, provider);
                }

                // Send expiration notification after token expiry
                if (await ShouldSendExpirationReminder(user.Id, tokenExpiry))
                {
                    await _emailService.SendTokenExpiredEmail(user.Id, provider);
                }

                // Check token validity
                if (providerService.IsTokenValid(settings.AuthenticationToken)) continue;

                _logger.LogInformation(
                    "User {UserName}'s authentication token for {Provider} has expired or is expiring in a few days! They need to regenerate it for scrobbling to work",
                    user.UserName, provider);

                // Notify user via event
                await _eventHub.SendMessageToAsync(
                    MessageFactory.ScrobblingKeyExpired,
                    MessageFactory.ScrobblingKeyExpiredEvent(provider),
                    user.Id, ct);
            }
        }
    }

    /// <summary>
    /// Checks if an early reminder email should be sent.
    /// </summary>
    private async Task<bool> ShouldSendEarlyReminder(int userId, DateTime tokenExpiry)
    {
        var earlyReminderDate = tokenExpiry.AddDays(-5);
        if (earlyReminderDate > DateTime.UtcNow) return false;

        var hasAlreadySentReminder = await _unitOfWork.DataContext.EmailHistory
            .AnyAsync(h => h.AppUserId == userId && h.Sent &&
                           h.EmailTemplate == EmailService.TokenExpiringSoonTemplate &&
                           h.SendDate >= earlyReminderDate);

        return !hasAlreadySentReminder;

    }

    /// <summary>
    /// Checks if an expiration notification email should be sent.
    /// </summary>
    private async Task<bool> ShouldSendExpirationReminder(int userId, DateTime tokenExpiry)
    {
        if (tokenExpiry > DateTime.UtcNow) return false;

        var hasAlreadySentExpirationEmail = await _unitOfWork.DataContext.EmailHistory
            .AnyAsync(h => h.AppUserId == userId && h.Sent &&
                           h.EmailTemplate == EmailService.TokenExpirationTemplate &&
                           h.SendDate >= tokenExpiry);

        return !hasAlreadySentExpirationEmail;
    }

    public async Task<bool> HasTokenExpired(int userId, ScrobbleProvider provider, CancellationToken ct = default)
    {
        var user = await _unitOfWork.UserRepository.GetUserByIdAsync(userId, ct: ct);
        if (user == null) return false;

        var token = user.ScrobbleProviders[provider].AuthenticationToken;

        if (string.IsNullOrEmpty(token)) return false;

        return await HasTokenExpired(token, provider);
    }

    private async Task<bool> HasTokenExpired(string token, ScrobbleProvider provider)
    {
        var providerService = _serviceProvider.GetRequiredKeyedService<IScrobbleProviderService>(provider);

        if (string.IsNullOrEmpty(token) || providerService.IsTokenValid(token)) return false;

        var license = await _unitOfWork.SettingsRepository.GetSettingAsync(ServerSettingKey.LicenseKey);
        if (string.IsNullOrEmpty(license.Value)) return true;

        var result = await _kavitaPlusApiService.HasTokenExpiredForProviderAsync(provider, token, license.Value);
        if (!result.IsSuccess)
        {
            _logger.LogWarning("Failed to check token validity with K+: {ErrorMessage}", result.ErrorMessage);
            return true;
        }

        return result.Data;
    }

    #endregion

    #region Scrobble ingest

    private static bool IsLibraryTypeSupported(ScrobbleProvider provider, LibraryType libraryType)
    {
        return libraryType switch
        {
            LibraryType.Manga => MangaProviders.Contains(provider),
            LibraryType.Comic => ComicProviders.Contains(provider),
            LibraryType.Book => BookProviders.Contains(provider),
            LibraryType.Image => false,
            LibraryType.LightNovel => LightNovelProviders.Contains(provider),
            LibraryType.ComicVine => ComicProviders.Contains(provider),
            _ => throw new ArgumentOutOfRangeException(nameof(libraryType), libraryType, null)
        };
    }

    private List<ScrobbleProvider> GetProvidersForScrobbleEvent(List<ScrobbleProvider>? scrobbleProviders,
        ScrobbleEventType eventType, ScrobbleUpdateContext ctx)
    {
        return GetProvidersForScrobbleEvent(scrobbleProviders, eventType, ctx.User, ctx.Series);
    }

    /// <summary>
    /// Returns the providers that are applicable for the ScrobbleEventType
    /// </summary>
    /// <param name="scrobbleProviders">If not null, returned providers are guaranteed to be in this list</param>
    /// <param name="eventType"></param>
    /// <param name="user">Should include UserPreferences</param>
    /// <param name="series"></param>
    /// <returns></returns>
    private List<ScrobbleProvider> GetProvidersForScrobbleEvent(
        List<ScrobbleProvider>? scrobbleProviders,
        ScrobbleEventType eventType,
        AppUser user,
        Series series)
    {

        Func<ScrobbleProviderSettingsDto, bool> guard = eventType switch
        {
            ScrobbleEventType.ChapterRead => s => s.ProgressScrobbling,
            ScrobbleEventType.AddWantToRead => s => s.WantToReadSync,
            ScrobbleEventType.RemoveWantToRead => s => s.WantToReadSync,
            ScrobbleEventType.ScoreUpdated => s => s.RatingScrobbling,
            ScrobbleEventType.Review => s => s.ReviewsScrobbling,
            ScrobbleEventType.ReadStatusUpdate => s => true,
            _ => throw new ArgumentOutOfRangeException(nameof(eventType), eventType, null)
        };

        var providerCandidates = scrobbleProviders ?? AllScrobbleProviders;
        List<ScrobbleProvider> providers = [];

        foreach (var provider in providerCandidates)
        {
            var scrobbleProvider = user.ScrobbleProviders[provider];

            if (string.IsNullOrEmpty(scrobbleProvider.AuthenticationToken))
            {
                continue;
            }

            var settings = scrobbleProvider.Settings;
            if (!guard(settings))
            {
                _logger.LogTrace("[{Provider}/{UserId}] Skipping {EventType} event on {SeriesId} because the event type is disabled",
                    provider, user.Id, eventType, series.Id);
                continue;
            }

            if (settings.HighestAgeRating != AgeRating.NotApplicable && series.Metadata.AgeRating > settings.HighestAgeRating)
            {
                _logger.LogTrace("[{Provider}/{UserId}] Skipping {EventType} event on {SeriesId} because the series's Age Rating is too high {SeriesAgeRating} > {ProviderAgeRating}",
                    provider, user.Id, eventType, series.Id, series.Metadata.AgeRating, settings.HighestAgeRating);
                continue;
            }

            if (!IsLibraryTypeSupported(provider, series.Library.Type))
            {
                _logger.LogTrace("[{Provider}/{UserId}] Skipping {EventType} event on {SeriesId} because the series's library type ({LibraryType}) is not supported",
                    provider, user.Id, eventType, series.Id, series.Library.Type);
                continue;
            }


            if (!settings.AllLibraries && !settings.Libraries.Contains(series.LibraryId))
            {
                _logger.LogTrace("[{Provider}/{UserId}] Skipping {EventType} event on {SeriesId} because the series's library ({LibraryId}) is not in the list of libraries to scrobble",
                    provider, user.Id, eventType, series.Id, series.LibraryId);
                continue;
            }

            providers.Add(provider);
        }

        return providers;
    }

    private async Task<ScrobbleUpdateContext> CreateScrobbleUpdateContext(int userId, int seriesId, int? chapterId, bool isBackfill, CancellationToken ct)
    {
        var series = await _unitOfWork.SeriesRepository.GetSeriesByIdAsync(seriesId, ScrobbleSeriesIncludes, ct);
        if (series == null) throw new KavitaException(await _localizationService.TranslateAsync(userId, "series-doesnt-exist"));

        Chapter? chapter = null;
        if (chapterId.HasValue)
        {
            chapter = await _unitOfWork.ChapterRepository.GetChapterAsync(chapterId.Value, ChapterIncludes.Volumes, ct: ct);
            if (chapter == null) throw new KavitaException(await _localizationService.TranslateAsync(userId, "chapter-doesnt-exist"));
        }

        var user = await _unitOfWork.UserRepository.GetUserByIdAsync(userId, AppUserIncludes.UserPreferences, ct);
        if (user == null) throw new KavitaException(await _localizationService.TranslateAsync(userId, "user-doesnt-exist"));

        return new ScrobbleUpdateContext
        {
            User = user,
            Series = series,
            Chapter = chapter,
            IsBackfill = isBackfill
        };
    }

    #region Review

    public Task ScrobbleSeriesReviewUpdate(int userId, int seriesId, string? reviewTitle, string reviewBody, CancellationToken ct = default)
    {
        return ScrobbleReviewUpdate(null, userId, seriesId, null, reviewTitle, reviewBody, false, ct);
    }

    public Task ScrobbleChapterReviewUpdate(int userId, int seriesId, int chapterId, string? reviewTitle, string reviewBody, CancellationToken ct = default)
    {
        return ScrobbleReviewUpdate(null, userId, seriesId, chapterId, reviewTitle, reviewBody, false, ct);
    }

    private async Task ScrobbleReviewUpdate(List<ScrobbleProvider>? scrobbleProviders, int userId, int seriesId, int? chapterId, string? reviewTitle, string reviewBody, bool isBackFill, CancellationToken ct = default)
    {
        if (!await _licenseService.HasActiveLicense(ct: ct)) return;

        var ctx = await CreateScrobbleUpdateContext(userId, seriesId, chapterId, isBackFill, ct);

        var providers = GetProvidersForScrobbleEvent(scrobbleProviders, ScrobbleEventType.Review, ctx);
        if (providers.Count == 0)
        {
            _logger.LogDebug("Ignoring scrobble review update on {SeriesId} - {ChapterId}, no providers matched", seriesId, chapterId);
            return;
        }

        _logger.LogInformation("Processing Scrobbling review event for {AppUserId} on {SeriesName}", userId, ctx.Series.Name);

        foreach (var provider in providers)
        {
            if (await CheckIfCannotScrobble(provider, ScrobbleEventType.Review, userId, seriesId, ctx.Series)) continue;

            var scrobbleProviderService = _serviceProvider.GetRequiredKeyedService<IScrobbleProviderService>(provider);

            try
            {
                await scrobbleProviderService.ScrobbleReviewUpdate(ctx, reviewTitle, reviewBody, ct);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "An error happened while trying to scrobble an review event for {UserId} - {Provider}", ctx.User.Id, provider);
            }
        }
    }

    #endregion

    #region Rating

    public Task ScrobbleSeriesRatingUpdate(int userId, int seriesId, float rating, CancellationToken ct = default)
    {
        return ScrobbleRatingUpdate(null, userId, seriesId, null, rating, false, ct);
    }

    public Task ScrobbleChapterRatingUpdate(int userId, int seriesId, int chapterId, float rating, CancellationToken ct = default)
    {
        return ScrobbleRatingUpdate(null, userId, seriesId, chapterId, rating, false, ct);
    }

    private async Task ScrobbleRatingUpdate(List<ScrobbleProvider>? scrobbleProviders, int userId, int seriesId, int? chapterId, float rating, bool isBackfill, CancellationToken ct = default)
    {
        if (!await _licenseService.HasActiveLicense(ct: ct)) return;

        var ctx = await CreateScrobbleUpdateContext(userId, seriesId, chapterId, isBackfill, ct);

        var providers = GetProvidersForScrobbleEvent(scrobbleProviders, ScrobbleEventType.ScoreUpdated, ctx);
        if (providers.Count == 0)
        {
            _logger.LogDebug("Ignoring scrobble rating update on {SeriesId} - {ChapterId}, no providers matched", seriesId, chapterId);
            return;
        }

        _logger.LogInformation("Processing Scrobbling rating event for {AppUserId} on {SeriesName}", userId, ctx.Series.Name);

        foreach (var provider in providers)
        {
            if (await CheckIfCannotScrobble(provider, ScrobbleEventType.ScoreUpdated, userId, seriesId, ctx.Series)) continue;

            var scrobbleProviderService = _serviceProvider.GetRequiredKeyedService<IScrobbleProviderService>(provider);

            try
            {
                await scrobbleProviderService.ScrobbleRatingUpdate(ctx, rating, ct);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "An error happened while trying to scrobble an rating event for {UserId} - {Provider}", ctx.User.Id, provider);
            }
        }
    }

    #endregion

    #region Reading

    public Task ScrobbleReadingUpdate(int userId, int seriesId, int chapterId, CancellationToken ct = default)
    {
        return ScrobbleReadingUpdate(userId, seriesId, chapterId, false, ct);
    }

    private async Task ScrobbleReadingUpdate(int userId, int seriesId, int chapterId, bool isBackfill, CancellationToken ct = default)
    {
        if (!await _licenseService.HasActiveLicense(ct: ct)) return;

        var ctx = await CreateScrobbleUpdateContext(userId, seriesId, chapterId, isBackfill, ct);

        var providers = GetProvidersForScrobbleEvent(null, ScrobbleEventType.ChapterRead, ctx);
        if (providers.Count == 0)
        {
            _logger.LogDebug("Ignoring scrobble reading update on {SeriesId} - {ChapterId}, no providers matched", seriesId, chapterId);
            return;
        }

        _logger.LogInformation("Processing Scrobbling reading event for {AppUserId} on {SeriesName}", userId, ctx.Series.Name);

        foreach (var provider in providers)
        {
            if (await CheckIfCannotScrobble(provider, ScrobbleEventType.ChapterRead, userId, seriesId, ctx.Series)) continue;

            var scrobbleProviderService = _serviceProvider.GetRequiredKeyedService<IScrobbleProviderService>(provider);

            try
            {
                await scrobbleProviderService.ScrobbleReadingUpdate(ctx, ct);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "An error happened while trying to scrobble an reading event for {UserId} - {Provider}", ctx.User.Id, provider);
            }
        }
    }

    public Task ScrobbleReadingUpdateForSeries(int userId, int seriesId, CancellationToken ct = default)
    {
        return ScrobbleReadingUpdateForSeries(null, userId, seriesId, false, ct);
    }

    private async Task ScrobbleReadingUpdateForSeries(List<ScrobbleProvider>? providers, int userId, int seriesId, bool isBackfill, CancellationToken ct = default)
    {
        if (!await _licenseService.HasActiveLicense(ct: ct)) return;

        var user = await _unitOfWork.UserRepository.GetUserByIdAsync(userId, ct: ct);
        if (user == null) throw new KavitaException(await _localizationService.TranslateAsync(userId, "user-doesnt-exist"));

        var series = await _unitOfWork.SeriesRepository.GetSeriesByIdAsync(seriesId, ScrobbleSeriesIncludes | SeriesIncludes.Chapters, ct);
        if (series == null) throw new KavitaException(await _localizationService.TranslateAsync(userId, "series-doesnt-exist"));

        var allChapters = series.Volumes.SelectMany(v => v.Chapters).ToList();

        await ScrobbleReadingUpdatesForChaptersSmart(providers, user, series, allChapters, isBackfill, ct);
    }

    public async Task ScrobbleReadingUpdateForVolume(int userId, int volumeId, CancellationToken ct = default)
    {
        if (!await _licenseService.HasActiveLicense(ct: ct)) return;

        var user = await _unitOfWork.UserRepository.GetUserByIdAsync(userId, ct: ct);
        if (user == null) throw new KavitaException(await _localizationService.TranslateAsync(userId, "user-doesnt-exist"));

        var volume = await _unitOfWork.VolumeRepository.GetVolumeByIdAsync(volumeId, VolumeIncludes.Chapters, ct: ct);
        if (volume == null) throw new KavitaException(await _localizationService.TranslateAsync(userId, "volume-doesnt-exist"));

        var series = await _unitOfWork.SeriesRepository.GetSeriesByIdAsync(volume.SeriesId, ScrobbleSeriesIncludes, ct);
        if (series == null) throw new KavitaException(await _localizationService.TranslateAsync(userId, "series-doesnt-exist"));

        var allChapters = volume.Chapters.ToList();

        await ScrobbleReadingUpdatesForChaptersSmart(null, user, series, allChapters, false, ct);
    }

    public async Task ScrobbleReadingUpdateForChapters(int userId, int seriesId, List<int> chapterIds, CancellationToken ct = default)
    {
        if (chapterIds.Count == 0) return;

        if (!await _licenseService.HasActiveLicense(ct: ct)) return;

        var user = await _unitOfWork.UserRepository.GetUserByIdAsync(userId, ct: ct);
        if (user == null) throw new KavitaException(await _localizationService.TranslateAsync(userId, "user-doesnt-exist"));

        var series = await _unitOfWork.SeriesRepository.GetSeriesByIdAsync(seriesId, ScrobbleSeriesIncludes, ct);
        if (series == null) throw new KavitaException(await _localizationService.TranslateAsync(userId, "series-doesnt-exist"));

        var chapters = await _unitOfWork.ChapterRepository.GetChaptersByIdsAsync(chapterIds, ct: ct);

        await ScrobbleReadingUpdatesForChaptersSmart(null, user, series, chapters.ToList(), false, ct);
    }

    private async Task ScrobbleReadingUpdatesForChaptersSmart(
        List<ScrobbleProvider>? scrobbleProviders,
        AppUser user,
        Series series,
        List<Chapter> chapters,
        bool isBackfill,
        CancellationToken ct = default)
    {
        var ctx = new ScrobbleUpdateContext
        {
            User = user,
            Series = series,
            IsBackfill = isBackfill
        };

        var providers = GetProvidersForScrobbleEvent(scrobbleProviders, ScrobbleEventType.ChapterRead, ctx);
        if (providers.Count == 0)
        {
            _logger.LogDebug("Ignoring scrobble reading update on {SeriesId} - chapters {ChapterIds}, no providers matched", series.Id, string.Join(", ", chapters.Select(c => c.Id)));
            return;
        }

        _logger.LogInformation("Processing Scrobbling reading event for {AppUserId} on {SeriesName} for {Count} chapters",
            user.Id, series.Name, chapters.Count);

        foreach (var provider in providers)
        {
            var scrobbleProviderService = _serviceProvider.GetRequiredKeyedService<IScrobbleProviderService>(provider);

            // Explicit check to reduce DB calls and work done
            if (scrobbleProviderService is ISeriesScrobbleService)
            {
                var firstChapter = chapters.FirstOrDefault();
                if (firstChapter != null)
                {
                    ctx.Chapter = firstChapter;
                    await scrobbleProviderService.ScrobbleReadingUpdate(ctx, ct);
                }
            }
            else
            {
                foreach (var chapter in chapters)
                {
                    ctx.Chapter = chapter;
                    await scrobbleProviderService.ScrobbleReadingUpdate(ctx, ct);
                }
            }
        }
    }

    #endregion

    public Task ScrobbleWantToReadUpdate(int userId, int seriesId, bool onWantToRead, CancellationToken ct = default)
    {
        return ScrobbleWantToReadUpdate(null, userId, seriesId, onWantToRead, false, ct);
    }

    private async Task ScrobbleWantToReadUpdate(List<ScrobbleProvider>? scrobbleProviders, int userId, int seriesId, bool onWantToRead, bool isBackFill, CancellationToken ct = default)
    {
        if (!await _licenseService.HasActiveLicense(ct: ct)) return;

        var user = await _unitOfWork.UserRepository.GetUserByIdAsync(userId, ct: ct);
        if (user == null) throw new KavitaException(await _localizationService.TranslateAsync(userId, "user-doesnt-exist"));

        var series = await _unitOfWork.SeriesRepository.GetSeriesByIdAsync(seriesId, ScrobbleSeriesIncludes | SeriesIncludes.Chapters, ct);
        if (series == null) throw new KavitaException(await _localizationService.TranslateAsync(userId, "series-doesnt-exist"));

        var ctx = new ScrobbleUpdateContext
        {
            User = user,
            Series = series,
            Chapter = null,
            IsBackfill = isBackFill
        };

        var eventType = onWantToRead ? ScrobbleEventType.AddWantToRead : ScrobbleEventType.RemoveWantToRead;

        var providers = GetProvidersForScrobbleEvent(scrobbleProviders, eventType, ctx);
        if (providers.Count == 0)
        {
            _logger.LogDebug("Ignoring scrobble want to read update on {SeriesId} (onWantToRead: {OnWantToRead}), no providers matched", seriesId, onWantToRead);
            return;
        }

        _logger.LogInformation("Processing Scrobbling want to read event for {AppUserId} on {SeriesName}", userId, series.Name);

        var allChapters = series.Volumes.SelectMany(v => v.Chapters).ToList();
        if (allChapters.Count == 0) return;

        var firstChapter = allChapters[0];

        foreach (var provider in providers)
        {
            if (await CheckIfCannotScrobble(provider, eventType, userId, seriesId, series)) continue;

            var scrobbleProviderService = _serviceProvider.GetRequiredKeyedService<IScrobbleProviderService>(provider);

            try
            {
                // As WantToRead updates are always at a series level, we split here to avoid DB calls
                if (scrobbleProviderService is ISeriesScrobbleService)
                {
                    ctx.Chapter = firstChapter;
                    await scrobbleProviderService.ScrobbleWantToReadUpdate(ctx, onWantToRead, ct);
                }
                else
                {
                    foreach (var chapter in allChapters)
                    {
                        ctx.Chapter = chapter;
                        await scrobbleProviderService.ScrobbleWantToReadUpdate(ctx, onWantToRead, ct);
                    }
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "An error happened while trying to scrobble a want to read event for {UserId} - {Provider}", user.Id, provider);
            }
        }
    }

    #endregion

    /// <summary>
    /// Returns false if the series cannot be scrobbled for the given provider. I.e. not matched for that provider
    /// series on hold, or the library is not eligible
    /// </summary>
    /// <param name="provider"></param>
    /// <param name="eventType"></param>
    /// <param name="userId"></param>
    /// <param name="seriesId"></param>
    /// <param name="series">Should have Library resolved</param>
    /// <returns></returns>
    private async Task<bool> CheckIfCannotScrobble(ScrobbleProvider provider, ScrobbleEventType eventType, int userId, int seriesId, Series series)
    {
        // TODO: This needs updating to take the provider into account (Dict?)
        if (series.DontMatch)
        {
            _logger.LogInformation("Series {SeriesName} is marked don't match. Not scrobbling", series.Name);
            await _auditService.LogScrobbleAsync(KavitaPlusEventType.ScrobbleEventSkipped, seriesId,
                new AuditLogScrobbleParamsDto() {Provider = provider, ScrobbleEventType = eventType}, AuditStatus.Info, "series-dont-match", userId);
            return true;
        }

        if (await _unitOfWork.UserRepository.HasHoldOnSeries(userId, seriesId))
        {
            _logger.LogInformation("Series {SeriesName} is on AppUserId {AppUserId}'s hold list. Not scrobbling", series.Name, userId);
            await _auditService.LogScrobbleAsync(KavitaPlusEventType.ScrobbleEventSkipped, seriesId,
                new AuditLogScrobbleParamsDto() {Provider = provider, ScrobbleEventType = eventType}, AuditStatus.Info, "scrobble-hold-active", userId);
            return true;
        }

        var library = series.Library ?? await _unitOfWork.LibraryRepository.GetLibraryForIdAsync(series.LibraryId);
        if (library is not {AllowScrobbling: true} || !ExternalMetadataService.IsPlusEligible(library.Type))
        {
            await _auditService.LogScrobbleAsync(KavitaPlusEventType.ScrobbleEventSkipped, seriesId,
                new AuditLogScrobbleParamsDto() {Provider = provider, ScrobbleEventType = eventType}, AuditStatus.Info, "library-scrobbling-disabled", userId);
            return true;
        }

        return false;
    }

    /// <summary>
    /// Returns the rate limit from the K+ api
    /// </summary>
    /// <param name="provider"></param>
    /// <param name="license"></param>
    /// <param name="accessToken"></param>
    /// <param name="ct"></param>
    /// <returns></returns>
    private async Task<int> GetRateLimit(ScrobbleProvider provider, string license, string accessToken, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(accessToken)) return 0;

        var result = await _kavitaPlusApiService.GetRateLimitForProviderAsync(provider, accessToken, license, ct);
        return result.IsSuccess ? result.Data : 0;
    }

    #region Scrobble process (Requests to K+)

    /// <summary>
    /// Retrieve all events for which the series has not errored, then delete all current errors
    /// </summary>
    private async Task<ScrobbleSyncContext> PrepareScrobbleContext(CancellationToken ct)
    {
        var erroredSeries = (await _unitOfWork.ScrobbleRepository.GetScrobbleErrors(ct))
            .Where(e => e.Comment is "Unknown Series" or UnknownSeriesErrorMessage or AccessTokenErrorMessage)
            .Select(e => e.SeriesId)
            .ToList();

        var readEvents = (await _unitOfWork.ScrobbleRepository.GetByEvent(ScrobbleEventType.ChapterRead, ct: ct))
            .Where(e => !erroredSeries.Contains(e.SeriesId))
            .ToList();
        var addToWantToRead = (await _unitOfWork.ScrobbleRepository.GetByEvent(ScrobbleEventType.AddWantToRead, ct: ct))
            .Where(e => !erroredSeries.Contains(e.SeriesId))
            .ToList();
        var removeWantToRead = (await _unitOfWork.ScrobbleRepository.GetByEvent(ScrobbleEventType.RemoveWantToRead, ct: ct))
            .Where(e => !erroredSeries.Contains(e.SeriesId))
            .ToList();
        var ratingEvents = (await _unitOfWork.ScrobbleRepository.GetByEvent(ScrobbleEventType.ScoreUpdated, ct: ct))
            .Where(e => !erroredSeries.Contains(e.SeriesId))
            .ToList();
        var reviewEvents = (await _unitOfWork.ScrobbleRepository.GetByEvent(ScrobbleEventType.Review, ct: ct))
            .Where(e => !erroredSeries.Contains(e.SeriesId))
            .ToList();
        var readStatusEvents = (await _unitOfWork.ScrobbleRepository.GetByEvent(ScrobbleEventType.ReadStatusUpdate, ct: ct))
            .Where(e => !erroredSeries.Contains(e.SeriesId))
            .ToList();

        return new ScrobbleSyncContext
        {
            ReadEvents = readEvents,
            RatingEvents = ratingEvents,
            AddToWantToRead = addToWantToRead,
            RemoveWantToRead = removeWantToRead,
            ReviewEvents = reviewEvents,
            ReadStatusEvents = readStatusEvents,
            Decisions = CalculateNetWantToReadDecisions(addToWantToRead, removeWantToRead),
            RateProfiles = AllScrobbleProviders.ToDictionary(
                p => p,
                p => _serviceProvider.GetRequiredKeyedService<IScrobbleProviderService>(p).RateProfile),
            License = (await _unitOfWork.SettingsRepository.GetSettingAsync(ServerSettingKey.LicenseKey, ct)).Value,
        };
    }

    /// <summary>
    /// Filters users who can scrobble, sets their rate limit and updates the <see cref="ScrobbleSyncContext.Users"/>
    /// </summary>
    /// <param name="ctx"></param>
    /// <param name="ct"></param>
    /// <returns></returns>
    private async Task PrepareUsersToScrobble(ScrobbleSyncContext ctx, CancellationToken ct)
    {
        // For all userIds, ensure that we can connect and have access
        var usersToScrobble = ctx.GetEventsToProcess()
            .Select(u => u.AppUser)
            .DistinctBy(u => u.Id)
            .ToList();

        foreach (var user in usersToScrobble)
        {
            await SetAndCheckRateLimit(ctx, user, ct);
        }

        ctx.Users = usersToScrobble;
    }

    /// <summary>
    /// Cleans up any events that are due to bugs or legacy
    /// </summary>
    private async Task CleanupOldOrBuggedEvents()
    {
        try
        {
            var eventsWithoutAuthenticationToken = (await _unitOfWork.ScrobbleRepository.GetEvents())
                .Where(e => e is { IsProcessed: false, IsErrored: false })
                .Where(e => e.AppUser.ScrobbleProviders.TryGetValue(e.ScrobbleProvider, out var value)
                            && string.IsNullOrEmpty(value.AuthenticationToken));

            _unitOfWork.ScrobbleRepository.Remove(eventsWithoutAuthenticationToken);
            await _unitOfWork.CommitAsync();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "There was an exception when trying to delete old scrobble events when the user has no active token");
        }
    }

    /// <summary>
    /// This is a task that is run on a fixed schedule (every few hours or every day) that clears out the scrobble event table
    /// and offloads the data to the API server which performs the syncing to the providers.
    /// </summary>
    /// <param name="ct"></param>
    [DisableConcurrentExecution(60 * 60 * 60)]
    [AutomaticRetry(Attempts = 3, OnAttemptsExceeded = AttemptsExceededAction.Delete)]
    public async Task ProcessUpdatesSinceLastSync(CancellationToken ct = default)
    {
        var ctx = await PrepareScrobbleContext(ct);
        if (ctx.TotalCount == 0) return;

        // Get all the applicable users to scrobble and set their rate limits
        await PrepareUsersToScrobble(ctx, ct);

        _logger.LogInformation("Scrobble Processing Details:" +
                               "\n  Read Events: {ReadEventsCount}" +
                               "\n  Want to Read Events: {WantToReadEventsCount}" +
                               "\n  Rating Events: {RatingEventsCount}" +
                               "\n  Review Events: {ReviewEventsCount}" +
                               "\n  Read Status Events: {ReadStatusEventCount}" +
                               "\n  Users to Scrobble: {UsersToScrobbleCount}"  +
                               "\n  Total Events to Process: {TotalEvents}",
            ctx.ReadEvents.Count,
            ctx.Decisions.Count,
            ctx.RatingEvents.Count,
            ctx.ReviewEvents.Count,
            ctx.ReadStatusEvents.Count,
            ctx.Users.Count,
            ctx.TotalCount);

        try
        {
            await ProcessReadEvents(ctx, ct);
            await ProcessRatingEvents(ctx, ct);
            await ProcessReviewEvents(ctx, ct);
            await ProcessReadStatusEvents(ctx, ct);
            await ProcessWantToReadRatingEvents(ctx, ct);
        }
        catch (FlurlHttpException ex)
        {
            _logger.LogError(ex, "Kavita+ API or a Scrobble service may be experiencing an outage. Stopping sending data");
            return;
        }


        await SaveToDb(ctx.ProgressCounter, true);
        _logger.LogInformation("Scrobbling Events is complete");

        await CleanupOldOrBuggedEvents();
    }

    [DisableConcurrentExecution(60 * 60 * 60)]
    [AutomaticRetry(Attempts = 1, OnAttemptsExceeded = AttemptsExceededAction.Delete)]
    public async Task RunReadStatusTransitionRules(CancellationToken ct = default)
    {
        var users = await _unitOfWork.UserRepository.GetAllUsersAsync(ct: ct);

        foreach (var user in users)
        {
            _logger.LogInformation("Processing Scrobble rules for User {UserName} ({UserId})", user.UserName?.Sanitize(), user.Id);
            await _ruleService.ResetReadSeriesAsync(user.Id, ct);

            foreach (var kv in user.ScrobbleProviders.Where(kv => !string.IsNullOrEmpty(kv.Value.AuthenticationToken)))
            {
                var scrobbleProviderService = _serviceProvider.GetRequiredKeyedService<IScrobbleProviderService>(kv.Key);
                var inactiveRule = kv.Value.Settings.InactiveSeriesRule;
                var droppedRule = kv.Value.Settings.DroppedSeriesRule;
                var onHoldStatus = inactiveRule.TransitionStatus;
                var droppedStatus = droppedRule.TransitionStatus;

                var inactiveHash = _ruleService.ComputeHash(inactiveRule);
                var droppedHash = _ruleService.ComputeHash(droppedRule);

                // Keys already delivered under the current config - these are skipped to avoid re-sending
                var inactiveDelivered = await _ruleService.GetDeliveredKeysAsync(user.Id, kv.Key, TransitionRuleKind.Inactive, inactiveHash, ct);
                var droppedDelivered = await _ruleService.GetDeliveredKeysAsync(user.Id, kv.Key, TransitionRuleKind.Dropped, droppedHash, ct);

                if (scrobbleProviderService is ISeriesScrobbleService)
                {
                    var onHoldSeries =
                        (await _unitOfWork.SeriesRepository.GetSeriesForReadStatusTransitionRuleAsync(user.Id,
                            inactiveRule, false, ct))
                        .Where(s => GetProvidersForScrobbleEvent(null, ScrobbleEventType.ReadStatusUpdate, user, s).Contains(kv.Key));

                    var droppedSeries =
                        (await _unitOfWork.SeriesRepository.GetSeriesForReadStatusTransitionRuleAsync(user.Id,
                            droppedRule, true, ct))
                        .Where(s => GetProvidersForScrobbleEvent(null, ScrobbleEventType.ReadStatusUpdate, user, s).Contains(kv.Key));



                    foreach (var series in onHoldSeries)
                    {
                        if (inactiveDelivered.Contains((series.Id, null))) continue;
                        var ctx = new ScrobbleUpdateContext { User = user, Series = series };

                        await scrobbleProviderService.ScrobbleReadStatusUpdates(ctx, onHoldStatus, TransitionRuleKind.Inactive, inactiveHash, ct);
                    }

                    foreach (var series in droppedSeries)
                    {
                        if (droppedDelivered.Contains((series.Id, null))) continue;
                        var ctx = new ScrobbleUpdateContext { User = user, Series = series };

                        await scrobbleProviderService.ScrobbleReadStatusUpdates(ctx, droppedStatus, TransitionRuleKind.Dropped, droppedHash, ct);
                    }

                    continue;
                }

                var onHoldChapters = (await _unitOfWork.ChapterRepository.GetChaptersForReadStatusTransitionRuleAsync(user.Id,
                    inactiveRule, ct))
                    .Where(c => GetProvidersForScrobbleEvent(null, ScrobbleEventType.ReadStatusUpdate, user, c.Volume.Series).Contains(kv.Key));

                var droppedChapters = (await _unitOfWork.ChapterRepository.GetChaptersForReadStatusTransitionRuleAsync(user.Id,
                        droppedRule, ct))
                    .Where(c => GetProvidersForScrobbleEvent(null, ScrobbleEventType.ReadStatusUpdate, user, c.Volume.Series).Contains(kv.Key));

                foreach (var chapter in onHoldChapters)
                {
                    if (inactiveDelivered.Contains((chapter.Volume.Series.Id, chapter.Id))) continue;
                    var ctx = new ScrobbleUpdateContext { User = user, Series = chapter.Volume.Series, Chapter = chapter};

                    await scrobbleProviderService.ScrobbleReadStatusUpdates(ctx, onHoldStatus, TransitionRuleKind.Inactive, inactiveHash, ct);
                }

                foreach (var chapter in droppedChapters)
                {
                    if (droppedDelivered.Contains((chapter.Volume.Series.Id, chapter.Id))) continue;
                    var ctx = new ScrobbleUpdateContext { User = user, Series = chapter.Volume.Series, Chapter = chapter };

                    await scrobbleProviderService.ScrobbleReadStatusUpdates(ctx, droppedStatus, TransitionRuleKind.Dropped, droppedHash, ct);
                }
            }
        }

        _logger.LogInformation("Scrobble rules completed processing");
    }


    /// <summary>
    /// Calculates the net want-to-read decisions by considering all events.
    /// Returns events that represent the final state for each user/series pair.
    /// </summary>
    /// <param name="addEvents">List of events for adding to want-to-read</param>
    /// <param name="removeEvents">List of events for removing from want-to-read</param>
    /// <returns>List of events that represent the final state (add or remove)</returns>
    private static List<ScrobbleEvent> CalculateNetWantToReadDecisions(List<ScrobbleEvent> addEvents, List<ScrobbleEvent> removeEvents)
    {
        // Create a dictionary to track the latest event for each user/series combination
        var latestEvents = new Dictionary<(int SeriesId, int? ChapterID, int AppUserId, ScrobbleProvider Provider), ScrobbleEvent>();

        // Process all add events
        foreach (var addEvent in addEvents)
        {
            var key = (addEvent.SeriesId, addEvent.ChapterId, addEvent.AppUserId, addEvent.ScrobbleProvider);

            if (latestEvents.TryGetValue(key, out var value) && addEvent.CreatedUtc <= value.CreatedUtc) continue;

            value = addEvent;
            latestEvents[key] = value;
        }

        // Process all remove events
        foreach (var removeEvent in removeEvents)
        {
            var key = (removeEvent.SeriesId, removeEvent.ChapterId, removeEvent.AppUserId, removeEvent.ScrobbleProvider);

            if (latestEvents.TryGetValue(key, out var value) && removeEvent.CreatedUtc <= value.CreatedUtc) continue;

            value = removeEvent;
            latestEvents[key] = value;
        }

        // Return all events that represent the final state
        return latestEvents.Values.ToList();
    }

    private async Task ProcessWantToReadRatingEvents(ScrobbleSyncContext ctx, CancellationToken ct)
    {
        await ProcessEvents(ctx.Decisions, ctx, evt => Task.FromResult(new ScrobbleV3Dto
            {
                Provider = evt.ScrobbleProvider,
                AuthenticationToken = null,
                Format = evt.Format,
                AniListId = evt.AniListId,
                MalId = (int?) evt.MalId,
                MangabakaId = evt.MangabakaId,
                HardcoverId = evt.HardcoverId,
                ScrobbleEventType = evt.ScrobbleEventType,
                ChapterNumber = evt.ChapterNumber,
                VolumeNumber = (int?) evt.VolumeNumber,
                SeriesName = evt.Series.Name,
                Year = evt.Series.Metadata.ReleaseYear
            }), ct);
    }

    private async Task ProcessRatingEvents(ScrobbleSyncContext ctx, CancellationToken ct)
    {
        await ProcessEvents(ctx.RatingEvents, ctx, evt => Task.FromResult(new ScrobbleV3Dto
            {
                Provider = evt.ScrobbleProvider,
                AuthenticationToken = null,
                Format = evt.Format,
                AniListId = evt.AniListId,
                MalId = (int?) evt.MalId,
                MangabakaId = evt.MangabakaId,
                HardcoverId = evt.HardcoverId,
                ScrobbleEventType = evt.ScrobbleEventType,
                SeriesName = evt.Series.Name,
                Rating = evt.Rating,
                Year = evt.Series.Metadata.ReleaseYear
            }), ct);
    }

    private async Task ProcessReviewEvents(ScrobbleSyncContext ctx, CancellationToken ct)
    {
        await ProcessEvents(ctx.ReviewEvents, ctx, evt =>
        {
            var scrobbleSettings = ctx.Users
                .FirstOrDefault(u => u.Id == evt.AppUserId)?
                .ScrobbleProviders[evt.ScrobbleProvider];

            return Task.FromResult(new ScrobbleV3Dto
            {
                Provider = evt.ScrobbleProvider,
                AuthenticationToken = null,
                Format = evt.Format,
                AniListId = evt.AniListId,
                MalId = (int?)evt.MalId,
                MangabakaId = evt.MangabakaId,
                HardcoverId = evt.HardcoverId,
                ScrobbleEventType = evt.ScrobbleEventType,
                SeriesName = evt.Series.Name,
                Year = evt.Series.Metadata.ReleaseYear,
                ReviewBody = evt.ReviewBody,
                ReviewTitle = evt.ReviewTitle,
                ReviewScrobbleTarget = scrobbleSettings?.Settings.ReviewScrobbleTarget
            });
        }, ct);
    }

    private async Task ProcessReadStatusEvents(ScrobbleSyncContext ctx, CancellationToken ct)
    {
        await ProcessEvents(ctx.ReadStatusEvents, ctx, evt =>
        {
            var scrobbleSettings = ctx.Users
                .FirstOrDefault(u => u.Id == evt.AppUserId)?
                .ScrobbleProviders[evt.ScrobbleProvider];

            return Task.FromResult(new ScrobbleV3Dto
            {
                Provider = evt.ScrobbleProvider,
                AuthenticationToken = null,
                Format = evt.Format,
                AniListId = evt.AniListId,
                MalId = (int?)evt.MalId,
                MangabakaId = evt.MangabakaId,
                HardcoverId = evt.HardcoverId,
                ScrobbleEventType = evt.ScrobbleEventType,
                SeriesName = evt.Series.Name,
                Year = evt.Series.Metadata.ReleaseYear,
                ReadStatus = evt.ReadStatus,
                ReviewScrobbleTarget = scrobbleSettings?.Settings.ReviewScrobbleTarget
            });
        }, ct);
    }

    private async Task ProcessReadEvents(ScrobbleSyncContext ctx, CancellationToken ct)
    {
        // Recalculate the highest volume/chapter for non chapter events
        foreach (var readEvt in ctx.ReadEvents.Where(e => e.ChapterId is null))
        {
            // Note: this causes skewing in the scrobble history because it makes it look like there are duplicate events
            readEvt.VolumeNumber =
                (int) await _unitOfWork.AppUserProgressRepository.GetHighestFullyReadVolumeForSeries(readEvt.SeriesId,
                    readEvt.AppUser.Id, ct);
            readEvt.ChapterNumber =
                await _unitOfWork.AppUserProgressRepository.GetHighestFullyReadChapterForSeries(readEvt.SeriesId,
                    readEvt.AppUser.Id, ct);
            _unitOfWork.ScrobbleRepository.Update(readEvt);
        }

        await ProcessEvents(ctx.ReadEvents, ctx, async evt => new ScrobbleV3Dto
        {
            Provider = evt.ScrobbleProvider,
            AuthenticationToken = null,
            Format = evt.Format,
            AniListId = evt.AniListId,
            MalId = (int?)evt.MalId,
            MangabakaId = evt.MangabakaId,
            HardcoverId = evt.HardcoverId,
            ScrobbleEventType = evt.ScrobbleEventType,
            ChapterNumber = evt.ChapterNumber,
            VolumeNumber = (int?)evt.VolumeNumber,
            PercentRead = (int?)evt.Progress,
            TotalReadCountForSeries = ((await _unitOfWork.SeriesRepository.GetSeriesDtoByIdAsync(evt.SeriesId, evt.AppUserId, ct: ct))!).TotalReads,
            SeriesName = evt.Series.Name,
            ScrobbleDateUtc = evt.LastModifiedUtc,
            Year = evt.Series.Metadata.ReleaseYear,
            StartedReadingDateUtc = await _unitOfWork.AppUserProgressRepository.GetFirstProgressForSeries(evt.SeriesId,
                evt.AppUser.Id, ct),
            LatestReadingDateUtc = await _unitOfWork.AppUserProgressRepository.GetLatestProgressForSeries(evt.SeriesId,
                evt.AppUser.Id, ct),
        }, ct);
    }

    /// <summary>
    /// Returns true if the user token is valid
    /// </summary>
    /// <param name="user"></param>
    /// <param name="evt"></param>
    /// <returns></returns>
    /// <remarks>If the token is not, adds a scrobble error</remarks>
    private async Task<bool> ValidateUserToken(AppUser user, ScrobbleEvent evt)
    {
        var providerService = _serviceProvider.GetRequiredKeyedService<IScrobbleProviderService>(evt.ScrobbleProvider);
        var userProvider = user.ScrobbleProviders[evt.ScrobbleProvider];

        if (providerService.IsTokenValid(userProvider.AuthenticationToken))
        {
            return true;
        }

        _unitOfWork.ScrobbleRepository.Attach(new ScrobbleError
        {
            Comment = $"{evt.ScrobbleProvider} token has expired and needs rotating. Scrobbling wont work until then",
            Details = $"User: {evt.AppUser.UserName}, Expired: {userProvider.ValidUntilUtc}",
            LibraryId = evt.LibraryId,
            SeriesId = evt.SeriesId
        });
        await _unitOfWork.CommitAsync();

        await _auditService.LogScrobbleAsync(KavitaPlusEventType.ScrobbleEventFailed, evt.SeriesId,
            ToAuditParams(evt), AuditStatus.Failure, "token-expired", evt.AppUserId);
        return false;
    }

    /// <summary>
    /// Returns true if the series can be scrobbled
    /// </summary>
    /// <param name="evt"></param>
    /// <returns></returns>
    /// <remarks>If the series cannot be scrobbled, adds a scrobble error</remarks>
    private async Task<bool> ValidateSeriesCanBeScrobbled(ScrobbleEvent evt)
    {
        if (evt.Series is { IsBlacklisted: false, DontMatch: false })
            return true;

        _logger.LogInformation("Series {SeriesName} ({SeriesId}) can't be matched and thus cannot scrobble this event",
            evt.Series.Name, evt.SeriesId);

        _unitOfWork.ScrobbleRepository.Attach(new ScrobbleError
        {
            Comment = UnknownSeriesErrorMessage,
            Details = $"User: {evt.AppUser.UserName} Series: {evt.Series.Name}",
            LibraryId = evt.LibraryId,
            SeriesId = evt.SeriesId
        });

        evt.SetErrorMessage(UnknownSeriesErrorMessage);
        evt.ProcessDateUtc = DateTime.UtcNow;
        _unitOfWork.ScrobbleRepository.Update(evt);
        await _unitOfWork.CommitAsync();
        await _auditService.LogScrobbleAsync(KavitaPlusEventType.ScrobbleEventFailed, evt.SeriesId,
            ToAuditParams(evt), AuditStatus.Failure, "unknown-series", evt.AppUserId);
        return false;
    }

    /// <summary>
    /// Removed Special parses numbers from chatter and volume numbers
    /// </summary>
    /// <param name="data"></param>
    /// <returns></returns>
    private static ScrobbleV3Dto NormalizeScrobbleData(ScrobbleV3Dto data)
    {
        // We need to handle the encoding and changing it to the old one until the API layer is updated to handle these
        if (data.VolumeNumber is Parser.SpecialVolumeNumber or Parser.DefaultChapterNumber)
        {
            data.VolumeNumber = 0;
        }


        if (data.ChapterNumber is Parser.DefaultChapterNumber)
        {
            data.ChapterNumber = 0;
        }


        return data;
    }

    /// <summary>
    /// Projects the original payload of a <see cref="ScrobbleEvent"/> into an audit params DTO so skipped/failed
    /// entries retain the rating, progress, volume/chapter, etc. that the event would have sent.
    /// </summary>
    private static AuditLogScrobbleParamsDto ToAuditParams(ScrobbleEvent evt) => new()
    {
        Provider = evt.ScrobbleProvider,
        ScrobbleEventType = evt.ScrobbleEventType,
        ChapterNumber = evt.ChapterNumber,
        VolumeNumber = evt.VolumeNumber,
        PercentRead = evt.Progress,
        Rating = evt.Rating,
        ReviewBody = evt.ReviewBody,
        ReadStatus = evt.ReadStatus ?? default,
        LibraryType = evt.Series?.Library?.Type ?? LibraryType.Manga,
        TransitionRuleKind = evt.TransitionRuleKind,
    };

    /// <summary>
    /// Loops through all events, and post them to K+
    /// </summary>
    /// <param name="events"></param>
    /// <param name="ctx"></param>
    /// <param name="createEvent"></param>
    /// <param name="ct"></param>
    private async Task ProcessEvents(IEnumerable<ScrobbleEvent> events, ScrobbleSyncContext ctx, Func<ScrobbleEvent, Task<ScrobbleV3Dto>> createEvent, CancellationToken ct)
    {
        // Process backfill events last
        var eventList = events
            .OrderBy(evt => evt.IsBackFill)
            .ToList();

        if (eventList.Count == 0) return;

        foreach (var evt in eventList.Where(e => !CanProcessScrobbleEvent(e)))
        {
            await _auditService.LogScrobbleAsync(KavitaPlusEventType.ScrobbleEventSkipped, evt.SeriesId,
                ToAuditParams(evt), AuditStatus.Info, userId: evt.AppUserId, ct: ct);
        }

        foreach (var evt in eventList.Where(CanProcessScrobbleEvent))
        {
            var user = ctx.Users.FirstOrDefault(u => u.Id == evt.AppUserId);
            if (user is null)
            {
                _logger.LogError("Event for unknown user, skipping");
                continue;
            }

            _logger.LogDebug("Processing Scrobble Events: {Count} / {Total}", ctx.ProgressCounter, ctx.TotalCount);
            ctx.ProgressCounter++;

            if (!await ValidateUserToken(user, evt)) continue;
            if (!await ValidateSeriesCanBeScrobbled(evt)) continue;

            var gate = ctx.GetRateGate(evt);

            if (!gate.HasRateLeft())
            {
                _logger.LogDebug("Skipped processing Scrobble event due to premature rate exceeded, provider: {Provider}", evt.ScrobbleProvider);
                await _auditService.LogScrobbleAsync(KavitaPlusEventType.ScrobbleEventSkipped, evt.SeriesId,
                    ToAuditParams(evt), AuditStatus.Failure, "rate-limit-hit", userId: evt.AppUserId, ct: ct);

                continue;
            }

            // Pace requests for this provider's scope (only the remainder this scope still owes),
            // then honor the global floor so we don't burst the K+ proxy across all providers/users
            await Task.Delay(gate.GetWaitTime(), ct);
            await Task.Delay(ctx.GetGlobalFloorWait(), ct);

            try
            {
                var data = NormalizeScrobbleData(await createEvent(evt));

                data.AuthenticationToken = user.ScrobbleProviders[evt.ScrobbleProvider].AuthenticationToken;

                // Arm the global floor as the request fires, so every send path (success or error) spaces the next one
                ctx.RecordGlobalRequest();
                var rateLeft = await PostScrobbleUpdate(data, ctx.License, evt);
                gate.RecordResult(rateLeft);

                evt.IsProcessed = true;
                evt.ProcessDateUtc = DateTime.UtcNow;
                _unitOfWork.ScrobbleRepository.Update(evt);

                // Record the durable ledger row only on confirmed delivery. No-op for non-rule events.
                // Committed alongside the event update by the SaveToDb below.
                await _ruleService.RecordDeliveredAsync(evt, ct);
            }
            catch (FlurlHttpException)
            {
                // If a flurl exception occured, the API is likely down. Kill processing
                throw;
            }
            catch (KavitaException ex)
            {
                if (ex.Message.Contains("Access token is invalid"))
                {
                    _logger.LogCritical(ex, "Access Token for AppUserId: {AppUserId} needs to be regenerated/renewed to continue scrobbling", evt.AppUser.Id);
                    evt.SetErrorMessage(AccessTokenErrorMessage);
                    _unitOfWork.ScrobbleRepository.Update(evt);

                    // Ensure series with this error do not get re-processed next sync
                    _unitOfWork.ScrobbleRepository.Attach(new ScrobbleError
                    {
                        Comment = AccessTokenErrorMessage,
                        Details = $"{evt.AppUser.UserName} has an invalid access token (K+ Error)",
                        LibraryId = evt.LibraryId,
                        SeriesId = evt.SeriesId,
                    });
                }

                if (ex.Message.Contains(RateLimitHitErrorMessage))
                {
                    // Ensure we skip all remaining events for this scope this cycle
                    gate.RecordResult(0);
                }

            }
            catch (Exception ex)
            {
                /* Swallow as it's already been handled in PostScrobbleUpdate */
                _logger.LogError(ex, "Error processing event {EventId}", evt.Id);
            }

            await SaveToDb(ctx.ProgressCounter);
        }

        await SaveToDb(ctx.ProgressCounter, true);
    }

    /// <summary>
    /// Save changes every five updates
    /// </summary>
    /// <param name="progressCounter"></param>
    /// <param name="force">Ignore update count check</param>
    private async Task SaveToDb(int progressCounter, bool force = false)
    {
        if ((force || progressCounter % 5 == 0) && _unitOfWork.HasChanges())
        {
            _logger.LogDebug("Saving Scrobbling Event Processing Progress");
            await _unitOfWork.CommitAsync();
        }
    }

    /// <summary>
    /// If no errors have been logged for the given series, creates a new Unknown series error, and blacklists the series
    /// </summary>
    /// <param name="data"></param>
    /// <param name="evt"></param>
    private async Task MarkSeriesAsUnknown(ScrobbleV3Dto data, ScrobbleEvent evt)
    {
        if (await _unitOfWork.ScrobbleRepository.HasErrorForSeries(evt.SeriesId)) return;

        // Create a new ExternalMetadata entry to indicate that this is not matchable
        var series = await _unitOfWork.SeriesRepository.GetSeriesByIdAsync(evt.SeriesId, SeriesIncludes.ExternalMetadata);
        if (series == null) return;

        series.ExternalSeriesMetadata ??= new ExternalSeriesMetadata {SeriesId = evt.SeriesId};
        series.IsBlacklisted = true;
        _unitOfWork.SeriesRepository.Update(series);

        _unitOfWork.ScrobbleRepository.Attach(new ScrobbleError
        {
            Comment = UnknownSeriesErrorMessage,
            Details = data.SeriesName,
            LibraryId = evt.LibraryId,
            SeriesId = evt.SeriesId
        });
    }

    /// <summary>
    /// Makes the K+ request, and handles any exceptions that occur
    /// </summary>
    /// <param name="data">Data to send to K+</param>
    /// <param name="license">K+ license key</param>
    /// <param name="evt">Related scrobble event</param>
    /// <returns></returns>
    /// <exception cref="KavitaException">Exceptions may be rethrown as a KavitaException</exception>
    /// <remarks>Some FlurlHttpException are also rethrown</remarks>
    public async Task<int> PostScrobbleUpdate(ScrobbleV3Dto data, string license, ScrobbleEvent evt)
    {
        try
        {
            var response = await _kavitaPlusApiService.PostScrobbleV3UpdateAsync(data, license);

            _logger.LogDebug("K+ API Scrobble response for series {SeriesName}: Successful {Successful}, ErrorMessage {ErrorMessage}, ExtraInformation: {ExtraInformation}, RateLeft: {RateLeft}",
                data.SeriesName, response.Successful, response.ErrorMessage, response.ExtraInformation, response.RateLeft);

            if (response.Successful || response.ErrorMessage == null)
            {
                await _auditService.LogScrobbleAsync(KavitaPlusEventType.ScrobbleEventSent, evt.SeriesId,
                    new AuditLogScrobbleParamsDto
                    {
                        Provider = data.Provider,
                        ScrobbleEventType = data.ScrobbleEventType,
                        ChapterNumber = data.ChapterNumber,
                        VolumeNumber = data.VolumeNumber,
                        PercentRead = data.PercentRead,
                        TotalReadCountForSeries = data.TotalReadCountForSeries,
                        Rating = data.Rating,
                        ReviewBody = data.ReviewBody,
                        ReadStatus = data.ReadStatus ?? ScrobbleReadStatus.Ignore,
                        TransitionRuleKind = evt.TransitionRuleKind,
                        LibraryType = evt.Series?.Library?.Type ?? LibraryType.Manga
                    },
                    AuditStatus.Success, userId: evt.AppUserId);
                return response.RateLeft;
            }

            if (response.ErrorMessage.Contains("Too Many Requests"))
            {
                _logger.LogInformation("Hit Too many requests while posting scrobble updates, will be retried in the next cycle");
                await _auditService.LogAsync(KavitaPlusAuditCategory.Scrobble, KavitaPlusEventType.ScrobbleRateLimitHit,
                    AuditStatus.Failure, error: "rate-limit-hit");

                throw new KavitaException(RateLimitHitErrorMessage);
            }

            if (response.ErrorMessage.Contains("Unauthorized"))
            {
                _logger.LogCritical("Kavita+ responded with Unauthorized. Please check your subscription");
                await _licenseService.HasActiveLicense(true);
                evt.SetErrorMessage(InvalidKPlusLicenseErrorMessage);
                throw new KavitaException("Kavita+ responded with Unauthorized. Please check your subscription");
            }

            if (response.ErrorMessage.Contains("Access token is invalid"))
            {
                evt.SetErrorMessage(AccessTokenErrorMessage);

                await _auditService.LogScrobbleAsync(KavitaPlusEventType.ScrobbleEventFailed, evt.SeriesId,
                    ToAuditParams(evt), AuditStatus.Failure, "invalid-token", userId: evt.AppUserId);

                throw new KavitaException("Access token is invalid");
            }

            if (response.ErrorMessage.Contains("Unknown Series"))
            {
                // Log the Series name and Id in ScrobbleErrors
                _logger.LogInformation("Kavita+ was unable to match the series: {SeriesName}", evt.Series.Name);

                await MarkSeriesAsUnknown(data, evt);
                evt.SetErrorMessage(UnknownSeriesErrorMessage);

                await _auditService.LogScrobbleAsync(KavitaPlusEventType.ScrobbleEventFailed, evt.SeriesId,
                    ToAuditParams(evt), AuditStatus.Failure, "unknown-series", userId: evt.AppUserId);

            } else if (response.ErrorMessage.StartsWith("Review"))
            {
                // Log the Series name and Id in ScrobbleErrors
                _logger.LogInformation("Kavita+ was unable to save the review");
                if (!await _unitOfWork.ScrobbleRepository.HasErrorForSeries(evt.SeriesId))
                {
                    _unitOfWork.ScrobbleRepository.Attach(new ScrobbleError()
                    {
                        Comment = response.ErrorMessage,
                        Details = data.SeriesName,
                        LibraryId = evt.LibraryId,
                        SeriesId = evt.SeriesId
                    });
                }
                evt.SetErrorMessage(ReviewFailedErrorMessage);
            }

            throw new KavitaException(response.ErrorMessage);
        }
        #pragma warning disable S2139
        catch (FlurlHttpException ex)
        #pragma warning restore S2139
        {
            var errorMessage = await ex.GetResponseStringAsync();
            // Trim quotes if the response is a JSON string
            errorMessage = errorMessage.Trim('"');

            if (errorMessage.Contains("Too Many Requests"))
            {
                _logger.LogInformation("Hit Too many requests while posting scrobble updates, will be retried in the next cycle");
                await _auditService.LogAsync(KavitaPlusAuditCategory.Scrobble, KavitaPlusEventType.ScrobbleRateLimitHit,
                    AuditStatus.Failure, error: "rate-limit-hit");

                throw new KavitaException(RateLimitHitErrorMessage);
            }

            _logger.LogError(ex, "Scrobbling to Kavita+ API failed due to error: {ErrorMessage}", ex.Message);
            if (ex.StatusCode == 500 || ex.Message.Contains("Call failed with status code 500 (Internal Server Error)"))
            {
                if (!await _unitOfWork.ScrobbleRepository.HasErrorForSeries(evt.SeriesId))
                {
                    _unitOfWork.ScrobbleRepository.Attach(new ScrobbleError()
                    {
                        Comment = UnknownSeriesErrorMessage,
                        Details = data.SeriesName,
                        LibraryId = evt.LibraryId,
                        SeriesId = evt.SeriesId
                    });
                }
                evt.SetErrorMessage(BadPayLoadErrorMessage);
                throw new KavitaException(BadPayLoadErrorMessage);
            }
            throw;
        }
    }

    #endregion

    #region BackFill

    public async Task CreateEventsFromExistingHistory(List<ScrobbleProvider> scrobbleProviders, int userId, CancellationToken ct = default)
    {
        foreach (var scrobbleProvider in scrobbleProviders)
        {
            await CreateEventsFromExistingHistory(scrobbleProvider, userId, ct);
        }
    }

    public async Task CreateEventsFromExistingHistory(ScrobbleProvider scrobbleProvider, int userId = 0,
        CancellationToken ct = default)
    {
        if (!await _licenseService.HasActiveLicense(ct: ct)) return;

        if (userId != 0)
        {
            var user = await _unitOfWork.UserRepository.GetUserByIdAsync(userId, ct: ct);
            if (user == null) return;
        }

        var userIds = (await _unitOfWork.UserRepository.GetAllUsersAsync(ct: ct))
            .Where(l => userId == 0 || userId == l.Id)
            .Where(u => !string.IsNullOrEmpty(u.ScrobbleProviders[scrobbleProvider].AuthenticationToken))
            .Select(u => u.Id);

        foreach (var uId in userIds)
        {
            await CreateEventsFromExistingHistoryForUser(scrobbleProvider, uId, ct);
        }
    }

    /// <summary>
    /// Creates wantToRead, rating, reviews, and series progress events for the suer
    /// </summary>
    /// <param name="scrobbleProvider"></param>
    /// <param name="userId"></param>
    /// <param name="ct"></param>
    private async Task CreateEventsFromExistingHistoryForUser(ScrobbleProvider scrobbleProvider, int userId, CancellationToken ct)
    {
        _logger.LogDebug("Creating events for user {UserId} from existing history for {Provider}", userId, scrobbleProvider);

        List<ScrobbleProvider> providers = [scrobbleProvider];

        var wantToRead = await _unitOfWork.SeriesRepository.GetWantToReadForUserAsync(userId, ct);
        _logger.LogTrace("Found {Count} wantToRead entries for user {UserId}", wantToRead.Count, userId);
        foreach (var wtr in wantToRead)
        {
            await ScrobbleWantToReadUpdate(providers, userId, wtr.Id, true, true, ct);
        }

        var ratings = (await _unitOfWork.UserRepository.GetSeriesWithRatings(userId, ct)).ToList();
        _logger.LogTrace("Found {Count} series ratings entries for user {UserId}", ratings.Count, userId);
        foreach (var rating in ratings)
        {
            await ScrobbleRatingUpdate(providers, userId, rating.SeriesId, null, rating.Rating, true, ct);
        }

        var chapterRatings = await _unitOfWork.UserRepository.GetChaptersWithRatings(userId, ct);
        _logger.LogTrace("Found {Count} chapter ratings entries for user {UserId}", chapterRatings.Count, userId);
        foreach (var chapterRating in chapterRatings)
        {
            await ScrobbleRatingUpdate(providers, userId, chapterRating.SeriesId, chapterRating.ChapterId, chapterRating.Rating, true, ct);
        }

        var reviews = (await _unitOfWork.UserRepository.GetSeriesWithReviews(userId, ct)).ToList();
        _logger.LogTrace("Found {Count} series reviews entries for user {UserId}", reviews.Count, userId);
        foreach (var review in reviews)
        {
            await ScrobbleReviewUpdate(providers, userId, review.SeriesId, null, string.Empty, review.Review!, true, ct);
        }

        var chapterReviews = await _unitOfWork.UserRepository.GetChaptersWithReviews(userId, ct);
        _logger.LogTrace("Found {Count} chapter reviews entries for user {UserId}", chapterReviews.Count, userId);
        foreach (var chapterReview in chapterReviews)
        {
            await ScrobbleReviewUpdate(providers, userId, chapterReview.SeriesId, chapterReview.ChapterId, string.Empty,
                chapterReview.Review!, true, ct);
        }

        var filter = new SeriesFilterV2Dto
        {
            Combination = FilterCombination.And,
            Statements =
            [
                new SeriesFilterStatementDto
                {
                    Comparison = FilterComparison.GreaterThan,
                    Field = SeriesFilterField.ReadProgress,
                    Value = "0"
                }
            ]
        };

        var seriesWithProgress =
            await _unitOfWork.SeriesRepository.GetSeriesDtoForLibraryIdAsync(userId, UserParams.Infinite, filter, ct: ct);

        _logger.LogTrace("Found {Count} series with progress entries for user {UserId}", seriesWithProgress.Count, userId);
        foreach (var series in seriesWithProgress.Where(series => series.PagesRead > 0))
        {
            await ScrobbleReadingUpdateForSeries(providers, userId, series.Id, true, ct);
        }

        var user = await _unitOfWork.UserRepository.GetUserByIdAsync(userId, ct: ct);
        if (user != null)
        {
            user.ScrobbleProviders[scrobbleProvider].ScrobbleEventGenerationRan = DateTime.UtcNow;

            await _unitOfWork.CommitAsync(ct);
        }
    }

    public async Task CreateEventsFromExistingHistoryForSeries(int seriesId, CancellationToken ct = default)
    {
        if (!await _licenseService.HasActiveLicense(ct: ct)) return;

        var series = await _unitOfWork.SeriesRepository.GetSeriesByIdAsync(seriesId, SeriesIncludes.Library, ct);
        if (series == null) return;

        _logger.LogInformation("Creating Scrobbling events for Series {SeriesName}", series.Name);

        var userIds = (await _unitOfWork.UserRepository.GetAllUsersAsync(ct: ct)).Select(u => u.Id);

        foreach (var uId in userIds)
        {
            // Handle "Want to Read" updates specific to the series
            var wantToRead = await _unitOfWork.SeriesRepository.GetWantToReadForUserAsync(uId, ct);
            foreach (var wtr in wantToRead.Where(wtr => wtr.Id == seriesId))
            {
                await ScrobbleWantToReadUpdate(uId, wtr.Id, true, ct);
            }

            // Handle ratings specific to the series
            var ratings = await _unitOfWork.UserRepository.GetSeriesWithRatings(uId, ct);
            foreach (var rating in ratings.Where(rating => rating.SeriesId == seriesId))
            {
                await ScrobbleSeriesRatingUpdate(uId, rating.SeriesId, rating.Rating, ct);
            }

            var chapterRatings = await _unitOfWork.UserRepository.GetChaptersWithRatings(uId, ct);
            foreach (var chapterRating in chapterRatings.Where(chapterRating => chapterRating.SeriesId == seriesId))
            {
                await ScrobbleChapterRatingUpdate(uId, chapterRating.SeriesId, chapterRating.ChapterId, chapterRating.Rating, ct);
            }

            // Handle review specific to the series
            var reviews = await _unitOfWork.UserRepository.GetSeriesWithReviews(uId, ct);
            foreach (var review in reviews.Where(r => r.SeriesId == seriesId && !string.IsNullOrEmpty(r.Review)))
            {
                await ScrobbleSeriesReviewUpdate(uId, review.SeriesId, string.Empty, review.Review!, ct);
            }

            var chapterReviews = await _unitOfWork.UserRepository.GetChaptersWithReviews(uId, ct);
            foreach (var chapterReview in chapterReviews.Where(r =>
                         r.SeriesId == seriesId && !string.IsNullOrEmpty(r.Review)))
            {
                await ScrobbleChapterReviewUpdate(uId, chapterReview.SeriesId, chapterReview.ChapterId,
                    string.Empty, chapterReview.Review!, ct);
            }

            // Handle progress updates for the specific series
            await ScrobbleReadingUpdateForSeries(uId, seriesId, ct);
        }
    }

    #endregion

    /// <summary>
    /// Removes all events (active) that are tied to a now-on hold series
    /// </summary>
    /// <param name="userId"></param>
    /// <param name="seriesId"></param>
    /// <param name="ct"></param>
    public async Task ClearEventsForSeries(int userId, int seriesId, CancellationToken ct = default)
    {
        _logger.LogInformation("Clearing Pre-existing Scrobble events for Series {SeriesId} by User {AppUserId} as Series is now on hold list", seriesId, userId);

        var events = await _unitOfWork.ScrobbleRepository.GetUserEventsForSeries(userId, seriesId, ct);
        _unitOfWork.ScrobbleRepository.Remove(events);
        await _unitOfWork.CommitAsync(ct);
    }

    public async Task SyncProviderInfo(int userId, ScrobbleProvider provider, CancellationToken ct = default)
    {
        _logger.LogDebug("Syncing scrobbling info for {UserId} for {Provider}", userId, provider);
        var user = await _unitOfWork.UserRepository.GetUserByIdAsync(userId, ct: ct);
        if (user == null) return;

        var license = (await _unitOfWork.SettingsRepository.GetSettingAsync(ServerSettingKey.LicenseKey, ct)).Value;
        var scrobbleProviderSettings = user.ScrobbleProviders[provider];

        scrobbleProviderSettings.LastSyncedUtc = DateTime.UtcNow;

        if (string.IsNullOrEmpty(scrobbleProviderSettings.AuthenticationToken))
        {
            scrobbleProviderSettings.ValidUntilUtc = DateTime.MinValue;
            scrobbleProviderSettings.UserName = string.Empty;

            _unitOfWork.UserRepository.Update(user);
            await _unitOfWork.CommitAsync(ct);

            await _eventHub.SendMessageToAsync(MessageFactory.ScrobbleProviderUpdated,
                MessageFactory.ScrobbleProviderUpdatedEvent(provider), userId, ct);

            return;
        }

        // TODO: Call HasTokenExpiredForProviderAsync() so we can validate the token authentication, rather than just assuming

        // MAL doesn't use JWT tokens
        if (provider != ScrobbleProvider.Mal)
        {
            var userInfo = await _kavitaPlusApiService.GetUserInfo(provider, scrobbleProviderSettings.AuthenticationToken, license, ct);
            if (!userInfo.IsSuccess)
            {
                _logger.LogWarning("Failed to sync provider info for {UserId} for {Provider} due to error: {ErrorMessage}", userId, provider, userInfo.ErrorMessage);
            }
            else
            {
                scrobbleProviderSettings.UserName = userInfo.Data!.Username;
            }

            if (provider is ScrobbleProvider.AniList or ScrobbleProvider.Hardcover)
            {
                try
                {
                    scrobbleProviderSettings.ValidUntilUtc = TokenService.GetTokenExpiry(scrobbleProviderSettings.AuthenticationToken);
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Failed to get token expiry for {UserId} for {Provider}", userId, provider);
                }
            }
            else
            {
                scrobbleProviderSettings.ValidUntilUtc = DateTime.MaxValue;
            }
        }
        else
        {
            scrobbleProviderSettings.ValidUntilUtc = DateTime.MaxValue;
        }

        _unitOfWork.UserRepository.Update(user);
        await _unitOfWork.CommitAsync(ct);

        await _eventHub.SendMessageToAsync(MessageFactory.ScrobbleProviderUpdated,
            MessageFactory.ScrobbleProviderUpdatedEvent(provider), userId, ct);
    }

    public async Task<List<int>> FilterLibrariesForProvider(ScrobbleProvider provider, int userId, List<int> libraryIds, CancellationToken ct = default)
    {
        var libraries = await _unitOfWork.LibraryRepository.GetLibrariesForUserIdAsync(userId, ct);

        return libraries
            .Where(l => IsLibraryTypeSupported(provider, l.Type))
            .Select(l => l.Id)
            .Where(libraryIds.Contains)
            .ToList();

    }


    public async Task<bool> RetryScrobbleAsync(int authUserId, KavitaPlusAuditEntryDto auditEntry, CancellationToken ct = default)
    {
        if (auditEntry.ScrobbleDetails == null) return false;
        if (auditEntry.Status != AuditStatus.Failure) return false;
        if (auditEntry.SubjectType != AuditSubjectType.Series && auditEntry.SubjectType != AuditSubjectType.Chapter) return false;

        if (!auditEntry.CanRetry) return false;

        switch (auditEntry.ScrobbleDetails!.ScrobbleEventType)
        {
            case ScrobbleEventType.ChapterRead:
                // TODO: Retrying ChapterRead events is not yet supported. Return false so the entry is not
                // marked as retried while we send nothing. Revisit and implement the retry path.
                return false;
            case ScrobbleEventType.AddWantToRead:
                if (auditEntry.SeriesId == null || auditEntry.UserId == null) return false;
                await ScrobbleWantToReadUpdate(auditEntry.UserId.Value, auditEntry.SeriesId.Value, true, ct);
                break;
            case ScrobbleEventType.RemoveWantToRead:
                if (auditEntry.SeriesId == null || auditEntry.UserId == null) return false;
                await ScrobbleWantToReadUpdate(auditEntry.UserId.Value, auditEntry.SeriesId.Value, false, ct);
                break;
            case ScrobbleEventType.ScoreUpdated:
                if (auditEntry.SeriesId == null || auditEntry.UserId == null) return false;

                if (auditEntry.SubjectType == AuditSubjectType.Chapter)
                {
                    await ScrobbleChapterRatingUpdate(auditEntry.UserId.Value, auditEntry.SeriesId.Value, auditEntry.SubjectId!.Value, auditEntry.ScrobbleDetails.Rating ?? 0f, ct);
                }
                else
                {
                    await ScrobbleSeriesRatingUpdate(auditEntry.UserId.Value, auditEntry.SeriesId.Value, auditEntry.ScrobbleDetails.Rating ?? 0f, ct);
                }
                break;
            case ScrobbleEventType.Review:
                if (auditEntry.SeriesId == null || auditEntry.UserId == null) return false;

                if (auditEntry is {SubjectType: AuditSubjectType.Chapter, SubjectId: not null})
                {
                    var chapterRating = await _unitOfWork.UserRepository.GetUserChapterRatingAsync(auditEntry.UserId.Value, auditEntry.SubjectId.Value, ct);

                    if (!string.IsNullOrEmpty(chapterRating?.Review))
                    {
                        await ScrobbleChapterReviewUpdate(auditEntry.UserId.Value, auditEntry.SeriesId.Value, auditEntry.SubjectId.Value,
                            string.Empty, chapterRating.Review, ct);
                    }
                }
                else
                {
                    var seriesRating = await _unitOfWork.UserRepository.GetUserRatingAsync(auditEntry.SeriesId.Value, auditEntry.UserId.Value, ct);
                    if (!string.IsNullOrEmpty(seriesRating?.Review))
                    {
                        await ScrobbleSeriesReviewUpdate(auditEntry.UserId.Value, auditEntry.SeriesId.Value,
                            string.Empty, seriesRating.Review, ct);
                    }
                }
                break;
            default:
                return false;
        }

        await _unitOfWork.KavitaPlusAuditRepository.MarkAsRetriedAsync(auditEntry.Id, ct);
        return true;
    }

    /// <summary>
    /// Removes all events that have been processed that are 7 days old
    /// </summary>
    /// <param name="ct"></param>
    [DisableConcurrentExecution(60 * 60 * 60)]
    [AutomaticRetry(Attempts = 3, OnAttemptsExceeded = AttemptsExceededAction.Delete)]
    public async Task ClearProcessedEvents(CancellationToken ct = default)
    {
        const int daysAgo = 7;
        var events = await _unitOfWork.ScrobbleRepository.GetProcessedEvents(daysAgo, ct);
        _unitOfWork.ScrobbleRepository.Remove(events);
        _logger.LogInformation("Removing {Count} scrobble events that have been processed {DaysAgo}+ days ago", events.Count, daysAgo);
        await _unitOfWork.CommitAsync(ct);
    }

    private static bool CanProcessScrobbleEvent(ScrobbleEvent readEvent)
    {
        var userProviders = GetUserProviders(readEvent.AppUser);
        switch (readEvent.Series.Library.Type)
        {
            case LibraryType.Manga when MangaProviders.Intersect(userProviders).Any():
            case LibraryType.Comic when ComicProviders.Intersect(userProviders).Any():
            case LibraryType.Book when BookProviders.Intersect(userProviders).Any():
            case LibraryType.LightNovel when LightNovelProviders.Intersect(userProviders).Any():
                return true;
            default:
                return false;
        }
    }

    private static List<ScrobbleProvider> GetUserProviders(AppUser appUser)
    {
        return appUser.ScrobbleProviders
            .Where(kv => !string.IsNullOrEmpty(kv.Value.AuthenticationToken))
            .Select(kv => kv.Key)
            .ToList();
    }

    private async Task SetAndCheckRateLimit(ScrobbleSyncContext ctx, AppUser user, CancellationToken ct)
    {
        var allUserProviders = ctx.ProvidersForUser(user.Id);

        var providersToCheck = user.ScrobbleProviders
            .Where(kv => !string.IsNullOrEmpty(kv.Value.AuthenticationToken))
            .Where(kv => allUserProviders.Contains(kv.Key))
            .ToList();

        foreach (var kv in providersToCheck)
        {
            var gate = ctx.GetRateGate(user.Id, kv.Key);

            // Server-scoped providers share one gate across users; only fetch the budget once
            if (gate.IsSeeded) continue;

            var result = await GetRateLimit(kv.Key, ctx.License, kv.Value.AuthenticationToken, ct);
            gate.Seed(result);

            if (result == 0)
            {
                _logger.LogWarning("User {UserName} has no remaining rate limit for {Provider}", user.UserName, kv.Key);
            }
        }
    }

}
