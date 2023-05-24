using System;
using System.IdentityModel.Tokens.Jwt;
using System.Net.Http;
using System.Threading.Tasks;
using API.Data;
using API.Data.Repositories;
using API.DTOs.Scrobbling;
using API.SignalR;
using Flurl.Http;
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
}

public class ScrobblingService : IScrobblingService
{
    private readonly IUnitOfWork _unitOfWork;
    private readonly ITokenService _tokenService;
    private readonly IEventHub _eventHub;
    private readonly ILogger<ScrobblingService> _logger;

    private const string ApiUrl = "http://localhost:5020";

    public ScrobblingService(IUnitOfWork unitOfWork, ITokenService tokenService, IEventHub eventHub, ILogger<ScrobblingService> logger)
    {
        _unitOfWork = unitOfWork;
        _tokenService = tokenService;
        _eventHub = eventHub;
        _logger = logger;

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

        if (HasTokenExpired(token)) return false;

        // NOTE: Should this side effect be here?
        await _eventHub.SendMessageToAsync(MessageFactory.ScrobblingKeyExpired,
            MessageFactory.ScrobblingKeyExpiredEvent(ScrobbleProvider.AniList), userId);
        return true;
    }

    private bool HasTokenExpired(string token)
    {
        if (string.IsNullOrEmpty(token) ||
            !_tokenService.HasTokenExpired(token)) return false;

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
        var token = await GetTokenForProvider(userId, ScrobbleProvider.AniList);
        if (string.IsNullOrEmpty(token) || HasTokenExpired(token))
        {
            throw new KavitaException("AniList Credentials have expired or not set");
        }

        var series = await _unitOfWork.SeriesRepository.GetSeriesByIdAsync(seriesId, SeriesIncludes.Metadata);
        if (series == null) throw new KavitaException("Series not found");

        var data = new ScrobbleDto()
        {
            SeriesName = series.Name,
            Rating = rating,
            ScrobbleEvent = ScrobbleEvent.ScoreUpdated,
            AccessToken = token,
            AniListId = ExtractAniListId(series.Metadata.WebLinks)
        };

        try
        {
            var response = await (ApiUrl + "/api/scrobbling/anilist/update")
                .WithHeader("Accept", "application/json")
                .WithHeader("User-Agent", "Kavita")
                .WithHeader("x-license-key", "TODO")
                .WithHeader("x-kavita-version", BuildInfo.Version)
                .WithHeader("Content-Type", "application/json")
                .WithTimeout(TimeSpan.FromSeconds(30))
                .PostJsonAsync(data);

            if (response.StatusCode != StatusCodes.Status200OK)
            {
                _logger.LogError("KavitaPlus API did not respond successfully. {Content}", response);
            }
        }
        catch (HttpRequestException e)
        {
            _logger.LogError(e, "An error happened during the request to KavitaPlus API");
        }
        catch (Exception e)
        {
            _logger.LogError(e, "An error happened during the request to KavitaPlus API");
        }
        throw new NotImplementedException();
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
