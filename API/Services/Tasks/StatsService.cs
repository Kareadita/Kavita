﻿using System;
using System.Linq;
using System.Net.Http;
using System.Runtime.InteropServices;
using System.Threading.Tasks;
using API.Data;
using API.Data.Repositories;
using API.DTOs.Stats;
using API.Entities.Enums;
using API.Entities.Enums.UserPreferences;
using Flurl.Http;
using Kavita.Common.EnvironmentInfo;
using Kavita.Common.Helpers;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace API.Services.Tasks;

public interface IStatsService
{
    Task Send();
    Task<ServerInfoDto> GetServerInfo();
}
public class StatsService : IStatsService
{
    private readonly ILogger<StatsService> _logger;
    private readonly IUnitOfWork _unitOfWork;
    private readonly DataContext _context;
    private const string ApiUrl = "https://stats.kavitareader.com";

    public StatsService(ILogger<StatsService> logger, IUnitOfWork unitOfWork, DataContext context)
    {
        _logger = logger;
        _unitOfWork = unitOfWork;
        _context = context;

        FlurlHttp.ConfigureClient(ApiUrl, cli =>
            cli.Settings.HttpClientFactory = new UntrustedCertClientFactory());
    }

    /// <summary>
    /// Due to all instances firing this at the same time, we can DDOS our server. This task when fired will schedule the task to be run
    /// randomly over a 6 hour spread
    /// </summary>
    public async Task Send()
    {
        var allowStatCollection = (await _unitOfWork.SettingsRepository.GetSettingsDtoAsync()).AllowStatCollection;
        if (!allowStatCollection)
        {
            return;
        }

        await SendData();
    }

    /// <summary>
    /// This must be public for Hangfire. Do not call this directly.
    /// </summary>
    // ReSharper disable once MemberCanBePrivate.Global
    public async Task SendData()
    {
        var data = await GetServerInfo();
        await SendDataToStatsServer(data);
    }


    private async Task SendDataToStatsServer(ServerInfoDto data)
    {
        var responseContent = string.Empty;

        try
        {
            var response = await (ApiUrl + "/api/v2/stats")
                .WithHeader("Accept", "application/json")
                .WithHeader("User-Agent", "Kavita")
                .WithHeader("x-api-key", "MsnvA2DfQqxSK5jh")
                .WithHeader("x-kavita-version", BuildInfo.Version)
                .WithHeader("Content-Type", "application/json")
                .WithTimeout(TimeSpan.FromSeconds(30))
                .PostJsonAsync(data);

            if (response.StatusCode != StatusCodes.Status200OK)
            {
                _logger.LogError("KavitaStats did not respond successfully. {Content}", response);
            }
        }
        catch (HttpRequestException e)
        {
            var info = new
            {
                dataSent = data,
                response = responseContent
            };

            _logger.LogError(e, "KavitaStats did not respond successfully. {Content}", info);
        }
        catch (Exception e)
        {
            _logger.LogError(e, "An error happened during the request to KavitaStats");
        }
    }

    public async Task<ServerInfoDto> GetServerInfo()
    {
        var installId = await _unitOfWork.SettingsRepository.GetSettingAsync(ServerSettingKey.InstallId);
        var installVersion = await _unitOfWork.SettingsRepository.GetSettingAsync(ServerSettingKey.InstallVersion);

        var serverSettings = await _unitOfWork.SettingsRepository.GetSettingsDtoAsync();

        var serverInfo = new ServerInfoDto
        {
            InstallId = installId.Value,
            Os = RuntimeInformation.OSDescription,
            KavitaVersion = installVersion.Value,
            DotnetVersion = Environment.Version.ToString(),
            IsDocker = new OsInfo(Array.Empty<IOsVersionAdapter>()).IsDocker,
            NumOfCores = Math.Max(Environment.ProcessorCount, 1),
            HasBookmarks = (await _unitOfWork.UserRepository.GetAllBookmarksAsync()).Any(),
            NumberOfLibraries = (await _unitOfWork.LibraryRepository.GetLibrariesAsync()).Count(),
            NumberOfCollections = (await _unitOfWork.CollectionTagRepository.GetAllTagsAsync()).Count(),
            NumberOfReadingLists = await _unitOfWork.ReadingListRepository.Count(),
            OPDSEnabled = serverSettings.EnableOpds,
            NumberOfUsers = (await _unitOfWork.UserRepository.GetAllUsers()).Count(),
            TotalFiles = await _unitOfWork.LibraryRepository.GetTotalFiles(),
            TotalGenres = await _unitOfWork.GenreRepository.GetCountAsync(),
            TotalPeople = await _unitOfWork.PersonRepository.GetCountAsync(),
            UsingSeriesRelationships = await GetIfUsingSeriesRelationship(),
            StoreBookmarksAsWebP = serverSettings.ConvertBookmarkToWebP,
            MaxSeriesInALibrary = await MaxSeriesInAnyLibrary(),
            MaxVolumesInASeries = await MaxVolumesInASeries(),
            MaxChaptersInASeries = await MaxChaptersInASeries(),
        };

        var usersWithPref = (await _unitOfWork.UserRepository.GetAllUsersAsync(AppUserIncludes.UserPreferences)).ToList();
        serverInfo.UsersOnCardLayout =
            usersWithPref.Count(u => u.UserPreferences.GlobalPageLayoutMode == PageLayoutMode.Cards);
        serverInfo.UsersOnListLayout =
            usersWithPref.Count(u => u.UserPreferences.GlobalPageLayoutMode == PageLayoutMode.List);

        var firstAdminUser = (await _unitOfWork.UserRepository.GetAdminUsersAsync()).FirstOrDefault();

        if (firstAdminUser != null)
        {
            var firstAdminUserPref = (await _unitOfWork.UserRepository.GetPreferencesAsync(firstAdminUser.UserName));
            var activeTheme = firstAdminUserPref.Theme ?? Seed.DefaultThemes.First(t => t.IsDefault);

            serverInfo.ActiveSiteTheme = activeTheme.Name;
            serverInfo.MangaReaderMode = firstAdminUserPref.ReaderMode;
        }

        return serverInfo;
    }

    private Task<bool> GetIfUsingSeriesRelationship()
    {
        return _context.SeriesRelation.AnyAsync();
    }

    private Task<int> MaxSeriesInAnyLibrary()
    {
        return _context.Series
            .Select(s => new
            {
                LibraryId = s.LibraryId,
                Count = _context.Library.Where(l => l.Id == s.LibraryId).SelectMany(l => l.Series).Count()
            })
            .MaxAsync(d => d.Count);
    }

    private Task<int> MaxVolumesInASeries()
    {
        return _context.Volume
            .Select(v => new
            {
                v.SeriesId,
                Count = _context.Series.Where(s => s.Id == v.SeriesId).SelectMany(s => s.Volumes).Count()
            })
            .MaxAsync(d => d.Count);
    }

    private Task<int> MaxChaptersInASeries()
    {
        return _context.Series
            .MaxAsync(s => s.Volumes
                .Where(v => v.Number == 0)
                .SelectMany(v => v.Chapters)
                .Count());
    }
}
