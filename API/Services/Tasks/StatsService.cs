using System;
using System.Collections.Generic;
using System.IO;
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
    Task SendCancellation();
}
/// <summary>
/// This is for reporting to the stat server
/// </summary>
public class StatsService : IStatsService
{
    private readonly ILogger<StatsService> _logger;
    private readonly IUnitOfWork _unitOfWork;
    private readonly DataContext _context;
    private readonly IStatisticService _statisticService;
    private const string ApiUrl = "https://stats.kavitareader.com";

    public StatsService(ILogger<StatsService> logger, IUnitOfWork unitOfWork, DataContext context, IStatisticService statisticService)
    {
        _logger = logger;
        _unitOfWork = unitOfWork;
        _context = context;
        _statisticService = statisticService;

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
        var serverSettings = await _unitOfWork.SettingsRepository.GetSettingsDtoAsync();

        var serverInfo = new ServerInfoDto
        {
            InstallId = serverSettings.InstallId,
            Os = RuntimeInformation.OSDescription,
            KavitaVersion = serverSettings.InstallVersion,
            DotnetVersion = Environment.Version.ToString(),
            IsDocker = new OsInfo().IsDocker,
            NumOfCores = Math.Max(Environment.ProcessorCount, 1),
            UsersWithEmulateComicBook = await _context.AppUserPreferences.CountAsync(p => p.EmulateBook),
            TotalReadingHours = await _statisticService.TimeSpentReadingForUsersAsync(ArraySegment<int>.Empty, ArraySegment<int>.Empty),

            PercentOfLibrariesWithFolderWatchingEnabled = await GetPercentageOfLibrariesWithFolderWatchingEnabled(),
            PercentOfLibrariesIncludedInRecommended = await GetPercentageOfLibrariesIncludedInRecommended(),
            PercentOfLibrariesIncludedInDashboard = await GetPercentageOfLibrariesIncludedInDashboard(),
            PercentOfLibrariesIncludedInSearch = await GetPercentageOfLibrariesIncludedInSearch(),

            HasBookmarks = (await _unitOfWork.UserRepository.GetAllBookmarksAsync()).Any(),
            NumberOfLibraries = (await _unitOfWork.LibraryRepository.GetLibrariesAsync()).Count(),
            NumberOfCollections = (await _unitOfWork.CollectionTagRepository.GetAllTagsAsync()).Count(),
            NumberOfReadingLists = await _unitOfWork.ReadingListRepository.Count(),
            OPDSEnabled = serverSettings.EnableOpds,
            NumberOfUsers = (await _unitOfWork.UserRepository.GetAllUsersAsync()).Count(),
            TotalFiles = await _unitOfWork.LibraryRepository.GetTotalFiles(),
            TotalGenres = await _unitOfWork.GenreRepository.GetCountAsync(),
            TotalPeople = await _unitOfWork.PersonRepository.GetCountAsync(),
            UsingSeriesRelationships = await GetIfUsingSeriesRelationship(),
            StoreBookmarksAsWebP = serverSettings.ConvertBookmarkToWebP,
            StoreCoversAsWebP = serverSettings.ConvertCoverToWebP,
            MaxSeriesInALibrary = await MaxSeriesInAnyLibrary(),
            MaxVolumesInASeries = await MaxVolumesInASeries(),
            MaxChaptersInASeries = await MaxChaptersInASeries(),
            MangaReaderBackgroundColors = await AllMangaReaderBackgroundColors(),
            MangaReaderPageSplittingModes = await AllMangaReaderPageSplitting(),
            MangaReaderLayoutModes = await AllMangaReaderLayoutModes(),
            FileFormats = AllFormats(),
            UsingRestrictedProfiles = await GetUsingRestrictedProfiles(),
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

    public async Task SendCancellation()
    {
        _logger.LogInformation("Informing KavitaStats that this instance is no longer sending stats");
        var installId = (await _unitOfWork.SettingsRepository.GetSettingsDtoAsync()).InstallId;

        var responseContent = string.Empty;

        try
        {
            var response = await (ApiUrl + "/api/v2/stats/opt-out?installId=" + installId)
                .WithHeader("Accept", "application/json")
                .WithHeader("User-Agent", "Kavita")
                .WithHeader("x-api-key", "MsnvA2DfQqxSK5jh")
                .WithHeader("x-kavita-version", BuildInfo.Version)
                .WithHeader("Content-Type", "application/json")
                .WithTimeout(TimeSpan.FromSeconds(30))
                .PostAsync();

            if (response.StatusCode != StatusCodes.Status200OK)
            {
                _logger.LogError("KavitaStats did not respond successfully. {Content}", response);
            }
        }
        catch (HttpRequestException e)
        {
            _logger.LogError(e, "KavitaStats did not respond successfully. {Response}", responseContent);
        }
        catch (Exception e)
        {
            _logger.LogError(e, "An error happened during the request to KavitaStats");
        }
    }

    private async Task<float> GetPercentageOfLibrariesWithFolderWatchingEnabled()
    {
        var libraries = (await _unitOfWork.LibraryRepository.GetLibrariesAsync()).ToList();
        if (libraries.Count == 0) return 0.0f;
        return libraries.Count(l => l.FolderWatching) / (1.0f * libraries.Count);
    }

    private async Task<float> GetPercentageOfLibrariesIncludedInRecommended()
    {
        var libraries = (await _unitOfWork.LibraryRepository.GetLibrariesAsync()).ToList();
        if (libraries.Count == 0) return 0.0f;
        return libraries.Count(l => l.IncludeInRecommended) / (1.0f * libraries.Count);
    }

    private async Task<float> GetPercentageOfLibrariesIncludedInDashboard()
    {
        var libraries = (await _unitOfWork.LibraryRepository.GetLibrariesAsync()).ToList();
        if (libraries.Count == 0) return 0.0f;
        return libraries.Count(l => l.IncludeInDashboard) / (1.0f * libraries.Count);
    }

    private async Task<float> GetPercentageOfLibrariesIncludedInSearch()
    {
        var libraries = (await _unitOfWork.LibraryRepository.GetLibrariesAsync()).ToList();
        if (libraries.Count == 0) return 0.0f;
        return libraries.Count(l => l.IncludeInSearch) / (1.0f * libraries.Count);
    }

    private Task<bool> GetIfUsingSeriesRelationship()
    {
        return _context.SeriesRelation.AnyAsync();
    }

    private async Task<int> MaxSeriesInAnyLibrary()
    {
        // If first time flow, just return 0
        if (!await _context.Series.AnyAsync()) return 0;
        return await _context.Series
            .Select(s => _context.Library.Where(l => l.Id == s.LibraryId).SelectMany(l => l.Series).Count())
            .MaxAsync();
    }

    private async Task<int> MaxVolumesInASeries()
    {
        // If first time flow, just return 0
        if (!await _context.Volume.AnyAsync()) return 0;
        return await _context.Volume
            .Select(v => new
            {
                v.SeriesId,
                Count = _context.Series.Where(s => s.Id == v.SeriesId).SelectMany(s => s.Volumes).Count()
            })
            .AsNoTracking()
            .AsSplitQuery()
            .MaxAsync(d => d.Count);
    }

    private async Task<int> MaxChaptersInASeries()
    {
        // If first time flow, just return 0
        if (!await _context.Chapter.AnyAsync()) return 0;
        return await _context.Series
            .AsNoTracking()
            .AsSplitQuery()
            .MaxAsync(s => s.Volumes
                .Where(v => v.Number == 0)
                .SelectMany(v => v.Chapters)
                .Count());
    }

    private async Task<IEnumerable<string>> AllMangaReaderBackgroundColors()
    {
        return await _context.AppUserPreferences.Select(p => p.BackgroundColor).Distinct().ToListAsync();
    }

    private async Task<IEnumerable<PageSplitOption>> AllMangaReaderPageSplitting()
    {
        return await _context.AppUserPreferences.Select(p => p.PageSplitOption).Distinct().ToListAsync();
    }


    private async Task<IEnumerable<LayoutMode>> AllMangaReaderLayoutModes()
    {
        return await _context.AppUserPreferences.Select(p => p.LayoutMode).Distinct().ToListAsync();
    }

    private IEnumerable<FileFormatDto> AllFormats()
    {
        var results =  _context.MangaFile
            .AsNoTracking()
            .AsEnumerable()
            .Select(m => new FileFormatDto()
            {
                Format = m.Format,
                Extension = Path.GetExtension(m.FilePath)?.ToLowerInvariant()
            })
            .DistinctBy(f => f.Extension)
            .ToList();

        return results;
    }

    private Task<bool> GetUsingRestrictedProfiles()
    {
        return _context.Users.AnyAsync(u => u.AgeRestriction > AgeRating.NotApplicable);
    }
}
