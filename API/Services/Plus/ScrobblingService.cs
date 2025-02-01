using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using System.Net.Http;
using System.Threading.Tasks;
using API.Data;
using API.Data.Repositories;
using API.DTOs.Filtering;
using API.DTOs.Scrobbling;
using API.Entities;
using API.Entities.Enums;
using API.Entities.Metadata;
using API.Entities.Scrobble;
using API.Extensions;
using API.Helpers;
using API.Services.Tasks.Scanner.Parser;
using API.SignalR;
using Flurl.Http;
using Hangfire;
using Kavita.Common;
using Kavita.Common.EnvironmentInfo;
using Kavita.Common.Helpers;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace API.Services.Plus;
#nullable enable

/// <summary>
/// Misleading name but is the source of data (like a review coming from AniList)
/// </summary>
public enum ScrobbleProvider
{
    /// <summary>
    /// For now, this means data comes from within this instance of Kavita
    /// </summary>
    Kavita = 0,
    AniList = 1,
    Mal = 2,
}

public interface IScrobblingService
{
    Task CheckExternalAccessTokens();
    Task<bool> HasTokenExpired(int userId, ScrobbleProvider provider);
    Task ScrobbleRatingUpdate(int userId, int seriesId, float rating);
    Task ScrobbleReviewUpdate(int userId, int seriesId, string? reviewTitle, string reviewBody);
    Task ScrobbleReadingUpdate(int userId, int seriesId);
    Task ScrobbleWantToReadUpdate(int userId, int seriesId, bool onWantToRead);

    [DisableConcurrentExecution(60 * 60 * 60)]
    [AutomaticRetry(Attempts = 3, OnAttemptsExceeded = AttemptsExceededAction.Delete)]
    public Task ClearProcessedEvents();
    [DisableConcurrentExecution(60 * 60 * 60)]
    [AutomaticRetry(Attempts = 3, OnAttemptsExceeded = AttemptsExceededAction.Delete)]
    Task ProcessUpdatesSinceLastSync();
    Task CreateEventsFromExistingHistory(int userId = 0);
    Task CreateEventsFromExistingHistoryForSeries(int seriesId);
    Task ClearEventsForSeries(int userId, int seriesId);
}

public class ScrobblingService : IScrobblingService
{
    private readonly IUnitOfWork _unitOfWork;
    private readonly IEventHub _eventHub;
    private readonly ILogger<ScrobblingService> _logger;
    private readonly ILicenseService _licenseService;
    private readonly ILocalizationService _localizationService;
    private readonly IEmailService _emailService;

    public const string AniListWeblinkWebsite = "https://anilist.co/manga/";
    public const string MalWeblinkWebsite = "https://myanimelist.net/manga/";
    public const string GoogleBooksWeblinkWebsite = "https://books.google.com/books?id=";
    public const string MangaDexWeblinkWebsite = "https://mangadex.org/title/";
    public const string AniListStaffWebsite = "https://anilist.co/staff/";
    public const string AniListCharacterWebsite = "https://anilist.co/character/";


    private static readonly IDictionary<string, int> WeblinkExtractionMap = new Dictionary<string, int>()
    {
        {AniListWeblinkWebsite, 0},
        {MalWeblinkWebsite, 0},
        {GoogleBooksWeblinkWebsite, 0},
        {MangaDexWeblinkWebsite, 0},
        {AniListStaffWebsite, 0},
        {AniListCharacterWebsite, 0},
    };

    private const int ScrobbleSleepTime = 1000; // We can likely tie this to AniList's 90 rate / min ((60 * 1000) / 90)

    private static readonly IList<ScrobbleProvider> BookProviders = new List<ScrobbleProvider>()
    {
    };
    private static readonly IList<ScrobbleProvider> LightNovelProviders = new List<ScrobbleProvider>()
    {
        ScrobbleProvider.AniList
    };
    private static readonly IList<ScrobbleProvider> ComicProviders = new List<ScrobbleProvider>();
    private static readonly IList<ScrobbleProvider> MangaProviders = new List<ScrobbleProvider>()
    {
        ScrobbleProvider.AniList
    };


    private const string UnknownSeriesErrorMessage = "Series cannot be matched for Scrobbling";
    private const string AccessTokenErrorMessage = "Access Token needs to be rotated to continue scrobbling";


    public ScrobblingService(IUnitOfWork unitOfWork, IEventHub eventHub, ILogger<ScrobblingService> logger,
        ILicenseService licenseService, ILocalizationService localizationService, IEmailService emailService)
    {
        _unitOfWork = unitOfWork;
        _eventHub = eventHub;
        _logger = logger;
        _licenseService = licenseService;
        _localizationService = localizationService;
        _emailService = emailService;

        FlurlConfiguration.ConfigureClientForUrl(Configuration.KavitaPlusApiUrl);
    }


    /// <summary>
    /// An automated job that will run against all user's tokens and validate if they are still active
    /// </summary>
    /// <remarks>This service can validate without license check as the task which calls will be guarded</remarks>
    /// <returns></returns>
    public async Task CheckExternalAccessTokens()
    {
        // Validate AniList
        var users = await _unitOfWork.UserRepository.GetAllUsersAsync();
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
                user.Id);

        }
    }

    /// <summary>
    /// Checks if an early reminder email should be sent.
    /// </summary>
    private async Task<bool> ShouldSendEarlyReminder(int userId, DateTime tokenExpiry)
    {
        var earlyReminderDate = tokenExpiry.AddDays(-5);
        if (earlyReminderDate <= DateTime.UtcNow)
        {
            var hasAlreadySentReminder = await _unitOfWork.DataContext.EmailHistory
                .AnyAsync(h => h.AppUserId == userId && h.Sent &&
                               h.EmailTemplate == EmailService.TokenExpiringSoonTemplate &&
                               h.SendDate >= earlyReminderDate);

            return !hasAlreadySentReminder;
        }

        return false;
    }

    /// <summary>
    /// Checks if an expiration notification email should be sent.
    /// </summary>
    private async Task<bool> ShouldSendExpirationReminder(int userId, DateTime tokenExpiry)
    {
        if (tokenExpiry <= DateTime.UtcNow)
        {
            var hasAlreadySentExpirationEmail = await _unitOfWork.DataContext.EmailHistory
                .AnyAsync(h => h.AppUserId == userId && h.Sent &&
                               h.EmailTemplate == EmailService.TokenExpirationTemplate &&
                               h.SendDate >= tokenExpiry);

            return !hasAlreadySentExpirationEmail;
        }

        return false;
    }


    public async Task<bool> HasTokenExpired(int userId, ScrobbleProvider provider)
    {
        var token = await GetTokenForProvider(userId, provider);

        if (await HasTokenExpired(token, provider))
        {
            // NOTE: Should this side effect be here?
            await _eventHub.SendMessageToAsync(MessageFactory.ScrobblingKeyExpired,
                MessageFactory.ScrobblingKeyExpiredEvent(ScrobbleProvider.AniList), userId);
            return true;
        }

        return false;
    }

    private async Task<bool> HasTokenExpired(string token, ScrobbleProvider provider)
    {
        if (string.IsNullOrEmpty(token) ||
            !TokenService.HasTokenExpired(token)) return false;

        var license = await _unitOfWork.SettingsRepository.GetSettingAsync(ServerSettingKey.LicenseKey);
        if (string.IsNullOrEmpty(license.Value)) return true;

        try
        {
            var response = await (Configuration.KavitaPlusApiUrl + "/api/scrobbling/valid-key?provider=" + provider + "&key=" + token)
                .WithKavitaPlusHeaders(license.Value)
                .GetStringAsync();

            return bool.Parse(response);
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

    public async Task ScrobbleReviewUpdate(int userId, int seriesId, string? reviewTitle, string reviewBody)
    {
        // Currently disabled until at least hardcover is implemented
        return;
        if (!await _licenseService.HasActiveLicense()) return;

        var series = await _unitOfWork.SeriesRepository.GetSeriesByIdAsync(seriesId, SeriesIncludes.Metadata | SeriesIncludes.Library);
        if (series == null) throw new KavitaException(await _localizationService.Translate(userId, "series-doesnt-exist"));

        _logger.LogInformation("Processing Scrobbling review event for {UserId} on {SeriesName}", userId, series.Name);
        if (await CheckIfCannotScrobble(userId, seriesId, series)) return;

        if (IsAniListReviewValid(reviewTitle, reviewBody))
        {
            _logger.LogDebug(
                "Rejecting Scrobble event for {Series}. Review is not long enough to meet requirements", series.Name);
            return;
        }

        var existingEvt = await _unitOfWork.ScrobbleRepository.GetEvent(userId, series.Id,
            ScrobbleEventType.Review);
        if (existingEvt is {IsProcessed: false})
        {
            _logger.LogDebug("Overriding Review scrobble event for {Series}", existingEvt.Series.Name);
            existingEvt.ReviewBody = reviewBody;
            existingEvt.ReviewTitle = reviewTitle;
            _unitOfWork.ScrobbleRepository.Update(existingEvt);
            await _unitOfWork.CommitAsync();
            return;
        }

        var evt = new ScrobbleEvent()
        {
            SeriesId = series.Id,
            LibraryId = series.LibraryId,
            ScrobbleEventType = ScrobbleEventType.Review,
            AniListId = ExtractId<int?>(series.Metadata.WebLinks, AniListWeblinkWebsite),
            MalId = GetMalId(series),
            AppUserId = userId,
            Format = series.Library.Type.ConvertToPlusMediaFormat(series.Format),
            ReviewBody = reviewBody,
            ReviewTitle = reviewTitle
        };
        _unitOfWork.ScrobbleRepository.Attach(evt);
        await _unitOfWork.CommitAsync();
        _logger.LogDebug("Added Scrobbling Review update on {SeriesName} with Userid {UserId} ", series.Name, userId);
    }

    private static bool IsAniListReviewValid(string reviewTitle, string reviewBody)
    {
        return string.IsNullOrEmpty(reviewTitle) || string.IsNullOrEmpty(reviewBody) || (reviewTitle.Length < 2200 ||
            reviewTitle.Length > 120 ||
            reviewTitle.Length < 20);
    }

    public async Task ScrobbleRatingUpdate(int userId, int seriesId, float rating)
    {
        if (!await _licenseService.HasActiveLicense()) return;

        var series = await _unitOfWork.SeriesRepository.GetSeriesByIdAsync(seriesId, SeriesIncludes.Metadata | SeriesIncludes.Library | SeriesIncludes.ExternalMetadata);
        if (series == null) throw new KavitaException(await _localizationService.Translate(userId, "series-doesnt-exist"));

        var user = await _unitOfWork.UserRepository.GetUserByIdAsync(userId, AppUserIncludes.UserPreferences);
        if (user == null || !user.UserPreferences.AniListScrobblingEnabled) return;

        _logger.LogInformation("Processing Scrobbling rating event for {UserId} on {SeriesName}", userId, series.Name);
        if (await CheckIfCannotScrobble(userId, seriesId, series)) return;

        var existingEvt = await _unitOfWork.ScrobbleRepository.GetEvent(userId, series.Id,
            ScrobbleEventType.ScoreUpdated);
        if (existingEvt is {IsProcessed: false})
        {
            // We need to just update Volume/Chapter number
            _logger.LogDebug("Overriding scrobble event for {Series} from Rating {Rating} -> {UpdatedRating}",
                existingEvt.Series.Name, existingEvt.Rating, rating);
            existingEvt.Rating = rating;
            _unitOfWork.ScrobbleRepository.Update(existingEvt);
            await _unitOfWork.CommitAsync();
            return;
        }

        var evt = new ScrobbleEvent()
        {
            SeriesId = series.Id,
            LibraryId = series.LibraryId,
            ScrobbleEventType = ScrobbleEventType.ScoreUpdated,
            AniListId = GetAniListId(series),
            MalId = GetMalId(series),
            AppUserId = userId,
            Format = series.Library.Type.ConvertToPlusMediaFormat(series.Format),
            Rating = rating
        };
        _unitOfWork.ScrobbleRepository.Attach(evt);
        await _unitOfWork.CommitAsync();
        _logger.LogDebug("Added Scrobbling Rating update on {SeriesName} with Userid {UserId}", series.Name, userId);
    }

    public static long? GetMalId(Series series)
    {
        var malId = ExtractId<long?>(series.Metadata.WebLinks, MalWeblinkWebsite);
        return malId ?? series.ExternalSeriesMetadata?.MalId;
    }

    public static int? GetAniListId(Series seriesWithExternalMetadata)
    {
        var aniListId = ExtractId<int?>(seriesWithExternalMetadata.Metadata.WebLinks, AniListWeblinkWebsite);
        return aniListId ?? seriesWithExternalMetadata.ExternalSeriesMetadata?.AniListId;
    }

    public async Task ScrobbleReadingUpdate(int userId, int seriesId)
    {
        if (!await _licenseService.HasActiveLicense()) return;

        var series = await _unitOfWork.SeriesRepository.GetSeriesByIdAsync(seriesId, SeriesIncludes.Metadata | SeriesIncludes.Library | SeriesIncludes.ExternalMetadata);
        if (series == null) throw new KavitaException(await _localizationService.Translate(userId, "series-doesnt-exist"));

        var user = await _unitOfWork.UserRepository.GetUserByIdAsync(userId, AppUserIncludes.UserPreferences);
        if (user == null || !user.UserPreferences.AniListScrobblingEnabled) return;

        _logger.LogInformation("Processing Scrobbling reading event for {UserId} on {SeriesName}", userId, series.Name);
        if (await CheckIfCannotScrobble(userId, seriesId, series)) return;

        var existingEvt = await _unitOfWork.ScrobbleRepository.GetEvent(userId, series.Id,
            ScrobbleEventType.ChapterRead);
        if (existingEvt is {IsProcessed: false})
        {
            // We need to just update Volume/Chapter number
            var prevChapter = $"{existingEvt.ChapterNumber}";
            var prevVol = $"{existingEvt.VolumeNumber}";

            existingEvt.VolumeNumber =
                (int) await _unitOfWork.AppUserProgressRepository.GetHighestFullyReadVolumeForSeries(seriesId, userId);
            existingEvt.ChapterNumber =
                await _unitOfWork.AppUserProgressRepository.GetHighestFullyReadChapterForSeries(seriesId, userId);
            _unitOfWork.ScrobbleRepository.Update(existingEvt);
            await _unitOfWork.CommitAsync();
            _logger.LogDebug("Overriding scrobble event for {Series} from vol {PrevVol} ch {PrevChap} -> vol {UpdatedVol} ch {UpdatedChap}",
                existingEvt.Series.Name, prevVol, prevChapter, existingEvt.VolumeNumber, existingEvt.ChapterNumber);
            return;
        }

        try
        {
            var evt = new ScrobbleEvent()
            {
                SeriesId = series.Id,
                LibraryId = series.LibraryId,
                ScrobbleEventType = ScrobbleEventType.ChapterRead,
                AniListId = GetAniListId(series),
                MalId = GetMalId(series),
                AppUserId = userId,
                VolumeNumber =
                    (int) await _unitOfWork.AppUserProgressRepository.GetHighestFullyReadVolumeForSeries(seriesId, userId),
                ChapterNumber =
                    await _unitOfWork.AppUserProgressRepository.GetHighestFullyReadChapterForSeries(seriesId, userId),
                Format = series.Library.Type.ConvertToPlusMediaFormat(series.Format),
            };

            _unitOfWork.ScrobbleRepository.Attach(evt);
            await _unitOfWork.CommitAsync();
            _logger.LogDebug("Added Scrobbling Read update on {SeriesName} - Volume: {VolumeNumber} Chapter: {ChapterNumber} for User: {UserId}", series.Name, evt.VolumeNumber, evt.ChapterNumber, userId);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "There was an issue when saving scrobble read event");
        }
    }

    public async Task ScrobbleWantToReadUpdate(int userId, int seriesId, bool onWantToRead)
    {
        if (!await _licenseService.HasActiveLicense()) return;

        var series = await _unitOfWork.SeriesRepository.GetSeriesByIdAsync(seriesId, SeriesIncludes.Metadata | SeriesIncludes.Library | SeriesIncludes.ExternalMetadata);
        if (series == null || !series.Library.AllowScrobbling) throw new KavitaException(await _localizationService.Translate(userId, "series-doesnt-exist"));

        var user = await _unitOfWork.UserRepository.GetUserByIdAsync(userId, AppUserIncludes.UserPreferences);
        if (user == null || !user.UserPreferences.AniListScrobblingEnabled) return;

        if (await CheckIfCannotScrobble(userId, seriesId, series)) return;
        _logger.LogInformation("Processing Scrobbling want-to-read event for {UserId} on {SeriesName}", userId, series.Name);

        var existing = await _unitOfWork.ScrobbleRepository.Exists(userId, series.Id,
            onWantToRead ? ScrobbleEventType.AddWantToRead : ScrobbleEventType.RemoveWantToRead);
        if (existing) return; // BUG: If I take a series and add to remove from want to read, then add to want to read, Kavita rejects the second as a duplicate, when it's not

        var evt = new ScrobbleEvent()
        {
            SeriesId = series.Id,
            LibraryId = series.LibraryId,
            ScrobbleEventType = onWantToRead ? ScrobbleEventType.AddWantToRead : ScrobbleEventType.RemoveWantToRead,
            AniListId = GetAniListId(series),
            MalId = GetMalId(series),
            AppUserId = userId,
            Format = series.Library.Type.ConvertToPlusMediaFormat(series.Format),
        };
        _unitOfWork.ScrobbleRepository.Attach(evt);
        await _unitOfWork.CommitAsync();
        _logger.LogDebug("Added Scrobbling WantToRead update on {SeriesName} with Userid {UserId} ", series.Name, userId);
    }

    private async Task<bool> CheckIfCannotScrobble(int userId, int seriesId, Series series)
    {
        if (series.DontMatch) return true;
        if (await _unitOfWork.UserRepository.HasHoldOnSeries(userId, seriesId))
        {
            _logger.LogInformation("Series {SeriesName} is on UserId {UserId}'s hold list. Not scrobbling", series.Name,
                userId);
            return true;
        }

        var library = await _unitOfWork.LibraryRepository.GetLibraryForIdAsync(series.LibraryId);
        if (library is not {AllowScrobbling: true}) return true;
        if (!ExternalMetadataService.IsPlusEligible(library.Type)) return true;
        return false;
    }

    private async Task<int> GetRateLimit(string license, string aniListToken)
    {
        if (string.IsNullOrWhiteSpace(aniListToken)) return 0;
        try
        {
            var response = await (Configuration.KavitaPlusApiUrl + "/api/scrobbling/rate-limit?accessToken=" + aniListToken)
                .WithKavitaPlusHeaders(license)
                .GetStringAsync();

            return int.Parse(response);
        }
        catch (Exception e)
        {
            _logger.LogError(e, "An error happened during the request to Kavita+ API");
        }

        return 0;
    }

    private async Task<int> PostScrobbleUpdate(ScrobbleDto data, string license, ScrobbleEvent evt)
    {
        try
        {
            var response = await (Configuration.KavitaPlusApiUrl + "/api/scrobbling/update")
                .WithKavitaPlusHeaders(license)
                .PostJsonAsync(data)
                .ReceiveJson<ScrobbleResponseDto>();

            if (!response.Successful)
            {
                // Might want to log this under ScrobbleError
                if (response.ErrorMessage != null && response.ErrorMessage.Contains("Too Many Requests"))
                {
                    _logger.LogInformation("Hit Too many requests, sleeping to regain requests and retrying");
                    await Task.Delay(TimeSpan.FromMinutes(10));
                    return await PostScrobbleUpdate(data, license, evt);
                }
                if (response.ErrorMessage != null && response.ErrorMessage.Contains("Unauthorized"))
                {
                    _logger.LogCritical("Kavita+ responded with Unauthorized. Please check your subscription");
                    await _licenseService.HasActiveLicense(true);
                    evt.IsErrored = true;
                    evt.ErrorDetails = "Kavita+ subscription no longer active";
                    throw new KavitaException("Kavita+ responded with Unauthorized. Please check your subscription");
                }
                if (response.ErrorMessage != null && response.ErrorMessage.Contains("Access token is invalid"))
                {
                    evt.IsErrored = true;
                    evt.ErrorDetails = AccessTokenErrorMessage;
                    throw new KavitaException("Access token is invalid");
                }
                if (response.ErrorMessage != null && response.ErrorMessage.Contains("Unknown Series"))
                {
                    // Log the Series name and Id in ScrobbleErrors
                    _logger.LogInformation("Kavita+ was unable to match the series: {SeriesName}", evt.Series.Name);
                    if (!await _unitOfWork.ScrobbleRepository.HasErrorForSeries(evt.SeriesId))
                    {
                        // Create a new ExternalMetadata entry to indicate that this is not matchable
                        var series = await _unitOfWork.SeriesRepository.GetSeriesByIdAsync(evt.SeriesId, SeriesIncludes.ExternalMetadata);
                        if (series.ExternalSeriesMetadata == null)
                        {
                            series.ExternalSeriesMetadata = new ExternalSeriesMetadata() {SeriesId = evt.SeriesId};
                        }
                        series!.IsBlacklisted = true;
                        _unitOfWork.SeriesRepository.Update(series);

                        _unitOfWork.ScrobbleRepository.Attach(new ScrobbleError()
                        {
                            Comment = UnknownSeriesErrorMessage,
                            Details = data.SeriesName,
                            LibraryId = evt.LibraryId,
                            SeriesId = evt.SeriesId
                        });

                    }

                    evt.IsErrored = true;
                    evt.ErrorDetails = UnknownSeriesErrorMessage;
                } else if (response.ErrorMessage != null && response.ErrorMessage.StartsWith("Review"))
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
                    evt.IsErrored = true;
                    evt.ErrorDetails = "Review was unable to be saved due to upstream requirements";
                }
            }

            return response.RateLeft;
        }
        catch (FlurlHttpException  ex)
        {
            _logger.LogError(ex, "Scrobbling to Kavita+ API failed due to error: {ErrorMessage}", ex.Message);
            if (ex.Message.Contains("Call failed with status code 500 (Internal Server Error)"))
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
                evt.IsErrored = true;
                evt.ErrorDetails = "Bad payload from Scrobble Provider";
                throw new KavitaException("Bad payload from Scrobble Provider");
            }
            throw;
        }
    }

    /// <summary>
    /// This will backfill events from existing progress history, ratings, and want to read for users that have a valid license
    /// </summary>
    /// <param name="userId">Defaults to 0 meaning all users. Allows a userId to be set if a scrobble key is added to a user</param>
    public async Task CreateEventsFromExistingHistory(int userId = 0)
    {
        if (!await _licenseService.HasActiveLicense()) return;

        if (userId != 0)
        {
            var user = await _unitOfWork.UserRepository.GetUserByIdAsync(userId);
            if (user == null || string.IsNullOrEmpty(user.AniListAccessToken)) return;
        }

        var libAllowsScrobbling = (await _unitOfWork.LibraryRepository.GetLibrariesAsync())
            .ToDictionary(lib => lib.Id, lib => lib.AllowScrobbling);

        var userIds = (await _unitOfWork.UserRepository.GetAllUsersAsync())
            .Where(l => userId == 0 || userId == l.Id)
            .Select(u => u.Id);

        foreach (var uId in userIds)
        {
            var wantToRead = await _unitOfWork.SeriesRepository.GetWantToReadForUserAsync(uId);
            foreach (var wtr in wantToRead)
            {
                if (!libAllowsScrobbling[wtr.LibraryId]) continue;
                await ScrobbleWantToReadUpdate(uId, wtr.Id, true);
            }

            var ratings = await _unitOfWork.UserRepository.GetSeriesWithRatings(uId);
            foreach (var rating in ratings)
            {
                if (!libAllowsScrobbling[rating.Series.LibraryId]) continue;
                await ScrobbleRatingUpdate(uId, rating.SeriesId, rating.Rating);
            }

            var seriesWithProgress = await _unitOfWork.SeriesRepository.GetSeriesDtoForLibraryIdAsync(0, uId,
                new UserParams(), new FilterDto()
                {
                    ReadStatus = new ReadStatus()
                    {
                        Read = true,
                        InProgress = true,
                        NotRead = false
                    },
                    Libraries = libAllowsScrobbling.Keys.Where(k => libAllowsScrobbling[k]).ToList()
                });

            foreach (var series in seriesWithProgress)
            {
                if (!libAllowsScrobbling[series.LibraryId]) continue;
                if (series.PagesRead <= 0) continue; // Since we only scrobble when things are higher, we can
                await ScrobbleReadingUpdate(uId, series.Id);
            }
        }
    }

    public async Task CreateEventsFromExistingHistoryForSeries(int seriesId)
    {
        if (!await _licenseService.HasActiveLicense()) return;

        var series = await _unitOfWork.SeriesRepository.GetSeriesByIdAsync(seriesId, SeriesIncludes.Library);
        if (series == null || !series.Library.AllowScrobbling) return;

        _logger.LogInformation("Creating Scrobbling events for Series {SeriesName}", series.Name);

        var userIds = (await _unitOfWork.UserRepository.GetAllUsersAsync())
            .Select(u => u.Id);

        foreach (var uId in userIds)
        {
            // Handle "Want to Read" updates specific to the series
            var wantToRead = await _unitOfWork.SeriesRepository.GetWantToReadForUserAsync(uId);
            foreach (var wtr in wantToRead.Where(wtr => wtr.Id == seriesId))
            {
                await ScrobbleWantToReadUpdate(uId, wtr.Id, true);
            }

            // Handle ratings specific to the series
            var ratings = await _unitOfWork.UserRepository.GetSeriesWithRatings(uId);
            foreach (var rating in ratings.Where(rating => rating.SeriesId == seriesId))
            {
                await ScrobbleRatingUpdate(uId, rating.SeriesId, rating.Rating);
            }

            // Handle progress updates for the specific series
            var seriesProgress = await _unitOfWork.SeriesRepository.GetSeriesDtoForLibraryIdAsync(
                series.LibraryId,
                uId,
                new UserParams(),
                new FilterDto
                {
                    ReadStatus = new ReadStatus
                    {
                        Read = true,
                        InProgress = true,
                        NotRead = false
                    },
                    Libraries = new List<int> { series.LibraryId },
                    SeriesNameQuery = series.Name
                });

            foreach (var progress in seriesProgress.Where(progress => progress.Id == seriesId))
            {
                if (progress.PagesRead > 0)
                {
                    await ScrobbleReadingUpdate(uId, progress.Id);
                }
            }
        }
    }

    /// <summary>
    /// Removes all events (active) that are tied to a now-on hold series
    /// </summary>
    /// <param name="userId"></param>
    /// <param name="seriesId"></param>
    public async Task ClearEventsForSeries(int userId, int seriesId)
    {
        _logger.LogInformation("Clearing Pre-existing Scrobble events for Series {SeriesId} by User {UserId} as Series is now on hold list", seriesId, userId);
        var events = await _unitOfWork.ScrobbleRepository.GetUserEventsForSeries(userId, seriesId);
        foreach (var scrobble in events)
        {
            _unitOfWork.ScrobbleRepository.Remove(scrobble);
        }

        await _unitOfWork.CommitAsync();
    }

    /// <summary>
    /// Removes all events that have been processed that are 7 days old
    /// </summary>
    [DisableConcurrentExecution(60 * 60 * 60)]
    [AutomaticRetry(Attempts = 3, OnAttemptsExceeded = AttemptsExceededAction.Delete)]
    public async Task ClearProcessedEvents()
    {
        var events = await _unitOfWork.ScrobbleRepository.GetProcessedEvents(7);
        _unitOfWork.ScrobbleRepository.Remove(events);
        await _unitOfWork.CommitAsync();
    }

    /// <summary>
    /// This is a task that is ran on a fixed schedule (every few hours or every day) that clears out the scrobble event table
    /// and offloads the data to the API server which performs the syncing to the providers.
    /// </summary>
    [DisableConcurrentExecution(60 * 60 * 60)]
    [AutomaticRetry(Attempts = 3, OnAttemptsExceeded = AttemptsExceededAction.Delete)]
    public async Task ProcessUpdatesSinceLastSync()
    {
        // Check how many scrobble events we have available then only do those.
        _logger.LogInformation("Starting Scrobble Processing");
        var userRateLimits = new Dictionary<int, int>();
        var license = await _unitOfWork.SettingsRepository.GetSettingAsync(ServerSettingKey.LicenseKey);

        var progressCounter = 0;

        var librariesWithScrobbling = (await _unitOfWork.LibraryRepository.GetLibrariesAsync())
            .AsEnumerable()
            .Where(l => l.AllowScrobbling)
            .Select(l => l.Id)
            .ToImmutableHashSet();

        var errors = (await _unitOfWork.ScrobbleRepository.GetScrobbleErrors())
            .Where(e => e.Comment == "Unknown Series" || e.Comment == UnknownSeriesErrorMessage || e.Comment == AccessTokenErrorMessage)
            .Select(e => e.SeriesId)
            .ToList();

        var readEvents = (await _unitOfWork.ScrobbleRepository.GetByEvent(ScrobbleEventType.ChapterRead))
            .Where(e => librariesWithScrobbling.Contains(e.LibraryId))
            .Where(e => !errors.Contains(e.SeriesId))
            .ToList();
        var addToWantToRead = (await _unitOfWork.ScrobbleRepository.GetByEvent(ScrobbleEventType.AddWantToRead))
            .Where(e => librariesWithScrobbling.Contains(e.LibraryId))
            .Where(e => !errors.Contains(e.SeriesId))
            .ToList();
        var removeWantToRead = (await _unitOfWork.ScrobbleRepository.GetByEvent(ScrobbleEventType.RemoveWantToRead))
            .Where(e => librariesWithScrobbling.Contains(e.LibraryId))
            .Where(e => !errors.Contains(e.SeriesId))
            .ToList();
        var ratingEvents = (await _unitOfWork.ScrobbleRepository.GetByEvent(ScrobbleEventType.ScoreUpdated))
            .Where(e => librariesWithScrobbling.Contains(e.LibraryId))
            .Where(e => !errors.Contains(e.SeriesId))
            .ToList();

        var decisions = addToWantToRead
            .GroupBy(item => new { item.SeriesId, item.AppUserId })
            .Select(group => new
            {
                group.Key.SeriesId,
                UserId = group.Key.AppUserId,
                Event = group.First(),
                Decision = group.Count() - removeWantToRead
                    .Count(removeItem => removeItem.SeriesId == group.Key.SeriesId && removeItem.AppUserId == group.Key.AppUserId)
            })
            .Where(d => d.Decision > 0)
            .Select(d => d.Event)
            .ToList();

        // For all userIds, ensure that we can connect and have access
        var usersToScrobble = readEvents.Select(r => r.AppUser)
            .Concat(addToWantToRead.Select(r => r.AppUser))
            .Concat(removeWantToRead.Select(r => r.AppUser))
            .Concat(ratingEvents.Select(r => r.AppUser))
            .Where(user => !string.IsNullOrEmpty(user.AniListAccessToken))
            .Where(user => user.UserPreferences.AniListScrobblingEnabled) // TODO: Add more as we add more support
            .DistinctBy(u => u.Id)
            .ToList();
        foreach (var user in usersToScrobble)
        {
            await SetAndCheckRateLimit(userRateLimits, user, license.Value);
        }

        var totalProgress = readEvents.Count + decisions.Count + ratingEvents.Count + decisions.Count;

        _logger.LogInformation("Found {TotalEvents} Scrobble Events", totalProgress);
        try
        {
            // Recalculate the highest volume/chapter
            foreach (var readEvt in readEvents)
            {
                readEvt.VolumeNumber =
                    (int) await _unitOfWork.AppUserProgressRepository.GetHighestFullyReadVolumeForSeries(readEvt.SeriesId,
                        readEvt.AppUser.Id);
                readEvt.ChapterNumber =
                    await _unitOfWork.AppUserProgressRepository.GetHighestFullyReadChapterForSeries(readEvt.SeriesId,
                        readEvt.AppUser.Id);
                _unitOfWork.ScrobbleRepository.Update(readEvt);
            }
            progressCounter = await ProcessEvents(readEvents, userRateLimits, usersToScrobble.Count, progressCounter, totalProgress, async evt => new ScrobbleDto()
            {
                Format = evt.Format,
                AniListId = evt.AniListId,
                MALId = (int?) evt.MalId,
                ScrobbleEventType = evt.ScrobbleEventType,
                ChapterNumber = evt.ChapterNumber,
                VolumeNumber = (int?) evt.VolumeNumber,
                AniListToken = evt.AppUser.AniListAccessToken,
                SeriesName = evt.Series.Name,
                LocalizedSeriesName = evt.Series.LocalizedName,
                ScrobbleDateUtc = evt.LastModifiedUtc,
                Year = evt.Series.Metadata.ReleaseYear,
                StartedReadingDateUtc = await _unitOfWork.AppUserProgressRepository.GetFirstProgressForSeries(evt.SeriesId, evt.AppUser.Id),
                LatestReadingDateUtc = await _unitOfWork.AppUserProgressRepository.GetLatestProgressForSeries(evt.SeriesId, evt.AppUser.Id),
            });

            progressCounter = await ProcessEvents(ratingEvents, userRateLimits, usersToScrobble.Count, progressCounter,
                totalProgress, evt => Task.FromResult(new ScrobbleDto()
            {
                Format = evt.Format,
                AniListId = evt.AniListId,
                MALId = (int?) evt.MalId,
                ScrobbleEventType = evt.ScrobbleEventType,
                AniListToken = evt.AppUser.AniListAccessToken,
                SeriesName = evt.Series.Name,
                LocalizedSeriesName = evt.Series.LocalizedName,
                Rating = evt.Rating,
                Year = evt.Series.Metadata.ReleaseYear
            }));

            progressCounter = await ProcessEvents(decisions, userRateLimits, usersToScrobble.Count, progressCounter,
                totalProgress, evt => Task.FromResult(new ScrobbleDto()
                {
                    Format = evt.Format,
                    AniListId = evt.AniListId,
                    MALId = (int?) evt.MalId,
                    ScrobbleEventType = evt.ScrobbleEventType,
                    ChapterNumber = evt.ChapterNumber,
                    VolumeNumber = (int?) evt.VolumeNumber,
                    AniListToken = evt.AppUser.AniListAccessToken,
                    SeriesName = evt.Series.Name,
                    LocalizedSeriesName = evt.Series.LocalizedName,
                    Year = evt.Series.Metadata.ReleaseYear
                }));

            // After decisions, we need to mark all the want to read and remove from want to read as completed
            if (decisions.All(d => d.IsProcessed))
            {
                foreach (var scrobbleEvent in addToWantToRead)
                {
                    scrobbleEvent.IsProcessed = true;
                    scrobbleEvent.ProcessDateUtc = DateTime.UtcNow;
                    _unitOfWork.ScrobbleRepository.Update(scrobbleEvent);
                }
                foreach (var scrobbleEvent in removeWantToRead)
                {
                    scrobbleEvent.IsProcessed = true;
                    scrobbleEvent.ProcessDateUtc = DateTime.UtcNow;
                    _unitOfWork.ScrobbleRepository.Update(scrobbleEvent);
                }
                await _unitOfWork.CommitAsync();
            }
        }
        catch (FlurlHttpException)
        {
            _logger.LogError("Kavita+ API or a Scrobble service may be experiencing an outage. Stopping sending data");
            return;
        }


        await SaveToDb(progressCounter, true);
        _logger.LogInformation("Scrobbling Events is complete");
    }


    private async Task<int> ProcessEvents(IEnumerable<ScrobbleEvent> events, IDictionary<int, int> userRateLimits,
        int usersToScrobble, int progressCounter, int totalProgress, Func<ScrobbleEvent, Task<ScrobbleDto>> createEvent)
    {
        var license = await _unitOfWork.SettingsRepository.GetSettingAsync(ServerSettingKey.LicenseKey);
        foreach (var evt in events)
        {
            _logger.LogDebug("Processing Reading Events: {Count} / {Total}", progressCounter, totalProgress);
            progressCounter++;

            // Check if this media item can even be processed for this user
            if (!CanProcessScrobbleEvent(evt))
            {
                continue;
            }

            if (TokenService.HasTokenExpired(evt.AppUser.AniListAccessToken))
            {
                _unitOfWork.ScrobbleRepository.Attach(new ScrobbleError()
                {
                    Comment = "AniList token has expired and needs rotating. Scrobbles wont work until then",
                    Details = $"User: {evt.AppUser.UserName}",
                    LibraryId = evt.LibraryId,
                    SeriesId = evt.SeriesId
                });
                await _unitOfWork.CommitAsync();
                return 0;
            }

            if (await _unitOfWork.ExternalSeriesMetadataRepository.IsBlacklistedSeries(evt.SeriesId))
            {
                _unitOfWork.ScrobbleRepository.Attach(new ScrobbleError()
                {
                    Comment = UnknownSeriesErrorMessage,
                    Details = $"User: {evt.AppUser.UserName} Series: {evt.Series.Name}",
                    LibraryId = evt.LibraryId,
                    SeriesId = evt.SeriesId
                });
                evt.IsErrored = true;
                evt.ErrorDetails = UnknownSeriesErrorMessage;
                evt.ProcessDateUtc = DateTime.UtcNow;
                _unitOfWork.ScrobbleRepository.Update(evt);
                await _unitOfWork.CommitAsync();
                return 0;
            }

            var count = await SetAndCheckRateLimit(userRateLimits, evt.AppUser, license.Value);
            userRateLimits[evt.AppUserId] = count;
            if (count == 0)
            {
                if (usersToScrobble == 1) break;
                continue;
            }

            try
            {
                var data = await createEvent(evt);
                // We need to handle the encoding and changing it to the old one until we can update the API layer to handle these
                // which could happen in v0.8.3
                if (data.VolumeNumber is Parser.SpecialVolumeNumber)
                {
                    data.VolumeNumber = 0;
                }
                if (data.VolumeNumber is Parser.DefaultChapterNumber)
                {
                    data.VolumeNumber = 0;
                }
                if (data.ChapterNumber is Parser.DefaultChapterNumber)
                {
                    data.ChapterNumber = 0;
                }
                userRateLimits[evt.AppUserId] = await PostScrobbleUpdate(data, license.Value, evt);
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
                    _logger.LogCritical("Access Token for UserId: {UserId} needs to be regenerated/renewed to continue scrobbling", evt.AppUser.Id);
                    evt.IsErrored = true;
                    evt.ErrorDetails = AccessTokenErrorMessage;
                    _unitOfWork.ScrobbleRepository.Update(evt);
                    return progressCounter;
                }
            }
            catch (Exception)
            {
                /* Swallow as it's already been handled in PostScrobbleUpdate */
            }
            await SaveToDb(progressCounter);
            // We can use count to determine how long to sleep based on rate gain. It might be specific to AniList, but we can model others
            var delay = count > 10 ? TimeSpan.FromMilliseconds(ScrobbleSleepTime) : TimeSpan.FromSeconds(60);
            await Task.Delay(delay);
        }

        await SaveToDb(progressCounter, true);
        return progressCounter;
    }

    private async Task SaveToDb(int progressCounter, bool force = false)
    {
        if (!force || progressCounter % 5 == 0)
        {
            _logger.LogDebug("Saving Progress");
            await _unitOfWork.CommitAsync();
        }
    }


    private static bool CanProcessScrobbleEvent(ScrobbleEvent readEvent)
    {
        var userProviders = GetUserProviders(readEvent.AppUser);
        if (readEvent.Series.Library.Type == LibraryType.Manga && MangaProviders.Intersect(userProviders).Any())
        {
            return true;
        }

        if (readEvent.Series.Library.Type == LibraryType.Comic &&
            ComicProviders.Intersect(userProviders).Any())
        {
            return true;
        }

        if (readEvent.Series.Library.Type == LibraryType.Book &&
            BookProviders.Intersect(userProviders).Any())
        {
            return true;
        }

        if (readEvent.Series.Library.Type == LibraryType.LightNovel &&
            LightNovelProviders.Intersect(userProviders).Any())
        {
            return true;
        }

        return false;
    }

    private static IList<ScrobbleProvider> GetUserProviders(AppUser appUser)
    {
        var providers = new List<ScrobbleProvider>();
        if (!string.IsNullOrEmpty(appUser.AniListAccessToken)) providers.Add(ScrobbleProvider.AniList);
        return providers;
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
                if (int.TryParse(value, out var intValue))
                    return (T)(object)intValue;
            }
            else if (typeof(T) == typeof(int))
            {
                if (int.TryParse(value, out var intValue))
                    return (T)(object)intValue;
                return default;
            }
            else if (typeof(T) == typeof(long?))
            {
                if (long.TryParse(value, out var longValue))
                    return (T)(object)longValue;
            }
            else if (typeof(T) == typeof(string))
            {
                return (T)(object)value;
            }
        }

        return default(T?);
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

        if (id == null)
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
        if (id is null or 0) return string.Empty;
        return $"{url}{id}/";
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
            _logger.LogInformation("User {UserName} had an issue figuring out rate: {Message}", user.UserName, ex.Message);
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
