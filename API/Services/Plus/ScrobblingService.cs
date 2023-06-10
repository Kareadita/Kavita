using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using System.Net.Http;
using System.Threading;
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
using Kavita.Common;
using Kavita.Common.EnvironmentInfo;
using Kavita.Common.Helpers;
using Microsoft.Extensions.Logging;

namespace API.Services.Plus;

public enum ScrobbleProvider
{
    AniList = 1
}

public interface IScrobblingService
{
    Task CheckExternalAccessTokens();
    Task<bool> HasTokenExpired(int userId, ScrobbleProvider provider);
    Task ScrobbleRatingUpdate(int userId, int seriesId, int rating);
    Task ScrobbleReadingUpdate(int userId, int seriesId);
    Task ScrobbleWantToReadUpdate(int userId, int seriesId, bool onWantToRead);
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

    public const string AniListWeblinkWebsite = "https://anilist.co/";
    public const string MalWeblinkWebsite = "https://myanimelist.net/manga/";

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

        if (await HasTokenExpired(userId, token, provider))
        {
            // NOTE: Should this side effect be here?
            await _eventHub.SendMessageToAsync(MessageFactory.ScrobblingKeyExpired,
                MessageFactory.ScrobblingKeyExpiredEvent(ScrobbleProvider.AniList), userId);
            return true;
        }

        return false;
    }

    private async Task<bool> HasTokenExpired(int userId, string token, ScrobbleProvider provider)
    {
        if (string.IsNullOrEmpty(token) ||
            !_tokenService.HasTokenExpired(token)) return false;

        var user = await _unitOfWork.UserRepository.GetUserByIdAsync(userId);
        if (string.IsNullOrEmpty(user?.License)) return true;

        try
        {
            var response = await (Configuration.KavitaPlusApiUrl + "/api/scrobbling/valid-key?provider=" + provider + "&key=" + token)
                .WithHeader("Accept", "application/json")
                .WithHeader("User-Agent", "Kavita")
                .WithHeader("x-license-key", user.License)
                .WithHeader("x-kavita-version", BuildInfo.Version)
                .WithHeader("Content-Type", "application/json")
                .WithTimeout(TimeSpan.FromSeconds(Configuration.DefaultTimeOutSecs))
                .GetStringAsync();

            return bool.Parse(response);
        }
        catch (HttpRequestException e)
        {
            _logger.LogError(e, "An error happened during the request to KavitaPlus API");
        }
        catch (Exception e)
        {
            _logger.LogError(e, "An error happened during the request to KavitaPlus API");
        }

        return true;
    }

    private async Task<string?> GetTokenForProvider(int userId, ScrobbleProvider provider)
    {
        var user = await _unitOfWork.UserRepository.GetUserByIdAsync(userId);
        if (user == null) return null;

        return provider switch
        {
            ScrobbleProvider.AniList => user.AniListAccessToken,
            _ => string.Empty
        };
    }

    public async Task ScrobbleRatingUpdate(int userId, int seriesId, int rating)
    {
        if (!await _licenseService.HasActiveLicense(userId)) return;
        var token = await GetTokenForProvider(userId, ScrobbleProvider.AniList);
        if (await HasTokenExpired(userId, token, ScrobbleProvider.AniList))
        {
            throw new KavitaException("AniList Credentials have expired or not set");
        }

        var series = await _unitOfWork.SeriesRepository.GetSeriesByIdAsync(seriesId, SeriesIncludes.Metadata | SeriesIncludes.Library);
        if (series == null) throw new KavitaException("Series not found");
        var library = await _unitOfWork.LibraryRepository.GetLibraryForIdAsync(series.LibraryId);
        if (library is not {AllowScrobbling: true}) return;

        var existing = await _unitOfWork.ScrobbleRepository.Exists(userId, series.Id,
            ScrobbleEventType.ScoreUpdated);
        if (existing) return;

        var evt = new ScrobbleEvent()
        {
            SeriesId = series.Id,
            LibraryId = series.LibraryId,
            ScrobbleEventType = ScrobbleEventType.ScoreUpdated,
            AniListId = ExtractId(series.Metadata.WebLinks, AniListWeblinkWebsite),
            AppUserId = userId,
            Format = LibraryTypeHelper.GetFormat(series.Library.Type),
        };
        _unitOfWork.ScrobbleRepository.Attach(evt);
        await _unitOfWork.CommitAsync();
    }

    public async Task ScrobbleReadingUpdate(int userId, int seriesId)
    {
        if (!await _licenseService.HasActiveLicense(userId)) return;
        var token = await GetTokenForProvider(userId, ScrobbleProvider.AniList);
        if (await HasTokenExpired(userId, token, ScrobbleProvider.AniList))
        {
            throw new KavitaException("AniList Credentials have expired or not set");
        }

        var series = await _unitOfWork.SeriesRepository.GetSeriesByIdAsync(seriesId, SeriesIncludes.Metadata | SeriesIncludes.Library);
        if (series == null) throw new KavitaException("Series not found");
        var library = await _unitOfWork.LibraryRepository.GetLibraryForIdAsync(series.LibraryId);
        if (library is not {AllowScrobbling: true}) return;

        var existing = await _unitOfWork.ScrobbleRepository.Exists(userId, series.Id,
            ScrobbleEventType.ChapterRead);
        if (existing) return;

        try
        {
            var evt = new ScrobbleEvent()
            {
                SeriesId = series.Id,
                LibraryId = series.LibraryId,
                ScrobbleEventType = ScrobbleEventType.ChapterRead,
                AniListId = ExtractId(series.Metadata.WebLinks, AniListWeblinkWebsite),
                AppUserId = userId,
                VolumeNumber =
                    await _unitOfWork.AppUserProgressRepository.GetHighestFullyReadVolumeForSeries(seriesId, userId),
                ChapterNumber =
                    await _unitOfWork.AppUserProgressRepository.GetHighestFullyReadChapterForSeries(seriesId, userId),
                Format = LibraryTypeHelper.GetFormat(series.Library.Type),
            };
            _unitOfWork.ScrobbleRepository.Attach(evt);
            await _unitOfWork.CommitAsync();
            _logger.LogDebug("Scrobbling Record on {SeriesName} with Userid {UserId} ", series.Name, userId);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "There was an issue when saving scrobble read event");
        }
    }

    public async Task ScrobbleWantToReadUpdate(int userId, int seriesId, bool onWantToRead)
    {
        if (!await _licenseService.HasActiveLicense(userId)) return;
        var token = await GetTokenForProvider(userId, ScrobbleProvider.AniList);
        if (await HasTokenExpired(userId, token, ScrobbleProvider.AniList))
        {
            throw new KavitaException("AniList Credentials have expired or not set");
        }

        var series = await _unitOfWork.SeriesRepository.GetSeriesByIdAsync(seriesId, SeriesIncludes.Metadata | SeriesIncludes.Library);
        if (series == null) throw new KavitaException("Series not found");
        var library = await _unitOfWork.LibraryRepository.GetLibraryForIdAsync(series.LibraryId);
        if (library is not {AllowScrobbling: true}) return;

        var existing = await _unitOfWork.ScrobbleRepository.Exists(userId, series.Id,
            onWantToRead ? ScrobbleEventType.AddWantToRead : ScrobbleEventType.RemoveWantToRead);
        if (existing) return;

        var evt = new ScrobbleEvent()
        {
            SeriesId = series.Id,
            LibraryId = series.LibraryId,
            ScrobbleEventType = onWantToRead ? ScrobbleEventType.AddWantToRead : ScrobbleEventType.RemoveWantToRead,
            AniListId = ExtractId(series.Metadata.WebLinks, AniListWeblinkWebsite),
            AppUserId = userId,
            Format = LibraryTypeHelper.GetFormat(series.Library.Type),
        };
        _unitOfWork.ScrobbleRepository.Attach(evt);
        await _unitOfWork.CommitAsync();
    }

    private async Task<int> GetRateLimit(string license, string aniListToken)
    {
        var serverSetting = await _unitOfWork.SettingsRepository.GetSettingsDtoAsync();
        try
        {
            var response = await (Configuration.KavitaPlusApiUrl + "/api/scrobbling/rate-limit?accessToken=" + aniListToken)
                .WithHeader("Accept", "application/json")
                .WithHeader("User-Agent", "Kavita")
                .WithHeader("x-license-key", license)
                .WithHeader("x-installId", serverSetting.InstallId)
                .WithHeader("x-kavita-version", BuildInfo.Version)
                .WithHeader("Content-Type", "application/json")
                .WithTimeout(TimeSpan.FromSeconds(Configuration.DefaultTimeOutSecs))
                .GetStringAsync();

            return int.Parse(response);
        }
        catch (Exception e)
        {
            _logger.LogError(e, "An error happened during the request to KavitaPlus API");
        }

        return 0;
    }

    private async Task<int> PostScrobbleUpdate(ScrobbleDto data, string license, ScrobbleEvent evt)
    {
        var serverSetting = await _unitOfWork.SettingsRepository.GetSettingsDtoAsync();
        try
        {
            var response = await (Configuration.KavitaPlusApiUrl + "/api/scrobbling/anilist/update")
                .WithHeader("Accept", "application/json")
                .WithHeader("User-Agent", "Kavita")
                .WithHeader("x-license-key", license)
                .WithHeader("x-installId", serverSetting.InstallId)
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
                    _logger.LogError("Hit Too many requests, sleeping to regain requests");
                    await Task.Delay(TimeSpan.FromMinutes(1));
                } else if (response.ErrorMessage != null && response.ErrorMessage.Contains("Unknown Series"))
                {
                    // Log the Series name and Id in ScrobbleErrors
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
                }

                _logger.LogError("Scrobbling failed due to {ErrorMessage}: {SeriesName}", response.ErrorMessage, data.SeriesName);
                throw new KavitaException($"Scrobbling failed due to {response.ErrorMessage}: {data.SeriesName}");
            }

            return response.RateLeft;
        }
        catch (FlurlHttpException  ex)
        {
            _logger.LogError("Scrobbling to KavitaPlus API failed due to error: {ErrorMessage}", ex.Message);
            if (ex.Message.Contains("Call failed with status code 500 (Internal Server Error)"))
            {
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

        var users = (await _unitOfWork.UserRepository.GetAllUsersAsync())
            .Where(l => userId == 0 || userId == l.Id);
        foreach (var user in users)
        {
            if (!(await _licenseService.HasActiveLicense(user.Id))) continue;

            var wantToRead = await _unitOfWork.SeriesRepository.GetWantToReadForUserAsync(user.Id);
            foreach (var wtr in wantToRead)
            {
                if (!libAllowsScrobbling[wtr.LibraryId]) continue;
                await ScrobbleWantToReadUpdate(user.Id, wtr.Id, true);
            }

            var ratings = await _unitOfWork.UserRepository.GetSeriesWithRatings(user.Id);
            foreach (var rating in ratings)
            {
                if (!libAllowsScrobbling[rating.Series.LibraryId]) continue;
                await ScrobbleRatingUpdate(user.Id, rating.SeriesId, rating.Rating);
            }

            var seriesWithProgress = await _unitOfWork.SeriesRepository.GetSeriesDtoForLibraryIdAsync(0, user.Id,
                new UserParams() {}, new FilterDto()
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
                await ScrobbleReadingUpdate(user.Id, series.Id);
            }

        }

        // Update SyncHistory saying we've processed all rows
        //await _unitOfWork.SyncHistoryRepository.Update(SyncKey.Scrobble); // TODO: Remove this migration and table

    }

    /// <summary>
    /// This is a task that is ran on a fixed schedule (every few hours or every day) that clears out the scrobble event table
    /// and offloads the data to the API server which performs the syncing to the providers.
    /// </summary>
    public async Task ProcessUpdatesSinceLastSync()
    {
        // Check how many scrobbles we have available then only do those.
        var userRateLimits = new Dictionary<int, int>();

        var progressCounter = 0;

        var librariesWithScrobbling = (await _unitOfWork.LibraryRepository.GetLibrariesAsync())
            .ToList()
            .Where(l => l.AllowScrobbling)
            .Select(l => l.Id)
            .ToImmutableHashSet();

        var readEvents = (await _unitOfWork.ScrobbleRepository.GetByEvent(ScrobbleEventType.ChapterRead))
            .Where(e => librariesWithScrobbling.Contains(e.LibraryId))
            .ToList();
        var addToWantToRead = (await _unitOfWork.ScrobbleRepository.GetByEvent(ScrobbleEventType.AddWantToRead))
            .Where(e => librariesWithScrobbling.Contains(e.LibraryId))
            .ToList();
        var removeWantToRead = (await _unitOfWork.ScrobbleRepository.GetByEvent(ScrobbleEventType.RemoveWantToRead))
            .Where(e => librariesWithScrobbling.Contains(e.LibraryId))
            .ToList();
        var ratingEvents = (await _unitOfWork.ScrobbleRepository.GetByEvent(ScrobbleEventType.ScoreUpdated))
            .Where(e => librariesWithScrobbling.Contains(e.LibraryId))
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
            await SetAndCheckRateLimit(userRateLimits, user);
        }

        var totalProgress = readEvents.Count + addToWantToRead.Count + removeWantToRead.Count + ratingEvents.Count + decisions.Count;

        try
        {
            progressCounter = await ProcessEvents(readEvents, userRateLimits, usersToScrobble.Count, progressCounter, totalProgress, readEvent => new ScrobbleDto()
            {
                Format = readEvent.Format,
                AniListId = readEvent.AniListId,
                ScrobbleEventType = readEvent.ScrobbleEventType,
                ChapterNumber = readEvent.ChapterNumber,
                VolumeNumber = readEvent.VolumeNumber,
                AniListToken = readEvent.AppUser.AniListAccessToken,
                SeriesName = readEvent.Series.Name,
                LocalizedSeriesName = readEvent.Series.LocalizedName,
                StartedReadingDateUtc = readEvent.CreatedUtc
            });

            progressCounter = await ProcessEvents(ratingEvents, userRateLimits, usersToScrobble.Count, progressCounter, totalProgress, ratingEvent => new ScrobbleDto()
            {
                Format = ratingEvent.Format,
                AniListId = ratingEvent.AniListId,
                ScrobbleEventType = ratingEvent.ScrobbleEventType,
                AniListToken = ratingEvent.AppUser.AniListAccessToken,
                SeriesName = ratingEvent.Series.Name,
                LocalizedSeriesName = ratingEvent.Series.LocalizedName,
                Rating = ratingEvent.Rating
            });

            progressCounter = await ProcessEvents(decisions, userRateLimits, usersToScrobble.Count, progressCounter, totalProgress, decision => new ScrobbleDto()
            {
                Format = decision.Format,
                AniListId = decision.AniListId,
                ScrobbleEventType = decision.ScrobbleEventType,
                ChapterNumber = decision.ChapterNumber,
                VolumeNumber = decision.VolumeNumber,
                AniListToken = decision.AppUser.AniListAccessToken,
                SeriesName = decision.Series.Name,
                LocalizedSeriesName = decision.Series.LocalizedName
            });
        }
        catch (FlurlHttpException)
        {
            _logger.LogError("KavitaPlus API or a Scrobble service may be experiencing an outage. Stopping sending data");
            return;
        }


        await SaveToDb(progressCounter, true);
        _logger.LogInformation("Scrobbling Events is complete");
    }

    private async Task<int> ProcessEvents(IEnumerable<ScrobbleEvent> events, IDictionary<int, int> userRateLimits,
        int usersToScrobble, int progressCounter, int totalProgress, Func<ScrobbleEvent, ScrobbleDto> createEvent)
    {
        foreach (var evt in events)
        {
            _logger.LogDebug("Processing Reading Events: {Count} / {Total}", progressCounter, totalProgress);
            progressCounter++;
            // Check if this media item can even be processed for this user
            if (!DoesUserHaveProviderAndValid(evt)) continue;
            var count = await SetAndCheckRateLimit(userRateLimits, evt.AppUser);
            if (count == 0)
            {
                if (usersToScrobble == 1) break;
                continue;
            }

            try
            {
                var data = createEvent(evt);
                userRateLimits[evt.AppUserId] = await PostScrobbleUpdate(data, evt.AppUser.License, evt);
                _unitOfWork.ScrobbleRepository.Remove(evt);
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
    public static int? ExtractId(string webLinks, string website)
    {
        foreach (var webLink in webLinks.Split(","))
        {
            if (!webLink.StartsWith(website)) continue;
            var tokens = webLink.Split(website)[1].Split("/");
            return int.Parse(tokens[1]);
        }

        return 0;
    }

    private async Task<int> SetAndCheckRateLimit(IDictionary<int, int> userRateLimits, AppUser user)
    {
        try
        {
            if (!userRateLimits.ContainsKey(user.Id))
            {
                var rate = await GetRateLimit(user.License, user.AniListAccessToken);
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
