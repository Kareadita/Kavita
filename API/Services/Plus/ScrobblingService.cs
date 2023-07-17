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
using API.Entities.Scrobble;
using API.Helpers;
using API.SignalR;
using Flurl.Http;
using Hangfire;
using Kavita.Common;
using Kavita.Common.EnvironmentInfo;
using Kavita.Common.Helpers;
using Microsoft.Extensions.Logging;

namespace API.Services.Plus;

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
    Task ScrobbleRatingUpdate(int userId, int seriesId, int rating);
    Task ScrobbleReviewUpdate(int userId, int seriesId, string reviewTitle, string reviewBody);
    Task ScrobbleReadingUpdate(int userId, int seriesId);
    Task ScrobbleWantToReadUpdate(int userId, int seriesId, bool onWantToRead);

    [DisableConcurrentExecution(60 * 60 * 60)]
    [AutomaticRetry(Attempts = 3, OnAttemptsExceeded = AttemptsExceededAction.Delete)]
    public Task ClearProcessedEvents();
    [DisableConcurrentExecution(60 * 60 * 60)]
    [AutomaticRetry(Attempts = 3, OnAttemptsExceeded = AttemptsExceededAction.Delete)]
    Task ProcessUpdatesSinceLastSync();
    Task CreateEventsFromExistingHistory(int userId = 0);
}

public class ScrobblingService : IScrobblingService
{
    private readonly IUnitOfWork _unitOfWork;
    private readonly ITokenService _tokenService;
    private readonly IEventHub _eventHub;
    private readonly ILogger<ScrobblingService> _logger;
    private readonly ILicenseService _licenseService;

    public const string AniListWeblinkWebsite = "https://anilist.co/manga/";
    public const string MalWeblinkWebsite = "https://myanimelist.net/manga/";

    private static readonly IDictionary<string, int> WeblinkExtractionMap = new Dictionary<string, int>()
    {
        {AniListWeblinkWebsite, 0},
        {MalWeblinkWebsite, 0},
    };

    private const int ScrobbleSleepTime = 700; // We can likely tie this to AniList's 90 rate / min ((60 * 1000) / 90)

    private static readonly IList<ScrobbleProvider> BookProviders = new List<ScrobbleProvider>()
    {
        ScrobbleProvider.AniList
    };
    private static readonly IList<ScrobbleProvider> ComicProviders = new List<ScrobbleProvider>();
    private static readonly IList<ScrobbleProvider> MangaProviders = new List<ScrobbleProvider>()
    {
        ScrobbleProvider.AniList
    };


    public ScrobblingService(IUnitOfWork unitOfWork, ITokenService tokenService,
        IEventHub eventHub, ILogger<ScrobblingService> logger, ILicenseService licenseService)
    {
        _unitOfWork = unitOfWork;
        _tokenService = tokenService;
        _eventHub = eventHub;
        _logger = logger;
        _licenseService = licenseService;

        FlurlHttp.ConfigureClient(Configuration.KavitaPlusApiUrl, cli =>
            cli.Settings.HttpClientFactory = new UntrustedCertClientFactory());
    }


    /// <summary>
    ///
    /// </summary>
    /// <remarks>This service can validate without license check as the task which calls will be guarded</remarks>
    /// <returns></returns>
    public async Task CheckExternalAccessTokens()
    {
        // Validate AniList
        var users = await _unitOfWork.UserRepository.GetAllUsersAsync();
        foreach (var user in users)
        {
            if (string.IsNullOrEmpty(user.AniListAccessToken) || !_tokenService.HasTokenExpired(user.AniListAccessToken)) continue;
            await _eventHub.SendMessageToAsync(MessageFactory.ScrobblingKeyExpired,
                MessageFactory.ScrobblingKeyExpiredEvent(ScrobbleProvider.AniList), user.Id);
        }
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
            !_tokenService.HasTokenExpired(token)) return false;

        var license = await _unitOfWork.SettingsRepository.GetSettingAsync(ServerSettingKey.LicenseKey);
        if (string.IsNullOrEmpty(license.Value)) return true;

        try
        {
            var response = await (Configuration.KavitaPlusApiUrl + "/api/scrobbling/valid-key?provider=" + provider + "&key=" + token)
                .WithHeader("Accept", "application/json")
                .WithHeader("User-Agent", "Kavita")
                .WithHeader("x-license-key", license.Value)
                .WithHeader("x-installId", HashUtil.ServerToken())
                .WithHeader("x-kavita-version", BuildInfo.Version)
                .WithHeader("Content-Type", "application/json")
                .WithTimeout(TimeSpan.FromSeconds(Configuration.DefaultTimeOutSecs))
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
        if (user == null) return null;

        return provider switch
        {
            ScrobbleProvider.AniList => user.AniListAccessToken,
            _ => string.Empty
        };
    }

    public async Task ScrobbleReviewUpdate(int userId, int seriesId, string reviewTitle, string reviewBody)
    {
        if (!await _licenseService.HasActiveLicense()) return;
        var token = await GetTokenForProvider(userId, ScrobbleProvider.AniList);
        if (await HasTokenExpired(token, ScrobbleProvider.AniList))
        {
            throw new KavitaException("AniList Credentials have expired or not set");
        }

        var series = await _unitOfWork.SeriesRepository.GetSeriesByIdAsync(seriesId, SeriesIncludes.Metadata | SeriesIncludes.Library);
        if (series == null) throw new KavitaException("Series not found");
        var library = await _unitOfWork.LibraryRepository.GetLibraryForIdAsync(series.LibraryId);
        if (library is not {AllowScrobbling: true}) return;
        if (library.Type == LibraryType.Comic) return;

        var existingEvt = await _unitOfWork.ScrobbleRepository.GetEvent(userId, series.Id,
            ScrobbleEventType.Review);
        if (existingEvt is {IsProcessed: false})
        {
            _logger.LogDebug("Overriding scrobble event for {Series} from Review {Tagline}/{Body} -> {UpdatedTagline}{UpdatedBody}",
                existingEvt.Series.Name, existingEvt.ReviewTitle, existingEvt.ReviewBody, reviewTitle, reviewBody);
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
            AniListId = (int?) ExtractId(series.Metadata.WebLinks, AniListWeblinkWebsite),
            MalId = ExtractId(series.Metadata.WebLinks, MalWeblinkWebsite),
            AppUserId = userId,
            Format = LibraryTypeHelper.GetFormat(series.Library.Type),
            ReviewBody = reviewBody,
            ReviewTitle = reviewTitle
        };
        _unitOfWork.ScrobbleRepository.Attach(evt);
        await _unitOfWork.CommitAsync();
        _logger.LogDebug("Added Scrobbling Review update on {SeriesName} with Userid {UserId} ", series.Name, userId);
    }

    public async Task ScrobbleRatingUpdate(int userId, int seriesId, int rating)
    {
        if (!await _licenseService.HasActiveLicense()) return;
        var token = await GetTokenForProvider(userId, ScrobbleProvider.AniList);
        if (await HasTokenExpired(token, ScrobbleProvider.AniList))
        {
            throw new KavitaException("AniList Credentials have expired or not set");
        }

        var series = await _unitOfWork.SeriesRepository.GetSeriesByIdAsync(seriesId, SeriesIncludes.Metadata | SeriesIncludes.Library);
        if (series == null) throw new KavitaException("Series not found");
        var library = await _unitOfWork.LibraryRepository.GetLibraryForIdAsync(series.LibraryId);
        if (library is not {AllowScrobbling: true}) return;
        if (library.Type == LibraryType.Comic) return;

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
            AniListId = (int?) ExtractId(series.Metadata.WebLinks, AniListWeblinkWebsite),
            MalId = ExtractId(series.Metadata.WebLinks, MalWeblinkWebsite),
            AppUserId = userId,
            Format = LibraryTypeHelper.GetFormat(series.Library.Type),
            Rating = rating
        };
        _unitOfWork.ScrobbleRepository.Attach(evt);
        await _unitOfWork.CommitAsync();
        _logger.LogDebug("Added Scrobbling Rating update on {SeriesName} with Userid {UserId} ", series.Name, userId);
    }

    public async Task ScrobbleReadingUpdate(int userId, int seriesId)
    {
        if (!await _licenseService.HasActiveLicense()) return;
        var token = await GetTokenForProvider(userId, ScrobbleProvider.AniList);
        if (await HasTokenExpired(token, ScrobbleProvider.AniList))
        {
            throw new KavitaException("AniList Credentials have expired or not set");
        }

        var series = await _unitOfWork.SeriesRepository.GetSeriesByIdAsync(seriesId, SeriesIncludes.Metadata | SeriesIncludes.Library);
        if (series == null) throw new KavitaException("Series not found");
        if (await _unitOfWork.UserRepository.HasHoldOnSeries(userId, seriesId))
        {
            _logger.LogInformation("Series {SeriesName} is on UserId {UserId}'s hold list. Not scrobbling", series.Name, userId);
            return;
        }
        var library = await _unitOfWork.LibraryRepository.GetLibraryForIdAsync(series.LibraryId);
        if (library is not {AllowScrobbling: true}) return;
        if (library.Type == LibraryType.Comic) return;

        var existingEvt = await _unitOfWork.ScrobbleRepository.GetEvent(userId, series.Id,
            ScrobbleEventType.ChapterRead);
        if (existingEvt is {IsProcessed: false})
        {
            // We need to just update Volume/Chapter number
            var prevChapter = $"{existingEvt.ChapterNumber}";
            var prevVol = $"{existingEvt.VolumeNumber}";

            existingEvt.VolumeNumber =
                await _unitOfWork.AppUserProgressRepository.GetHighestFullyReadVolumeForSeries(seriesId, userId);
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
                AniListId = (int?) ExtractId(series.Metadata.WebLinks, AniListWeblinkWebsite),
                MalId = ExtractId(series.Metadata.WebLinks, MalWeblinkWebsite),
                AppUserId = userId,
                VolumeNumber =
                    await _unitOfWork.AppUserProgressRepository.GetHighestFullyReadVolumeForSeries(seriesId, userId),
                ChapterNumber =
                    await _unitOfWork.AppUserProgressRepository.GetHighestFullyReadChapterForSeries(seriesId, userId),
                Format = LibraryTypeHelper.GetFormat(series.Library.Type),
            };
            _unitOfWork.ScrobbleRepository.Attach(evt);
            await _unitOfWork.CommitAsync();
            _logger.LogDebug("Added Scrobbling Read update on {SeriesName} with Userid {UserId} ", series.Name, userId);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "There was an issue when saving scrobble read event");
        }
    }

    public async Task ScrobbleWantToReadUpdate(int userId, int seriesId, bool onWantToRead)
    {
        if (!await _licenseService.HasActiveLicense()) return;
        var token = await GetTokenForProvider(userId, ScrobbleProvider.AniList);
        if (await HasTokenExpired(token, ScrobbleProvider.AniList))
        {
            throw new KavitaException("AniList Credentials have expired or not set");
        }

        var series = await _unitOfWork.SeriesRepository.GetSeriesByIdAsync(seriesId, SeriesIncludes.Metadata | SeriesIncludes.Library);
        if (series == null) throw new KavitaException("Series not found");
        var library = await _unitOfWork.LibraryRepository.GetLibraryForIdAsync(series.LibraryId);
        if (library is not {AllowScrobbling: true}) return;
        if (library.Type == LibraryType.Comic) return;

        var existing = await _unitOfWork.ScrobbleRepository.Exists(userId, series.Id,
            onWantToRead ? ScrobbleEventType.AddWantToRead : ScrobbleEventType.RemoveWantToRead);
        if (existing) return;

        var evt = new ScrobbleEvent()
        {
            SeriesId = series.Id,
            LibraryId = series.LibraryId,
            ScrobbleEventType = onWantToRead ? ScrobbleEventType.AddWantToRead : ScrobbleEventType.RemoveWantToRead,
            AniListId = (int?) ExtractId(series.Metadata.WebLinks, AniListWeblinkWebsite),
            MalId = ExtractId(series.Metadata.WebLinks, MalWeblinkWebsite),
            AppUserId = userId,
            Format = LibraryTypeHelper.GetFormat(series.Library.Type),
        };
        _unitOfWork.ScrobbleRepository.Attach(evt);
        await _unitOfWork.CommitAsync();
        _logger.LogDebug("Added Scrobbling WantToRead update on {SeriesName} with Userid {UserId} ", series.Name, userId);
    }

    private async Task<int> GetRateLimit(string license, string aniListToken)
    {
        try
        {
            var response = await (Configuration.KavitaPlusApiUrl + "/api/scrobbling/rate-limit?accessToken=" + aniListToken)
                .WithHeader("Accept", "application/json")
                .WithHeader("User-Agent", "Kavita")
                .WithHeader("x-license-key", license)
                .WithHeader("x-installId", HashUtil.ServerToken())
                .WithHeader("x-kavita-version", BuildInfo.Version)
                .WithHeader("Content-Type", "application/json")
                .WithTimeout(TimeSpan.FromSeconds(Configuration.DefaultTimeOutSecs))
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
                .WithHeader("Accept", "application/json")
                .WithHeader("User-Agent", "Kavita")
                .WithHeader("x-license-key", license)
                .WithHeader("x-installId", HashUtil.ServerToken())
                .WithHeader("x-kavita-version", BuildInfo.Version)
                .WithHeader("Content-Type", "application/json")
                .WithTimeout(TimeSpan.FromSeconds(Configuration.DefaultTimeOutSecs))
                .PostJsonAsync(data)
                .ReceiveJson<ScrobbleResponseDto>();

            if (!response.Successful)
            {
                // Might want to log this under ScrobbleError
                if (response.ErrorMessage != null && response.ErrorMessage.Contains("Too Many Requests"))
                {
                    _logger.LogInformation("Hit Too many requests, sleeping to regain requests");
                    await Task.Delay(TimeSpan.FromMinutes(1));
                } else if (response.ErrorMessage != null && response.ErrorMessage.Contains("Unknown Series"))
                {
                    // Log the Series name and Id in ScrobbleErrors
                    _logger.LogInformation("Kavita+ was unable to match the series");
                    if (!await _unitOfWork.ScrobbleRepository.HasErrorForSeries(evt.SeriesId))
                    {
                        _unitOfWork.ScrobbleRepository.Attach(new ScrobbleError()
                        {
                            Comment = "Unknown Series",
                            Details = data.SeriesName,
                            LibraryId = evt.LibraryId,
                            SeriesId = evt.SeriesId
                        });
                    }
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
                }

                _logger.LogError("Scrobbling failed due to {ErrorMessage}: {SeriesName}", response.ErrorMessage, data.SeriesName);
                throw new KavitaException($"Scrobbling failed due to {response.ErrorMessage}: {data.SeriesName}");
            }

            return response.RateLeft;
        }
        catch (FlurlHttpException  ex)
        {
            _logger.LogError("Scrobbling to Kavita+ API failed due to error: {ErrorMessage}", ex.Message);
            if (ex.Message.Contains("Call failed with status code 500 (Internal Server Error)"))
            {
                if (!await _unitOfWork.ScrobbleRepository.HasErrorForSeries(evt.SeriesId))
                {
                    _unitOfWork.ScrobbleRepository.Attach(new ScrobbleError()
                    {
                        Comment = "Unknown Series",
                        Details = data.SeriesName,
                        LibraryId = evt.LibraryId,
                        SeriesId = evt.SeriesId
                    });
                }
                throw new KavitaException("Bad payload from Scrobble Provider");
            }
            throw;
        }
    }

    /// <summary>
    /// This will back fill events from existing progress history, ratings, and want to read for users that have a valid license
    /// </summary>
    /// <param name="userId">Defaults to 0 meaning all users. Allows a userId to be set if a scrobble key is added to a user</param>
    public async Task CreateEventsFromExistingHistory(int userId = 0)
    {
        var libAllowsScrobbling = (await _unitOfWork.LibraryRepository.GetLibrariesAsync())
            .ToDictionary(lib => lib.Id, lib => lib.AllowScrobbling);

        var userIds = (await _unitOfWork.UserRepository.GetAllUsersAsync())
            .Where(l => userId == 0 || userId == l.Id)
            .Select(u => u.Id);

        if (!await _licenseService.HasActiveLicense()) return;

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

            var reviews = await _unitOfWork.UserRepository.GetSeriesWithReviews(uId);
            foreach (var review in reviews)
            {
                if (!libAllowsScrobbling[review.Series.LibraryId]) continue;
                await ScrobbleReviewUpdate(uId, review.SeriesId, review.Tagline, review.Review);
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
                await ScrobbleReadingUpdate(uId, series.Id);
            }

        }
    }

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
        // Check how many scrobbles we have available then only do those.
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
            .Where(e => e.Comment == "Unknown Series")
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
        var reviewEvents = (await _unitOfWork.ScrobbleRepository.GetByEvent(ScrobbleEventType.Review))
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
            .DistinctBy(u => u.Id)
            .ToList();
        foreach (var user in usersToScrobble)
        {
            await SetAndCheckRateLimit(userRateLimits, user, license.Value);
        }

        var totalProgress = readEvents.Count + addToWantToRead.Count + removeWantToRead.Count + ratingEvents.Count + decisions.Count + reviewEvents.Count;

        _logger.LogInformation("Found {TotalEvents} Scrobble Events", totalProgress);
        try
        {
            // Recalculate the highest volume/chapter
            foreach (var readEvt in readEvents)
            {
                readEvt.VolumeNumber =
                    await _unitOfWork.AppUserProgressRepository.GetHighestFullyReadVolumeForSeries(readEvt.SeriesId,
                        readEvt.AppUser.Id);
                readEvt.ChapterNumber =
                    await _unitOfWork.AppUserProgressRepository.GetHighestFullyReadChapterForSeries(readEvt.SeriesId,
                        readEvt.AppUser.Id);
                _unitOfWork.ScrobbleRepository.Update(readEvt);
            }
            progressCounter = await ProcessEvents(readEvents, userRateLimits, usersToScrobble.Count, progressCounter, totalProgress, evt => new ScrobbleDto()
            {
                Format = evt.Format,
                AniListId = evt.AniListId,
                MALId = (int?) evt.MalId,
                ScrobbleEventType = evt.ScrobbleEventType,
                ChapterNumber = evt.ChapterNumber,
                VolumeNumber = evt.VolumeNumber,
                AniListToken = evt.AppUser.AniListAccessToken,
                SeriesName = evt.Series.Name,
                LocalizedSeriesName = evt.Series.LocalizedName,
                StartedReadingDateUtc = evt.CreatedUtc,
                ScrobbleDateUtc = evt.LastModifiedUtc,
                Year = evt.Series.Metadata.ReleaseYear
            });

            progressCounter = await ProcessEvents(ratingEvents, userRateLimits, usersToScrobble.Count, progressCounter, totalProgress, evt => new ScrobbleDto()
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
            });

            progressCounter = await ProcessEvents(reviewEvents, userRateLimits, usersToScrobble.Count, progressCounter, totalProgress, evt => new ScrobbleDto()
            {
                Format = evt.Format,
                AniListId = evt.AniListId,
                MALId = (int?) evt.MalId,
                ScrobbleEventType = evt.ScrobbleEventType,
                AniListToken = evt.AppUser.AniListAccessToken,
                SeriesName = evt.Series.Name,
                LocalizedSeriesName = evt.Series.LocalizedName,
                Rating = evt.Rating,
                Year = evt.Series.Metadata.ReleaseYear,
                ReviewBody = evt.ReviewBody,
                ReviewTitle = evt.ReviewTitle
            });

            progressCounter = await ProcessEvents(decisions, userRateLimits, usersToScrobble.Count, progressCounter, totalProgress, evt => new ScrobbleDto()
            {
                Format = evt.Format,
                AniListId = evt.AniListId,
                MALId = (int?) evt.MalId,
                ScrobbleEventType = evt.ScrobbleEventType,
                ChapterNumber = evt.ChapterNumber,
                VolumeNumber = evt.VolumeNumber,
                AniListToken = evt.AppUser.AniListAccessToken,
                SeriesName = evt.Series.Name,
                LocalizedSeriesName = evt.Series.LocalizedName,
                Year = evt.Series.Metadata.ReleaseYear
            });
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
        int usersToScrobble, int progressCounter, int totalProgress, Func<ScrobbleEvent, ScrobbleDto> createEvent)
    {
        var license = await _unitOfWork.SettingsRepository.GetSettingAsync(ServerSettingKey.LicenseKey);
        foreach (var evt in events)
        {
            _logger.LogDebug("Processing Reading Events: {Count} / {Total}", progressCounter, totalProgress);
            progressCounter++;
            // Check if this media item can even be processed for this user
            if (!DoesUserHaveProviderAndValid(evt))
            {
                continue;
            }
            var count = await SetAndCheckRateLimit(userRateLimits, evt.AppUser, license.Value);
            if (count == 0)
            {
                if (usersToScrobble == 1) break;
                continue;
            }

            try
            {
                var data = createEvent(evt);
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

    private static bool DoesUserHaveProviderAndValid(ScrobbleEvent readEvent)
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
    public static long? ExtractId(string webLinks, string website)
    {
        var index = WeblinkExtractionMap[website];
        foreach (var webLink in webLinks.Split(','))
        {
            if (!webLink.StartsWith(website)) continue;
            var tokens = webLink.Split(website)[1].Split('/');
            return long.Parse(tokens[index]);
        }

        return 0;
    }

    private async Task<int> SetAndCheckRateLimit(IDictionary<int, int> userRateLimits, AppUser user, string license)
    {
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

    public static string CreateUrl(string url, long? id)
    {
        if (id is null or 0) return string.Empty;
        return $"{url}{id}/";
    }
}
