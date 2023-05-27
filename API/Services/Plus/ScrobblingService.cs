using System;
using System.Collections.Generic;
using System.IdentityModel.Tokens.Jwt;
using System.Linq;
using System.Net.Http;
using System.Threading.Tasks;
using API.Data;
using API.Data.Repositories;
using API.DTOs;
using API.DTOs.Filtering;
using API.DTOs.Scrobbling;
using API.Entities;
using API.Entities.Enums;
using API.Helpers;
using API.SignalR;
using Flurl.Http;
using Hangfire.Storage.SQLite.Entities;
using Kavita.Common;
using Kavita.Common.EnvironmentInfo;
using Kavita.Common.Helpers;
using Microsoft.AspNetCore.Http;
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
    Task CreateEventsFromExistingHistory();
}

public class ScrobblingService : IScrobblingService
{
    private readonly IUnitOfWork _unitOfWork;
    private readonly ITokenService _tokenService;
    private readonly IEventHub _eventHub;
    private readonly ILogger<ScrobblingService> _logger;
    private readonly ILicenseService _licenseService;

    private const string ApiUrl = "http://localhost:5020";
    private const int TimeOutSecs = 30;

    public ScrobblingService(IUnitOfWork unitOfWork, ITokenService tokenService,
        IEventHub eventHub, ILogger<ScrobblingService> logger, ILicenseService licenseService)
    {
        _unitOfWork = unitOfWork;
        _tokenService = tokenService;
        _eventHub = eventHub;
        _logger = logger;
        _licenseService = licenseService;

        FlurlHttp.ConfigureClient(ApiUrl, cli =>
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

        try
        {
            var response = await (ApiUrl + "/api/scrobbling/valid-key?provider=" + provider + "&key=" + token)
                .WithHeader("Accept", "application/json")
                .WithHeader("User-Agent", "Kavita")
                .WithHeader("x-license-key", "TODO")
                .WithHeader("x-kavita-version", BuildInfo.Version)
                .WithHeader("Content-Type", "application/json")
                .WithTimeout(TimeSpan.FromSeconds(TimeOutSecs))
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
        // TODO: License check
        var token = await GetTokenForProvider(userId, ScrobbleProvider.AniList);
        if (await HasTokenExpired(token, ScrobbleProvider.AniList))
        {
            throw new KavitaException("AniList Credentials have expired or not set");
        }

        var series = await _unitOfWork.SeriesRepository.GetSeriesByIdAsync(seriesId, SeriesIncludes.Metadata);
        if (series == null) throw new KavitaException("Series not found");

        var existing = await _unitOfWork.ScrobbleEventRepository.Exists(userId, series.Id,
            ScrobbleEventType.ScoreUpdated);
        if (existing) return;

        var evt = new ScrobbleEvent()
        {
            SeriesId = series.Id,
            LibraryId = series.LibraryId,
            ScrobbleEventType = ScrobbleEventType.ScoreUpdated,
            AniListId = ExtractAniListId(series.Metadata.WebLinks),
            AppUserId = userId,
            Format = MediaFormat.Manga,
        };
        _unitOfWork.ScrobbleEventRepository.Attach(evt);
        await _unitOfWork.CommitAsync();
    }

    public async Task ScrobbleReadingUpdate(int userId, int seriesId)
    {
        // TODO: License check
        var token = await GetTokenForProvider(userId, ScrobbleProvider.AniList);
        if (await HasTokenExpired(token, ScrobbleProvider.AniList))
        {
            throw new KavitaException("AniList Credentials have expired or not set");
        }

        var series = await _unitOfWork.SeriesRepository.GetSeriesByIdAsync(seriesId, SeriesIncludes.Metadata);
        if (series == null) throw new KavitaException("Series not found");

        var existing = await _unitOfWork.ScrobbleEventRepository.Exists(userId, series.Id,
            ScrobbleEventType.ChapterRead);
        if (existing) return;

        try
        {
            var evt = new ScrobbleEvent()
            {
                SeriesId = series.Id,
                LibraryId = series.LibraryId,
                ScrobbleEventType = ScrobbleEventType.ChapterRead,
                AniListId = ExtractAniListId(series.Metadata.WebLinks),
                AppUserId = userId,
                VolumeNumber =
                    await _unitOfWork.AppUserProgressRepository.GetHighestFullyReadVolumeForSeries(seriesId, userId),
                ChapterNumber =
                    await _unitOfWork.AppUserProgressRepository.GetHighestFullyReadChapterForSeries(seriesId, userId),
                Format = MediaFormat.Manga,
            };
            _unitOfWork.ScrobbleEventRepository.Attach(evt);
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
        // TODO: License check
        var token = await GetTokenForProvider(userId, ScrobbleProvider.AniList);
        if (await HasTokenExpired(token, ScrobbleProvider.AniList))
        {
            throw new KavitaException("AniList Credentials have expired or not set");
        }

        var series = await _unitOfWork.SeriesRepository.GetSeriesByIdAsync(seriesId, SeriesIncludes.Metadata);
        if (series == null) throw new KavitaException("Series not found");

        var existing = await _unitOfWork.ScrobbleEventRepository.Exists(userId, series.Id,
            onWantToRead ? ScrobbleEventType.AddWantToRead : ScrobbleEventType.RemoveWantToRead);
        if (existing) return;

        var evt = new ScrobbleEvent()
        {
            SeriesId = series.Id,
            LibraryId = series.LibraryId,
            ScrobbleEventType = onWantToRead ? ScrobbleEventType.AddWantToRead : ScrobbleEventType.RemoveWantToRead,
            AniListId = ExtractAniListId(series.Metadata.WebLinks),
            AppUserId = userId,
            Format = MediaFormat.Manga,
        };
        _unitOfWork.ScrobbleEventRepository.Attach(evt);
        await _unitOfWork.CommitAsync();
    }

    private async Task PostScrobbleUpdate(ScrobbleDto data)
    {
        var serverSetting = await _unitOfWork.SettingsRepository.GetSettingsDtoAsync();
        try
        {
            var response = await (ApiUrl + "/api/scrobbling/anilist/update")
                .WithHeader("Accept", "application/json")
                .WithHeader("User-Agent", "Kavita")
                .WithHeader("x-license-key", serverSetting.LicenseKey)
                .WithHeader("x-installId", serverSetting.InstallId)
                .WithHeader("x-kavita-version", BuildInfo.Version)
                .WithHeader("Content-Type", "application/json")
                .WithTimeout(TimeSpan.FromSeconds(TimeOutSecs))
                .PostJsonAsync(data);

            if (response.StatusCode != StatusCodes.Status200OK)
            {
                _logger.LogError("KavitaPlus API did not respond successfully. {Content}", response);
            }
        }
        catch (Exception e)
        {
            _logger.LogError(e, "An error happened during the request to KavitaPlus API");
        }
    }

    /// <summary>
    /// This will back fill events from existing progress history, ratings, and want to read
    /// </summary>
    public async Task CreateEventsFromExistingHistory()
    {
        // TODO: We need a table to store when the last time this ran, so that we don't trigger twice and all of this is already done
        var lastSync = await _unitOfWork.SyncHistoryRepository.GetSyncTime(SyncKey.Scrobble);
        if (lastSync >= DateTime.UtcNow)
        {
            _logger.LogDebug("Nothing to sync");
        }
        foreach (var user in (await _unitOfWork.UserRepository.GetAllUsersAsync()))
        {
            if (!(await _licenseService.IsLicenseValid("TODO: License here"))) continue;

            var wantToRead = await _unitOfWork.SeriesRepository.GetWantToReadForUserAsync(user.Id);
            foreach (var wtr in wantToRead)
            {
                await ScrobbleWantToReadUpdate(user.Id, wtr.Id, true);
            }

            var ratings = await _unitOfWork.UserRepository.GetSeriesWithRatings(user.Id);
            foreach (var rating in ratings)
            {
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
                    }
                });

            foreach (var series in seriesWithProgress)
            {
                await ScrobbleReadingUpdate(user.Id, series.Id);
            }

        }

        // Update SyncHistory saying we've processed all rows
        await _unitOfWork.SyncHistoryRepository.Update(SyncKey.Scrobble);

    }

    public async Task ProcessUpdatesSinceLastSync()
    {
        // TODO: License check
        var readEvents = await _unitOfWork.ScrobbleEventRepository.GetByEvent(ScrobbleEventType.ChapterRead);
        var addToWantToRead = await _unitOfWork.ScrobbleEventRepository.GetByEvent(ScrobbleEventType.AddWantToRead);
        var removeWantToRead = await _unitOfWork.ScrobbleEventRepository.GetByEvent(ScrobbleEventType.RemoveWantToRead);
        var ratingEvents = await _unitOfWork.ScrobbleEventRepository.GetByEvent(ScrobbleEventType.ScoreUpdated);


        foreach (var readEvent in readEvents)
        {
            await PostScrobbleUpdate(new ScrobbleDto()
            {
                Format = readEvent.Format,
                AniListId = readEvent.AniListId,
                ScrobbleEventType = readEvent.ScrobbleEventType,
                ChapterNumber = readEvent.ChapterNumber,
                VolumeNumber = readEvent.VolumeNumber,
                AccessToken = readEvent.AppUser.AniListAccessToken,
                SeriesName = readEvent.Series.Name,
                LocalizedSeriesName = readEvent.Series.LocalizedName
            });
            _unitOfWork.ScrobbleEventRepository.Remove(readEvent);
        }

        foreach (var ratingEvent in ratingEvents)
        {
            await PostScrobbleUpdate(new ScrobbleDto()
            {
                Format = ratingEvent.Format,
                AniListId = ratingEvent.AniListId,
                ScrobbleEventType = ratingEvent.ScrobbleEventType,
                AccessToken = ratingEvent.AppUser.AniListAccessToken,
                SeriesName = ratingEvent.Series.Name,
                LocalizedSeriesName = ratingEvent.Series.LocalizedName,
                Rating = ratingEvent.Rating
            });
            _unitOfWork.ScrobbleEventRepository.Remove(ratingEvent);
        }

        var decisions = addToWantToRead
            .GroupBy(item => new { item.SeriesId, item.AppUserId })
            .Select(group => new
            {
                SeriesId = group.Key.SeriesId,
                UserId = group.Key.AppUserId,
                Event = group.First(),
                Decision = group.Count() - removeWantToRead
                    .Count(removeItem => removeItem.SeriesId == group.Key.SeriesId && removeItem.AppUserId == group.Key.AppUserId)
            })
            .Where(d => d.Decision > 0);

        foreach (var decision in decisions)
        {
            await PostScrobbleUpdate(new ScrobbleDto()
            {
                Format = decision.Event.Format,
                AniListId = decision.Event.AniListId,
                ScrobbleEventType = decision.Event.ScrobbleEventType,
                ChapterNumber = decision.Event.ChapterNumber,
                VolumeNumber = decision.Event.VolumeNumber,
                AccessToken = decision.Event.AppUser.AniListAccessToken,
                SeriesName = decision.Event.Series.Name,
                LocalizedSeriesName = decision.Event.Series.LocalizedName
            });
            _unitOfWork.ScrobbleEventRepository.Remove(decision.Event);
        }

        await _unitOfWork.CommitAsync();
    }

    private static int ExtractAniListId(string webLinks)
    {
        foreach (var webLink in webLinks.Split(","))
        {
            if (!webLink.StartsWith("https://anilist.co/")) continue;
            var tokens = webLink.Split("https://anilist.co/")[1].Split("/");
            return int.Parse(tokens[1]);
        }

        return 0;
    }
}
