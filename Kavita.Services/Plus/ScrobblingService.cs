using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using System.Net.Http;
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
using Kavita.Common.Helpers;
using Kavita.Models.DTOs.Filtering.v2;
using Kavita.Models.DTOs.Filtering.v2.Requests;
using Kavita.Models.DTOs.KavitaPlus;
using Kavita.Models.DTOs.Scrobbling;
using Kavita.Models.DTOs.SignalR;
using Kavita.Models.Entities;
using Kavita.Models.Entities.Enums;
using Kavita.Models.Entities.Enums.Audit;
using Kavita.Models.Entities.Metadata;
using Kavita.Models.Entities.Scrobble;
using Kavita.Models.Entities.User;
using Kavita.Models.Extensions;
using Kavita.Services.Helpers;
using Kavita.Services.Scanner;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace Kavita.Services.Plus;

/// <summary>
/// Context used when syncing scrobble events. Do NOT reuse between syncs
/// </summary>
public class ScrobbleSyncContext
{
    public required List<ScrobbleEvent> ReadEvents {get; init;}
    public required List<ScrobbleEvent> RatingEvents {get; init;}
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
    /// Maps userId to left over request amount
    /// </summary>
    public required Dictionary<int, int> RateLimits { get; init; }

    /// <summary>
    /// All users being scrobbled for
    /// </summary>
    public List<AppUser> Users { get; set; } = [];
    /// <summary>
    /// Amount of already processed events
    /// </summary>
    public int ProgressCounter { get; set; }

    /// <summary>
    /// Sum of all events to process
    /// </summary>
    public int TotalCount => ReadEvents.Count + RatingEvents.Count + AddToWantToRead.Count + RemoveWantToRead.Count;
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
    private readonly IKavitaPlusAuditService _auditService;

    public const string AniListWeblinkWebsite = ScrobblingHelper.AniListWeblinkWebsite;
    public const string MalWeblinkWebsite = ScrobblingHelper.MalWeblinkWebsite;
    public const string MalStaffWebsite = ScrobblingHelper.MalStaffWebsite;
    public const string MalCharacterWebsite = ScrobblingHelper.MalCharacterWebsite;
    public const string GoogleBooksWeblinkWebsite = ScrobblingHelper.GoogleBooksWeblinkWebsite;
    public const string MangaDexWeblinkWebsite = ScrobblingHelper.MangaDexWeblinkWebsite;
    public const string AniListStaffWebsite = ScrobblingHelper.AniListStaffWebsite;
    public const string AniListCharacterWebsite = ScrobblingHelper.AniListCharacterWebsite;
    public const string HardcoverStaffWebsite = ScrobblingHelper.HardcoverStaffWebsite;

    private const int ScrobbleSleepTime = 1000; // We can likely tie this to AniList's 90 rate / min ((60 * 1000) / 90)
    private const SeriesIncludes ScrobbleSeriesIncludes = SeriesIncludes.Library | SeriesIncludes.ExternalMetadata | SeriesIncludes.Metadata;

    private static readonly IList<ScrobbleProvider> BookProviders = [];
    private static readonly IList<ScrobbleProvider> LightNovelProviders =
    [
        ScrobbleProvider.AniList
    ];
    private static readonly IList<ScrobbleProvider> ComicProviders = [];
    private static readonly IList<ScrobbleProvider> MangaProviders = (List<ScrobbleProvider>)
        [ScrobbleProvider.AniList];


    private const string UnknownSeriesErrorMessage = "Series cannot be matched for Scrobbling";
    private const string AccessTokenErrorMessage = "Access Token needs to be rotated to continue scrobbling";
    private const string InvalidKPlusLicenseErrorMessage = "Kavita+ subscription no longer active";
    private const string ReviewFailedErrorMessage = "Review was unable to be saved due to upstream requirements";
    private const string BadPayLoadErrorMessage = "Bad payload from Scrobble Provider";


    public ScrobblingService(IUnitOfWork unitOfWork, IEventHub eventHub, ILogger<ScrobblingService> logger,
        ILicenseService licenseService, ILocalizationService localizationService, IEmailService emailService,
        IKavitaPlusApiService kavitaPlusApiService, IKavitaPlusAuditService auditService)
    {
        _unitOfWork = unitOfWork;
        _eventHub = eventHub;
        _logger = logger;
        _licenseService = licenseService;
        _localizationService = localizationService;
        _emailService = emailService;
        _kavitaPlusApiService = kavitaPlusApiService;
        _auditService = auditService;

        FlurlConfiguration.ConfigureClientForUrl(Configuration.KavitaPlusApiUrl);
    }

    #region Access token checks

    /// <summary>
    /// An automated job that will run against all user's tokens and validate if they are still active
    /// </summary>
    /// <param name="ct"></param>
    /// <remarks>This service can validate without license check as the task which calls will be guarded</remarks>
    /// <returns></returns>
    public async Task CheckExternalAccessTokens(CancellationToken ct = default)
    {
        // Validate AniList
        var users = await _unitOfWork.UserRepository.GetAllUsersAsync(ct: ct);
        foreach (var user in users)
        {
            if (string.IsNullOrEmpty(user.AniListAccessToken)) continue;

            var tokenExpiry = JwtHelper.GetTokenExpiry(user.AniListAccessToken);

            // Send early reminder 5 days before token expiry
            if (await ShouldSendEarlyReminder(user.Id, tokenExpiry))
            {
                await _emailService.SendTokenExpiringSoonEmail(user.Id, ScrobbleProvider.AniList);
            }

            // Send expiration notification after token expiry
            if (await ShouldSendExpirationReminder(user.Id, tokenExpiry))
            {
                await _emailService.SendTokenExpiredEmail(user.Id, ScrobbleProvider.AniList);
            }

            // Check token validity
            if (JwtHelper.IsTokenValid(user.AniListAccessToken)) continue;

            _logger.LogInformation(
                "User {UserName}'s AniList token has expired or is expiring in a few days! They need to regenerate it for scrobbling to work",
                user.UserName);

            // Notify user via event
            await _eventHub.SendMessageToAsync(
                MessageFactory.ScrobblingKeyExpired,
                MessageFactory.ScrobblingKeyExpiredEvent(ScrobbleProvider.AniList),
                user.Id, ct);

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
        var token = await GetTokenForProvider(userId, provider);

        if (string.IsNullOrEmpty(token)) return true;

        return await HasTokenExpired(token, provider);
    }

    private async Task<bool> HasTokenExpired(string token, ScrobbleProvider provider)
    {
        if (string.IsNullOrEmpty(token) || !TokenService.HasTokenExpired(token)) return false;

        var license = await _unitOfWork.SettingsRepository.GetSettingAsync(ServerSettingKey.LicenseKey);
        if (string.IsNullOrEmpty(license.Value)) return true;

        try
        {
            return await _kavitaPlusApiService.HasTokenExpiredAsync(license.Value, token, provider);
        }
        catch (HttpRequestException e)
        {
            _logger.LogError(e, "An error happened during the request to Kavita+ API");
        }
        catch (Exception e)
        {
            _logger.LogError(e, "An error happened during the request to Kavita+ API");
        }

        return true;
    }

    private async Task<string> GetTokenForProvider(int userId, ScrobbleProvider provider)
    {
        var user = await _unitOfWork.UserRepository.GetUserByIdAsync(userId);
        if (user == null) return string.Empty;

        return provider switch
        {
            ScrobbleProvider.AniList => user.AniListAccessToken,
            _ => string.Empty
        } ?? string.Empty;
    }

    #endregion

    #region Scrobble ingest

    public Task ScrobbleReviewUpdate(int userId, int seriesId, string? reviewTitle, string reviewBody,
        CancellationToken ct = default)
    {
        // Currently disabled until at least hardcover is implemented
        return Task.CompletedTask;
    }

    public async Task ScrobbleRatingUpdate(int userId, int seriesId, float rating, CancellationToken ct = default)
    {
        if (!await _licenseService.HasActiveLicense(ct: ct)) return;

        var series = await _unitOfWork.SeriesRepository.GetSeriesByIdAsync(seriesId, ScrobbleSeriesIncludes, ct);
        if (series == null) throw new KavitaException(await _localizationService.TranslateAsync(userId, "series-doesnt-exist"));

        var user = await _unitOfWork.UserRepository.GetUserByIdAsync(userId, AppUserIncludes.UserPreferences, ct);
        if (user == null || !user.UserPreferences.AniListScrobblingEnabled) return;

        _logger.LogInformation("Processing Scrobbling rating event for {AppUserId} on {SeriesName}", userId, series.Name);
        if (await CheckIfCannotScrobble(userId, seriesId, series)) return;

        var existingEvt = await _unitOfWork.ScrobbleRepository.GetEvent(userId, series.Id,
            ScrobbleEventType.ScoreUpdated, true, ct);
        if (existingEvt is {IsProcessed: false})
        {
            _logger.LogDebug("Overriding scrobble event for {Series} from Rating {Rating} -> {UpdatedRating}",
                existingEvt.Series.Name, existingEvt.Rating, rating);
            var prevRating = existingEvt.Rating;
            existingEvt.Rating = rating;
            _unitOfWork.ScrobbleRepository.Update(existingEvt);
            await _unitOfWork.CommitAsync(ct);
            await _auditService.LogScrobbleAsync(KavitaPlusEventType.ScrobbleEventUpdated, seriesId,
                new AuditLogScrobbleParamsDto { ScrobbleEventType = ScrobbleEventType.ScoreUpdated, Rating = rating, LibraryType = series.Library.Type },
                AuditStatus.Info, userId: userId, ct: ct);
            return;
        }

        var evt = new ScrobbleEvent()
        {
            SeriesId = series.Id,
            LibraryId = series.LibraryId,
            ScrobbleEventType = ScrobbleEventType.ScoreUpdated,
            AniListId = ScrobblingHelper.GetAniListId(series),
            MalId = ScrobblingHelper.GetMalId(series),
            AppUserId = userId,
            Format = series.Library.Type.ConvertToPlusMediaFormat(series.Format),
            Rating = rating
        };
        _unitOfWork.ScrobbleRepository.Attach(evt);
        await _unitOfWork.CommitAsync(ct);
        _logger.LogDebug("Added Scrobbling Rating update on {SeriesName} with Userid {AppUserId}", series.Name, userId);
        await _auditService.LogScrobbleAsync(KavitaPlusEventType.ScrobbleEventCreated, seriesId,
            new AuditLogScrobbleParamsDto { ScrobbleEventType = ScrobbleEventType.ScoreUpdated, Rating = rating, LibraryType = series.Library.Type },
            AuditStatus.Info, userId: userId, ct: ct);
    }

    public async Task ScrobbleReadingUpdate(int userId, int seriesId, CancellationToken ct = default)
    {
        if (!await _licenseService.HasActiveLicense(ct: ct)) return;

        var series = await _unitOfWork.SeriesRepository.GetSeriesByIdAsync(seriesId, ScrobbleSeriesIncludes, ct);
        if (series == null) throw new KavitaException(await _localizationService.TranslateAsync(userId, "series-doesnt-exist"));

        var user = await _unitOfWork.UserRepository.GetUserByIdAsync(userId, AppUserIncludes.UserPreferences, ct);
        if (user == null || !user.UserPreferences.AniListScrobblingEnabled) return;

        _logger.LogInformation("Processing Scrobbling reading event for {AppUserId} on {SeriesName}", userId, series.Name);
        if (await CheckIfCannotScrobble(userId, seriesId, series)) return;

        var isAnyProgressOnSeries = await _unitOfWork.AppUserProgressRepository.HasAnyProgressOnSeriesAsync(seriesId, userId, ct);

        var volumeNumber = (int) await _unitOfWork.AppUserProgressRepository.GetHighestFullyReadVolumeForSeries(seriesId, userId, ct);
        var chapterNumber = await _unitOfWork.AppUserProgressRepository.GetHighestFullyReadChapterForSeries(seriesId, userId, ct);

        // Check if there is an existing not yet processed event, if so update it
        var existingEvt = await _unitOfWork.ScrobbleRepository.GetEvent(userId, series.Id,
            ScrobbleEventType.ChapterRead, true, ct);

        if (existingEvt is {IsProcessed: false})
        {
            if (!isAnyProgressOnSeries)
            {
                _unitOfWork.ScrobbleRepository.Remove(existingEvt);
                await _unitOfWork.CommitAsync(ct);
                _logger.LogDebug("Removed scrobble event for {Series} as there is no reading progress", series.Name);
                return;
            }

            // We need to just update Volume/Chapter number
            var prevChapter = $"{existingEvt.ChapterNumber}";
            var prevVol = $"{existingEvt.VolumeNumber}";

            existingEvt.VolumeNumber = volumeNumber;
            existingEvt.ChapterNumber = chapterNumber;

            _unitOfWork.ScrobbleRepository.Update(existingEvt);
            await _unitOfWork.CommitAsync(ct);

            _logger.LogDebug("Overriding scrobble event for {Series} from vol {PrevVol} ch {PrevChap} -> vol {UpdatedVol} ch {UpdatedChap}",
                existingEvt.Series.Name, prevVol, prevChapter, existingEvt.VolumeNumber, existingEvt.ChapterNumber);
            await _auditService.LogScrobbleAsync(KavitaPlusEventType.ScrobbleEventUpdated, seriesId,
                new AuditLogScrobbleParamsDto { ScrobbleEventType = ScrobbleEventType.ChapterRead, ChapterNumber = existingEvt.ChapterNumber, VolumeNumber = existingEvt.VolumeNumber, LibraryType = series.Library.Type },
                AuditStatus.Info, userId: userId, ct: ct);
            return;
        }

        if (!isAnyProgressOnSeries)
        {
            // Do not create a new scrobble event if there is no progress
            return;
        }

        try
        {
            var evt = new ScrobbleEvent
            {
                SeriesId = series.Id,
                LibraryId = series.LibraryId,
                ScrobbleEventType = ScrobbleEventType.ChapterRead,
                AniListId = ScrobblingHelper.GetAniListId(series),
                MalId = ScrobblingHelper.GetMalId(series),
                AppUserId = userId,
                VolumeNumber = volumeNumber,
                ChapterNumber = chapterNumber,
                Format = series.Library.Type.ConvertToPlusMediaFormat(series.Format),
            };

            if (evt.VolumeNumber is Parser.SpecialVolumeNumber)
            {
                // We don't process Specials because they will never match on AniList
                return;
            }

            _unitOfWork.ScrobbleRepository.Attach(evt);
            await _unitOfWork.CommitAsync(ct);
            _logger.LogDebug("Added Scrobbling Read update on {SeriesName} - Volume: {VolumeNumber} Chapter: {ChapterNumber} for User: {AppUserId}", series.Name, evt.VolumeNumber, evt.ChapterNumber, userId);
            await _auditService.LogScrobbleAsync(KavitaPlusEventType.ScrobbleEventCreated, seriesId,
                new AuditLogScrobbleParamsDto { ScrobbleEventType = ScrobbleEventType.ChapterRead, ChapterNumber = evt.ChapterNumber, VolumeNumber = evt.VolumeNumber, LibraryType = series.Library.Type },
                AuditStatus.Info, userId: userId, ct: ct);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "There was an issue when saving scrobble read event");
        }
    }

    public async Task ScrobbleWantToReadUpdate(int userId, int seriesId, bool onWantToRead, CancellationToken ct = default)
    {
        if (!await _licenseService.HasActiveLicense(ct: ct)) return;

        var series = await _unitOfWork.SeriesRepository.GetSeriesByIdAsync(seriesId, ScrobbleSeriesIncludes, ct);
        if (series == null) throw new KavitaException(await _localizationService.TranslateAsync(userId, "series-doesnt-exist"));

        if (!series.Library.AllowScrobbling) return;

        var user = await _unitOfWork.UserRepository.GetUserByIdAsync(userId, AppUserIncludes.UserPreferences, ct);
        if (user == null || !user.UserPreferences.AniListScrobblingEnabled) return;

        if (await CheckIfCannotScrobble(userId, seriesId, series)) return;
        _logger.LogInformation("Processing Scrobbling want-to-read event for {AppUserId} on {SeriesName}", userId, series.Name);

        // Get existing events for this series/user
        var existingEvents = (await _unitOfWork.ScrobbleRepository.GetUserEventsForSeries(userId, seriesId, ct))
            .Where(e => new[] { ScrobbleEventType.AddWantToRead, ScrobbleEventType.RemoveWantToRead }.Contains(e.ScrobbleEventType));

        // Remove all existing want-to-read events for this series/user
        _unitOfWork.ScrobbleRepository.Remove(existingEvents);

        // Create the new event
        var evt = new ScrobbleEvent()
        {
            SeriesId = series.Id,
            LibraryId = series.LibraryId,
            ScrobbleEventType = onWantToRead ? ScrobbleEventType.AddWantToRead : ScrobbleEventType.RemoveWantToRead,
            AniListId = ScrobblingHelper.GetAniListId(series),
            MalId = ScrobblingHelper.GetMalId(series),
            AppUserId = userId,
            Format = series.Library.Type.ConvertToPlusMediaFormat(series.Format),
        };

        _unitOfWork.ScrobbleRepository.Attach(evt);
        await _unitOfWork.CommitAsync(ct);
        _logger.LogDebug("Added Scrobbling WantToRead update on {SeriesName} with Userid {AppUserId} ", series.Name, userId);
        await _auditService.LogScrobbleAsync(KavitaPlusEventType.ScrobbleEventCreated, seriesId,
            new AuditLogScrobbleParamsDto { ScrobbleEventType = evt.ScrobbleEventType, LibraryType = series.Library.Type },
            AuditStatus.Info, userId: userId, ct: ct);
    }

    #endregion

    /// <summary>
    /// Returns false if, the series is on hold or Don't Match, or when the library has scrobbling disable or not eligible
    /// </summary>
    /// <param name="userId"></param>
    /// <param name="seriesId"></param>
    /// <param name="series">Should have Library resolved</param>
    /// <returns></returns>
    private async Task<bool> CheckIfCannotScrobble(int userId, int seriesId, Series series)
    {
        if (series.DontMatch)
        {
            await _auditService.LogScrobbleAsync(KavitaPlusEventType.ScrobbleEventSkipped, seriesId,
                new AuditLogScrobbleParamsDto(), AuditStatus.Info, "series-dont-match", userId);
            return true;
        }

        if (await _unitOfWork.UserRepository.HasHoldOnSeries(userId, seriesId))
        {
            _logger.LogInformation("Series {SeriesName} is on AppUserId {AppUserId}'s hold list. Not scrobbling", series.Name, userId);
            await _auditService.LogScrobbleAsync(KavitaPlusEventType.ScrobbleEventSkipped, seriesId,
                new AuditLogScrobbleParamsDto(), AuditStatus.Info, "scrobble-hold-active", userId);
            return true;
        }

        // TODO: Double check if all callers pass with Library or not
        var library = series.Library ?? await _unitOfWork.LibraryRepository.GetLibraryForIdAsync(series.LibraryId);
        if (library is not {AllowScrobbling: true} || !ExternalMetadataService.IsPlusEligible(library.Type))
        {
            await _auditService.LogScrobbleAsync(KavitaPlusEventType.ScrobbleEventSkipped, seriesId,
                new AuditLogScrobbleParamsDto(), AuditStatus.Info, "library-scrobbling-disabled", userId);
            return true;
        }

        return false;
    }

    /// <summary>
    /// Returns the rate limit from the K+ api
    /// </summary>
    /// <param name="license"></param>
    /// <param name="aniListToken"></param>
    /// <returns></returns>
    private async Task<int> GetRateLimit(string license, string aniListToken)
    {
        if (string.IsNullOrWhiteSpace(aniListToken)) return 0;

        try
        {
            return await _kavitaPlusApiService.GetRateLimitAsync(license, aniListToken);
        }
        catch (Exception e)
        {
            _logger.LogError(e, "An error happened trying to get rate limit from Kavita+ API");
        }

        return 0;
    }

    #region Scrobble process (Requests to K+)

    /// <summary>
    /// Retrieve all events for which the series has not errored, then delete all current errors
    /// </summary>
    private async Task<ScrobbleSyncContext> PrepareScrobbleContext()
    {
        var librariesWithScrobbling = (await _unitOfWork.LibraryRepository.GetLibrariesAsync())
            .AsEnumerable()
            .Where(l => l.AllowScrobbling)
            .Select(l => l.Id)
            .ToImmutableHashSet();

        var erroredSeries = (await _unitOfWork.ScrobbleRepository.GetScrobbleErrors())
            .Where(e => e.Comment is "Unknown Series" or UnknownSeriesErrorMessage or AccessTokenErrorMessage)
            .Select(e => e.SeriesId)
            .ToList();

        var readEvents = (await _unitOfWork.ScrobbleRepository.GetByEvent(ScrobbleEventType.ChapterRead))
            .Where(e => librariesWithScrobbling.Contains(e.LibraryId))
            .Where(e => !erroredSeries.Contains(e.SeriesId))
            .ToList();
        var addToWantToRead = (await _unitOfWork.ScrobbleRepository.GetByEvent(ScrobbleEventType.AddWantToRead))
            .Where(e => librariesWithScrobbling.Contains(e.LibraryId))
            .Where(e => !erroredSeries.Contains(e.SeriesId))
            .ToList();
        var removeWantToRead = (await _unitOfWork.ScrobbleRepository.GetByEvent(ScrobbleEventType.RemoveWantToRead))
            .Where(e => librariesWithScrobbling.Contains(e.LibraryId))
            .Where(e => !erroredSeries.Contains(e.SeriesId))
            .ToList();
        var ratingEvents = (await _unitOfWork.ScrobbleRepository.GetByEvent(ScrobbleEventType.ScoreUpdated))
            .Where(e => librariesWithScrobbling.Contains(e.LibraryId))
            .Where(e => !erroredSeries.Contains(e.SeriesId))
            .ToList();

        return new ScrobbleSyncContext
        {
            ReadEvents = readEvents,
            RatingEvents = ratingEvents,
            AddToWantToRead = addToWantToRead,
            RemoveWantToRead = removeWantToRead,
            Decisions = CalculateNetWantToReadDecisions(addToWantToRead, removeWantToRead),
            RateLimits = [],
            License = (await _unitOfWork.SettingsRepository.GetSettingAsync(ServerSettingKey.LicenseKey)).Value,
        };
    }

    /// <summary>
    /// Filters users who can scrobble, sets their rate limit and updates the <see cref="ScrobbleSyncContext.Users"/>
    /// </summary>
    /// <param name="ctx"></param>
    /// <returns></returns>
    private async Task PrepareUsersToScrobble(ScrobbleSyncContext ctx)
    {
        // For all userIds, ensure that we can connect and have access
        var usersToScrobble = ctx.ReadEvents.Select(r => r.AppUser)
            .Concat(ctx.AddToWantToRead.Select(r => r.AppUser))
            .Concat(ctx.RemoveWantToRead.Select(r => r.AppUser))
            .Concat(ctx.RatingEvents.Select(r => r.AppUser))
            .Where(user => !string.IsNullOrEmpty(user.AniListAccessToken))
            .Where(user => user.UserPreferences.AniListScrobblingEnabled)
            .DistinctBy(u => u.Id)
            .ToList();

        foreach (var user in usersToScrobble)
        {
            await SetAndCheckRateLimit(ctx.RateLimits, user, ctx.License);
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
            var eventsWithoutAnilistToken = (await _unitOfWork.ScrobbleRepository.GetEvents())
                .Where(e => e is { IsProcessed: false, IsErrored: false })
                .Where(e => string.IsNullOrEmpty(e.AppUser.AniListAccessToken));

            _unitOfWork.ScrobbleRepository.Remove(eventsWithoutAnilistToken);
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
        var ctx = await PrepareScrobbleContext();
        if (ctx.TotalCount == 0) return;

        // Get all the applicable users to scrobble and set their rate limits
        await PrepareUsersToScrobble(ctx);

        _logger.LogInformation("Scrobble Processing Details:" +
                               "\n  Read Events: {ReadEventsCount}" +
                               "\n  Want to Read Events: {WantToReadEventsCount}" +
                               "\n  Rating Events: {RatingEventsCount}" +
                               "\n  Users to Scrobble: {UsersToScrobbleCount}"  +
                               "\n  Total Events to Process: {TotalEvents}",
            ctx.ReadEvents.Count,
            ctx.Decisions.Count,
            ctx.RatingEvents.Count,
            ctx.Users.Count,
            ctx.TotalCount);

        try
        {
            await ProcessReadEvents(ctx);
            await ProcessRatingEvents(ctx);
            await ProcessWantToReadRatingEvents(ctx);
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
        var latestEvents = new Dictionary<(int SeriesId, int AppUserId), ScrobbleEvent>();

        // Process all add events
        foreach (var addEvent in addEvents)
        {
            var key = (addEvent.SeriesId, addEvent.AppUserId);

            if (latestEvents.TryGetValue(key, out var value) && addEvent.CreatedUtc <= value.CreatedUtc) continue;

            value = addEvent;
            latestEvents[key] = value;
        }

        // Process all remove events
        foreach (var removeEvent in removeEvents)
        {
            var key = (removeEvent.SeriesId, removeEvent.AppUserId);

            if (latestEvents.TryGetValue(key, out var value) && removeEvent.CreatedUtc <= value.CreatedUtc) continue;

            value = removeEvent;
            latestEvents[key] = value;
        }

        // Return all events that represent the final state
        return latestEvents.Values.ToList();
    }

    private async Task ProcessWantToReadRatingEvents(ScrobbleSyncContext ctx)
    {
        await ProcessEvents(ctx.Decisions, ctx, evt => Task.FromResult(new ScrobbleDto
            {
                Format = evt.Format,
                AniListId = evt.AniListId,
                MALId = (int?) evt.MalId,
                ScrobbleEventType = evt.ScrobbleEventType,
                ChapterNumber = evt.ChapterNumber,
                VolumeNumber = (int?) evt.VolumeNumber,
                AniListToken = evt.AppUser.AniListAccessToken ?? string.Empty,
                SeriesName = evt.Series.Name,
                LocalizedSeriesName = evt.Series.LocalizedName,
                Year = evt.Series.Metadata.ReleaseYear
            }));

        // After decisions, we need to mark all the want to read and remove from want to read as completed
        var processedDecisions = ctx.Decisions.Where(d => d.IsProcessed).ToList();
        if (processedDecisions.Count > 0)
        {
            foreach (var scrobbleEvent in processedDecisions)
            {
                scrobbleEvent.IsProcessed = true;
                scrobbleEvent.ProcessDateUtc = DateTime.UtcNow;
                _unitOfWork.ScrobbleRepository.Update(scrobbleEvent);
            }
            await _unitOfWork.CommitAsync();
        }
    }

    private async Task ProcessRatingEvents(ScrobbleSyncContext ctx)
    {
        await ProcessEvents(ctx.RatingEvents, ctx, evt => Task.FromResult(new ScrobbleDto
            {
                Format = evt.Format,
                AniListId = evt.AniListId,
                MALId = (int?) evt.MalId,
                ScrobbleEventType = evt.ScrobbleEventType,
                AniListToken = evt.AppUser.AniListAccessToken ?? string.Empty,
                SeriesName = evt.Series.Name,
                LocalizedSeriesName = evt.Series.LocalizedName,
                Rating = evt.Rating,
                Year = evt.Series.Metadata.ReleaseYear
            }));
    }

    private async Task ProcessReadEvents(ScrobbleSyncContext ctx)
    {
        // Recalculate the highest volume/chapter
        foreach (var readEvt in ctx.ReadEvents)
        {
            // Note: this causes skewing in the scrobble history because it makes it look like there are duplicate events
            readEvt.VolumeNumber =
                (int) await _unitOfWork.AppUserProgressRepository.GetHighestFullyReadVolumeForSeries(readEvt.SeriesId,
                    readEvt.AppUser.Id);
            readEvt.ChapterNumber =
                await _unitOfWork.AppUserProgressRepository.GetHighestFullyReadChapterForSeries(readEvt.SeriesId,
                    readEvt.AppUser.Id);
            _unitOfWork.ScrobbleRepository.Update(readEvt);
        }

        await ProcessEvents(ctx.ReadEvents, ctx, async evt => new ScrobbleDto
            {
                Format = evt.Format,
                AniListId = evt.AniListId,
                MALId = (int?) evt.MalId,
                ScrobbleEventType = evt.ScrobbleEventType,
                ChapterNumber = evt.ChapterNumber,
                VolumeNumber = (int?) evt.VolumeNumber,
                AniListToken = evt.AppUser.AniListAccessToken  ?? string.Empty,
                SeriesName = evt.Series.Name,
                LocalizedSeriesName = evt.Series.LocalizedName,
                ScrobbleDateUtc = evt.LastModifiedUtc,
                Year = evt.Series.Metadata.ReleaseYear,
                StartedReadingDateUtc = await _unitOfWork.AppUserProgressRepository.GetFirstProgressForSeries(evt.SeriesId, evt.AppUser.Id),
                LatestReadingDateUtc = await _unitOfWork.AppUserProgressRepository.GetLatestProgressForSeries(evt.SeriesId, evt.AppUser.Id),
            });
    }

    /// <summary>
    /// Returns true if the user token is valid
    /// </summary>
    /// <param name="evt"></param>
    /// <returns></returns>
    /// <remarks>If the token is not, adds a scrobble error</remarks>
    private async Task<bool> ValidateUserToken(ScrobbleEvent evt)
    {
        if (!TokenService.HasTokenExpired(evt.AppUser.AniListAccessToken))
            return true;

        _unitOfWork.ScrobbleRepository.Attach(new ScrobbleError
        {
            Comment = "AniList token has expired and needs rotating. Scrobbling wont work until then",
            Details = $"User: {evt.AppUser.UserName}, Expired: {TokenService.GetTokenExpiry(evt.AppUser.AniListAccessToken)}",
            LibraryId = evt.LibraryId,
            SeriesId = evt.SeriesId
        });
        await _unitOfWork.CommitAsync();
        await _auditService.LogScrobbleAsync(KavitaPlusEventType.ScrobbleEventFailed, evt.SeriesId,
            new AuditLogScrobbleParamsDto { ScrobbleEventType = evt.ScrobbleEventType },
            AuditStatus.Failure, "token-expired", evt.AppUserId);
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
            new AuditLogScrobbleParamsDto { ScrobbleEventType = evt.ScrobbleEventType },
            AuditStatus.Failure, "unknown-series", evt.AppUserId);
        return false;
    }

    /// <summary>
    /// Removed Special parses numbers from chatter and volume numbers
    /// </summary>
    /// <param name="data"></param>
    /// <returns></returns>
    private static ScrobbleDto NormalizeScrobbleData(ScrobbleDto data)
    {
        // We need to handle the encoding and changing it to the old one until we can update the API layer to handle these
        // which could happen in v0.8.3
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
    /// Loops through all events, and post them to K+
    /// </summary>
    /// <param name="events"></param>
    /// <param name="ctx"></param>
    /// <param name="createEvent"></param>
    private async Task ProcessEvents(IEnumerable<ScrobbleEvent> events, ScrobbleSyncContext ctx, Func<ScrobbleEvent, Task<ScrobbleDto>> createEvent)
    {
        var eventList = events.ToList();
        foreach (var evt in eventList.Where(e => !CanProcessScrobbleEvent(e)))
        {
            await _auditService.LogScrobbleAsync(KavitaPlusEventType.ScrobbleEventSkipped, evt.SeriesId,
                new AuditLogScrobbleParamsDto { ScrobbleEventType = evt.ScrobbleEventType },
                AuditStatus.Info, userId: evt.AppUserId);
        }

        foreach (var evt in eventList.Where(CanProcessScrobbleEvent))
        {
            _logger.LogDebug("Processing Scrobble Events: {Count} / {Total}", ctx.ProgressCounter, ctx.TotalCount);
            ctx.ProgressCounter++;

            if (!await ValidateUserToken(evt)) continue;
            if (!await ValidateSeriesCanBeScrobbled(evt)) continue;

            var count = await SetAndCheckRateLimit(ctx.RateLimits, evt.AppUser, ctx.License);
            if (count == 0)
            {
                _logger.LogDebug("Skipped processing Scrobble event due to premature rate exceeded");
                await _auditService.LogScrobbleAsync(KavitaPlusEventType.ScrobbleEventSkipped, evt.SeriesId,
                    new AuditLogScrobbleParamsDto { ScrobbleEventType = evt.ScrobbleEventType },
                    AuditStatus.Failure, "rate-limit-hit", userId: evt.AppUserId);
                if (ctx.Users.Count == 1) break;
                continue;
            }

            try
            {
                var data = NormalizeScrobbleData(await createEvent(evt));

                ctx.RateLimits[evt.AppUserId] = await PostScrobbleUpdate(data, ctx.License, evt);

                evt.IsProcessed = true;
                evt.ProcessDateUtc = DateTime.UtcNow;
                _unitOfWork.ScrobbleRepository.Update(evt);
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
            }
            catch (Exception ex)
            {
                /* Swallow as it's already been handled in PostScrobbleUpdate */
                _logger.LogError(ex, "Error processing event {EventId}", evt.Id);
            }

            await SaveToDb(ctx.ProgressCounter);

            // We can use count to determine how long to sleep based on rate gain. It might be specific to AniList, but we can model others
            var delay = count > 10 ? TimeSpan.FromMilliseconds(ScrobbleSleepTime) : TimeSpan.FromSeconds(60);
            await Task.Delay(delay);
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
    private async Task MarkSeriesAsUnknown(ScrobbleDto data, ScrobbleEvent evt)
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
    public async Task<int> PostScrobbleUpdate(ScrobbleDto data, string license, ScrobbleEvent evt)
    {
        try
        {
            var response = await _kavitaPlusApiService.PostScrobbleUpdateAsync(data, license);

            _logger.LogDebug("K+ API Scrobble response for series {SeriesName}: Successful {Successful}, ErrorMessage {ErrorMessage}, ExtraInformation: {ExtraInformation}, RateLeft: {RateLeft}",
                data.SeriesName, response.Successful, response.ErrorMessage, response.ExtraInformation, response.RateLeft);

            if (response.Successful || response.ErrorMessage == null)
            {
                await _auditService.LogScrobbleAsync(KavitaPlusEventType.ScrobbleEventSent, evt.SeriesId,
                    new AuditLogScrobbleParamsDto { ScrobbleEventType = data.ScrobbleEventType, ChapterNumber = data.ChapterNumber, VolumeNumber = data.VolumeNumber, Rating = data.Rating, LibraryType = evt.Series?.Library?.Type ?? LibraryType.Manga },
                    AuditStatus.Success, userId: evt.AppUserId);
                return response.RateLeft;
            }

            // Might want to log this under ScrobbleError
            if (response.ErrorMessage.Contains("Too Many Requests"))
            {
                _logger.LogInformation("Hit Too many requests while posting scrobble updates, sleeping to regain requests and retrying");
                await _auditService.LogAsync(KavitaPlusAuditCategory.Scrobble, KavitaPlusEventType.ScrobbleRateLimitHit,
                    AuditStatus.Failure, AuditSubjectType.Global, error: "rate-limit-hit");
                await Task.Delay(TimeSpan.FromMinutes(10));
                return await PostScrobbleUpdate(data, license, evt);
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
                    new AuditLogScrobbleParamsDto { ScrobbleEventType = evt.ScrobbleEventType, LibraryType = evt.Series?.Library?.Type ?? LibraryType.Manga },
                    AuditStatus.Failure, "invalid-token", userId: evt.AppUserId);
                throw new KavitaException("Access token is invalid");
            }

            if (response.ErrorMessage.Contains("Unknown Series"))
            {
                // Log the Series name and Id in ScrobbleErrors
                _logger.LogInformation("Kavita+ was unable to match the series: {SeriesName}", evt.Series.Name);
                await MarkSeriesAsUnknown(data, evt);
                evt.SetErrorMessage(UnknownSeriesErrorMessage);
                await _auditService.LogScrobbleAsync(KavitaPlusEventType.ScrobbleEventFailed, evt.SeriesId,
                    new AuditLogScrobbleParamsDto { ScrobbleEventType = evt.ScrobbleEventType, LibraryType = evt.Series?.Library?.Type ?? LibraryType.Manga },
                    AuditStatus.Failure, "unknown-series", userId: evt.AppUserId);
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

            return response.RateLeft;
        }
        catch (FlurlHttpException ex)
        {
            var errorMessage = await ex.GetResponseStringAsync();
            // Trim quotes if the response is a JSON string
            errorMessage = errorMessage.Trim('"');

            if (errorMessage.Contains("Too Many Requests"))
            {
                _logger.LogInformation("Hit Too many requests while posting scrobble updates, sleeping to regain requests and retrying");
                await Task.Delay(TimeSpan.FromMinutes(10));
                return await PostScrobbleUpdate(data, license, evt);
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

    /// <summary>
    /// This will backfill events from existing progress history, ratings, and want to read for users that have a valid license
    /// </summary>
    /// <param name="userId">Defaults to 0 meaning all users. Allows a userId to be set if a scrobble key is added to a user</param>
    /// <param name="ct"></param>
    public async Task CreateEventsFromExistingHistory(int userId = 0, CancellationToken ct = default)
    {
        if (!await _licenseService.HasActiveLicense(ct: ct)) return;

        if (userId != 0)
        {
            var user = await _unitOfWork.UserRepository.GetUserByIdAsync(userId, ct: ct);
            if (user == null || string.IsNullOrEmpty(user.AniListAccessToken)) return;
            if (user.HasRunScrobbleEventGeneration)
            {
                _logger.LogWarning("User {UserName} has already run scrobble event generation, Kavita will not generate more events", user.UserName);
                return;
            }
        }

        var libAllowsScrobbling = (await _unitOfWork.LibraryRepository.GetLibrariesAsync(ct: ct))
            .ToDictionary(lib => lib.Id, lib => lib.AllowScrobbling);

        var userIds = (await _unitOfWork.UserRepository.GetAllUsersAsync(ct: ct))
            .Where(l => userId == 0 || userId == l.Id)
            .Where(u => !u.HasRunScrobbleEventGeneration)
            .Select(u => u.Id);

        foreach (var uId in userIds)
        {
            await CreateEventsFromExistingHistoryForUser(uId, libAllowsScrobbling);
        }
    }

    /// <summary>
    /// Creates wantToRead, rating, reviews, and series progress events for the suer
    /// </summary>
    /// <param name="userId"></param>
    /// <param name="libAllowsScrobbling"></param>
    private async Task CreateEventsFromExistingHistoryForUser(int userId, Dictionary<int, bool> libAllowsScrobbling)
    {
        var wantToRead = await _unitOfWork.SeriesRepository.GetWantToReadForUserAsync(userId);
        foreach (var wtr in wantToRead)
        {
            if (!libAllowsScrobbling[wtr.LibraryId]) continue;
            await ScrobbleWantToReadUpdate(userId, wtr.Id, true);
        }

        var ratings = await _unitOfWork.UserRepository.GetSeriesWithRatings(userId);
        foreach (var rating in ratings)
        {
            if (!libAllowsScrobbling[rating.Series.LibraryId]) continue;
            await ScrobbleRatingUpdate(userId, rating.SeriesId, rating.Rating);
        }

        var reviews = await _unitOfWork.UserRepository.GetSeriesWithReviews(userId);
        foreach (var review in reviews.Where(r => !string.IsNullOrEmpty(r.Review)))
        {
            if (!libAllowsScrobbling[review.Series.LibraryId]) continue;
            await ScrobbleReviewUpdate(userId, review.SeriesId, string.Empty, review.Review!);
        }



        var scrobbleLibraries = libAllowsScrobbling.Keys.Where(k => libAllowsScrobbling[k]).ToList();
        if (scrobbleLibraries.Count > 0)
        {
            var filter = new SeriesFilterV2Dto()
            {
                Combination = FilterCombination.And,
                Statements =
                [
                    new SeriesFilterStatementDto()
                    {
                        Comparison = FilterComparison.Contains,
                        Field = SeriesFilterField.Libraries,
                        Value = string.Join(',', scrobbleLibraries)
                    },
                    new SeriesFilterStatementDto()
                    {
                        Comparison = FilterComparison.LessThan,
                        Field = SeriesFilterField.ReadProgress,
                        Value = "100"
                    },
                    new SeriesFilterStatementDto()
                    {
                        Comparison = FilterComparison.GreaterThan,
                        Field = SeriesFilterField.ReadProgress,
                        Value = "0"
                    }
                ]
            };

            var seriesWithProgress =
                await _unitOfWork.SeriesRepository.GetSeriesDtoForLibraryIdAsync(userId, new UserParams(), filter);

            foreach (var series in seriesWithProgress.Where(series => series.PagesRead > 0))
            {
                if (!libAllowsScrobbling[series.LibraryId]) continue;
                await ScrobbleReadingUpdate(userId, series.Id);
            }
        }




        var user = await _unitOfWork.UserRepository.GetUserByIdAsync(userId);
        if (user != null)
        {
            user.HasRunScrobbleEventGeneration = true;
            user.ScrobbleEventGenerationRan = DateTime.UtcNow;
            await _unitOfWork.CommitAsync();
        }
    }

    public async Task CreateEventsFromExistingHistoryForSeries(int seriesId, CancellationToken ct = default)
    {
        if (!await _licenseService.HasActiveLicense(ct: ct)) return;

        var series = await _unitOfWork.SeriesRepository.GetSeriesByIdAsync(seriesId, SeriesIncludes.Library, ct);
        if (series == null || !series.Library.AllowScrobbling) return;

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
                await ScrobbleRatingUpdate(uId, rating.SeriesId, rating.Rating, ct);
            }

            // Handle review specific to the series
            var reviews = await _unitOfWork.UserRepository.GetSeriesWithReviews(uId, ct);
            foreach (var review in reviews.Where(r => r.SeriesId == seriesId && !string.IsNullOrEmpty(r.Review)))
            {
                await ScrobbleReviewUpdate(uId, review.SeriesId, string.Empty, review.Review!, ct);
            }

            // Handle progress updates for the specific series
            await ScrobbleReadingUpdate(uId, seriesId, ct);
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

    public async Task<bool> RetryScrobbleAsync(int authUserId, KavitaPlusAuditEntryDto auditEntry, CancellationToken ct = default)
    {
        if (auditEntry.ScrobbleDetails == null) return false;
        if (auditEntry.Status != AuditStatus.Failure) return false;
        if (auditEntry.SubjectType != AuditSubjectType.Series && auditEntry.SubjectType != AuditSubjectType.Chapter) return false;

        if (!auditEntry.CanRetry) return false;

        switch (auditEntry.ScrobbleDetails!.ScrobbleEventType)
        {
            case ScrobbleEventType.ChapterRead:
                if (auditEntry.SeriesId == null || auditEntry.UserId == null) return false;
                await ScrobbleReadingUpdate(auditEntry.UserId.Value, auditEntry.SeriesId.Value, ct);
                break;
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
                await ScrobbleRatingUpdate(auditEntry.UserId.Value, auditEntry.SeriesId.Value, auditEntry.ScrobbleDetails.Rating ?? 0f, ct);
                break;
            case ScrobbleEventType.Review:
                if (auditEntry.SeriesId == null || auditEntry.UserId == null) return false;
                string? reviewBody;
                if (auditEntry is {SubjectType: AuditSubjectType.Chapter, SubjectId: not null})
                {
                    var chapterRating = await _unitOfWork.UserRepository.GetUserChapterRatingAsync(auditEntry.UserId.Value, auditEntry.SubjectId.Value, ct);
                    reviewBody = chapterRating?.Review;
                }
                else
                {
                    var seriesRating = await _unitOfWork.UserRepository.GetUserRatingAsync(auditEntry.SeriesId.Value, auditEntry.UserId.Value, ct);
                    reviewBody = seriesRating?.Review;
                }
                if (string.IsNullOrEmpty(reviewBody)) return false;
                await ScrobbleReviewUpdate(auditEntry.UserId.Value, auditEntry.SeriesId.Value, string.Empty, reviewBody, ct);
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
        var providers = new List<ScrobbleProvider>();
        if (!string.IsNullOrEmpty(appUser.AniListAccessToken)) providers.Add(ScrobbleProvider.AniList);

        return providers;
    }

    private async Task<int> SetAndCheckRateLimit(IDictionary<int, int> userRateLimits, AppUser user, string license)
    {
        if (string.IsNullOrEmpty(user.AniListAccessToken)) return 0;
        try
        {
            if (!userRateLimits.ContainsKey(user.Id))
            {
                var rate = await GetRateLimit(license, user.AniListAccessToken);
                userRateLimits.Add(user.Id, rate);
            }
        }
        catch (Exception ex)
        {
            _logger.LogInformation(ex, "User {UserName} had an issue figuring out rate: {Message}", user.UserName, ex.Message);
            userRateLimits.Add(user.Id, 0);
        }

        userRateLimits.TryGetValue(user.Id, out var count);
        if (count == 0)
        {
            _logger.LogInformation("User {UserName} is out of rate for Scrobbling", user.UserName);
        }

        return count;
    }

}
