using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Runtime.InteropServices;
using System.Threading.Tasks;
using API.Data;
using API.Data.Misc;
using API.Data.Repositories;
using API.DTOs.Stats;
using API.DTOs.Stats.V3;
using API.Entities;
using API.Entities.Enums;
using API.Entities.Enums.UserPreferences;
using API.Services.Plus;
using Flurl.Http;
using Kavita.Common.EnvironmentInfo;
using Kavita.Common.Helpers;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace API.Services.Tasks;

#nullable enable

public interface IStatsService
{
    Task Send();
    Task<ServerInfoDto> GetServerInfo();
    Task<ServerInfoSlimDto> GetServerInfoSlim();
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
    private readonly ILicenseService _licenseService;
    private readonly UserManager<AppUser> _userManager;
    private readonly IEmailService _emailService;
    private readonly ICacheService _cacheService;
    private const string ApiUrl = "https://stats.kavitareader.com";

    public StatsService(ILogger<StatsService> logger, IUnitOfWork unitOfWork, DataContext context, IStatisticService statisticService,
        ILicenseService licenseService, UserManager<AppUser> userManager, IEmailService emailService, ICacheService cacheService)
    {
        _logger = logger;
        _unitOfWork = unitOfWork;
        _context = context;
        _statisticService = statisticService;
        _licenseService = licenseService;
        _userManager = userManager;
        _emailService = emailService;
        _cacheService = cacheService;

        FlurlHttp.ConfigureClient(ApiUrl, cli =>
            cli.Settings.HttpClientFactory = new UntrustedCertClientFactory());
    }

    /// <summary>
    /// Due to all instances firing this at the same time, we can DDOS our server. This task when fired will schedule the task to be run
    /// randomly over a six-hour spread
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
        var data = await GetStatV3Payload();
        await SendDataToStatsServer(data);
    }


    private async Task SendDataToStatsServer(ServerInfoV3Dto data)
    {
        var responseContent = string.Empty;

        try
        {
            var response = await (ApiUrl + "/api/v3/stats")
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
            IsDocker = OsInfo.IsDocker,
            NumOfCores = Math.Max(Environment.ProcessorCount, 1),
            UsersWithEmulateComicBook = await _context.AppUserPreferences.CountAsync(p => p.EmulateBook),
            TotalReadingHours = await _statisticService.TimeSpentReadingForUsersAsync(ArraySegment<int>.Empty, ArraySegment<int>.Empty),

            PercentOfLibrariesWithFolderWatchingEnabled = await GetPercentageOfLibrariesWithFolderWatchingEnabled(),
            PercentOfLibrariesIncludedInRecommended = await GetPercentageOfLibrariesIncludedInRecommended(),
            PercentOfLibrariesIncludedInDashboard = await GetPercentageOfLibrariesIncludedInDashboard(),
            PercentOfLibrariesIncludedInSearch = await GetPercentageOfLibrariesIncludedInSearch(),

            HasBookmarks = (await _unitOfWork.UserRepository.GetAllBookmarksAsync()).Any(),
            NumberOfLibraries = (await _unitOfWork.LibraryRepository.GetLibrariesAsync()).Count(),
            NumberOfCollections = (await _unitOfWork.CollectionTagRepository.GetAllCollectionsAsync()).Count(),
            NumberOfReadingLists = await _unitOfWork.ReadingListRepository.Count(),
            OPDSEnabled = serverSettings.EnableOpds,
            NumberOfUsers = (await _unitOfWork.UserRepository.GetAllUsersAsync()).Count(),
            TotalFiles = await _unitOfWork.LibraryRepository.GetTotalFiles(),
            TotalGenres = await _unitOfWork.GenreRepository.GetCountAsync(),
            TotalPeople = await _unitOfWork.PersonRepository.GetCountAsync(),
            UsingSeriesRelationships = await GetIfUsingSeriesRelationship(),
            EncodeMediaAs = serverSettings.EncodeMediaAs,
            MaxSeriesInALibrary = await MaxSeriesInAnyLibrary(),
            MaxVolumesInASeries = await MaxVolumesInASeries(),
            MaxChaptersInASeries = await MaxChaptersInASeries(),
            MangaReaderBackgroundColors = await AllMangaReaderBackgroundColors(),
            MangaReaderPageSplittingModes = await AllMangaReaderPageSplitting(),
            MangaReaderLayoutModes = await AllMangaReaderLayoutModes(),
            FileFormats = AllFormats(),
            UsingRestrictedProfiles = await GetUsingRestrictedProfiles(),
            LastReadTime = await _unitOfWork.AppUserProgressRepository.GetLatestProgress()
        };

        var usersWithPref = (await _unitOfWork.UserRepository.GetAllUsersAsync(AppUserIncludes.UserPreferences)).ToList();
        serverInfo.UsersOnCardLayout =
            usersWithPref.Count(u => u.UserPreferences.GlobalPageLayoutMode == PageLayoutMode.Cards);
        serverInfo.UsersOnListLayout =
            usersWithPref.Count(u => u.UserPreferences.GlobalPageLayoutMode == PageLayoutMode.List);

        var firstAdminUser = (await _unitOfWork.UserRepository.GetAdminUsersAsync()).FirstOrDefault();

        if (firstAdminUser != null)
        {
            var firstAdminUserPref = (await _unitOfWork.UserRepository.GetPreferencesAsync(firstAdminUser.UserName!));
            var activeTheme = firstAdminUserPref?.Theme ?? Seed.DefaultThemes.First(t => t.IsDefault);

            serverInfo.ActiveSiteTheme = activeTheme.Name;
            if (firstAdminUserPref != null) serverInfo.MangaReaderMode = firstAdminUserPref.ReaderMode;
        }

        return serverInfo;
    }

    public async Task<ServerInfoSlimDto> GetServerInfoSlim()
    {
        var serverSettings = await _unitOfWork.SettingsRepository.GetSettingsDtoAsync();
        return new ServerInfoSlimDto()
        {
            InstallId = serverSettings.InstallId,
            KavitaVersion = serverSettings.InstallVersion,
            IsDocker = OsInfo.IsDocker,
            FirstInstallDate = serverSettings.FirstInstallDate,
            FirstInstallVersion = serverSettings.FirstInstallVersion
        };
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

    private static async Task<long> PingStatsApi()
    {
        try
        {
            var sw = Stopwatch.StartNew();
            var response = await (ApiUrl + "/api/health/")
                .WithHeader("Accept", "application/json")
                .WithHeader("User-Agent", "Kavita")
                .WithHeader("x-api-key", "MsnvA2DfQqxSK5jh")
                .WithHeader("x-kavita-version", BuildInfo.Version)
                .WithHeader("Content-Type", "application/json")
                .WithTimeout(TimeSpan.FromSeconds(30))
                .PostAsync();

            if (response.StatusCode == StatusCodes.Status200OK)
            {
                sw.Stop();
                return sw.ElapsedMilliseconds;
            }
        }
        catch (Exception)
        {
            /* Swallow */
        }

        return 0;
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
            .Select(s => _context.Library.Where(l => l.Id == s.LibraryId).SelectMany(l => l.Series!).Count())
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
                Count = _context.Series.Where(s => s.Id == v.SeriesId).SelectMany(s => s.Volumes!).Count()
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
            .MaxAsync(s => s.Volumes!
                .Where(v => v.MinNumber == 0)
                .SelectMany(v => v.Chapters!)
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
                Extension = m.Extension
            })
            .DistinctBy(f => f.Extension)
            .ToList();

        return results;
    }

    private Task<bool> GetUsingRestrictedProfiles()
    {
        return _context.Users.AnyAsync(u => u.AgeRestriction > AgeRating.NotApplicable);
    }

    private async Task<ServerInfoV3Dto> GetStatV3Payload()
    {
        var serverSettings = await _unitOfWork.SettingsRepository.GetSettingsDtoAsync();
        var dto = new ServerInfoV3Dto()
        {
            InstallId = serverSettings.InstallId,
            KavitaVersion = serverSettings.InstallVersion,
            InitialKavitaVersion = serverSettings.FirstInstallVersion,
            InitialInstallDate = (DateTime) serverSettings.FirstInstallDate!,
            IsDocker = OsInfo.IsDocker,
            Os = RuntimeInformation.OSDescription,
            NumOfCores = Math.Max(Environment.ProcessorCount, 1),
            DotnetVersion = Environment.Version.ToString(),
            OpdsEnabled = serverSettings.EnableOpds,
            EncodeMediaAs = serverSettings.EncodeMediaAs,
        };

        dto.OsLocale = CultureInfo.CurrentCulture.DisplayName;
        var license = (await _unitOfWork.SettingsRepository.GetSettingAsync(ServerSettingKey.LicenseKey)).Value;
        dto.ActiveKavitaPlusSubscription = await _licenseService.HasActiveSubscription(license);
        dto.LastReadTime = await _unitOfWork.AppUserProgressRepository.GetLatestProgress();
        dto.MaxSeriesInALibrary = await MaxSeriesInAnyLibrary();
        dto.MaxVolumesInASeries = await MaxVolumesInASeries();
        dto.MaxChaptersInASeries = await MaxChaptersInASeries();
        dto.TotalFiles = await _unitOfWork.LibraryRepository.GetTotalFiles();
        dto.TotalGenres = await _unitOfWork.GenreRepository.GetCountAsync();
        dto.TotalPeople = await _unitOfWork.PersonRepository.GetCountAsync();
        dto.TotalLibraries = (await _unitOfWork.LibraryRepository.GetLibrariesAsync()).Count();
        dto.NumberOfCollections = (await _unitOfWork.CollectionTagRepository.GetAllCollectionsAsync()).Count();
        dto.NumberOfReadingLists = await _unitOfWork.ReadingListRepository.Count();

        // Find a random cbz/zip file and open it for reading
        await OpenRandomFile(dto);
        dto.TimeToPingKavitaStatsApi = await PingStatsApi();

        #region Relationships

        dto.Relationships = await _context.SeriesRelation
            .GroupBy(sr => sr.RelationKind)
            .Select(g => new RelationshipStatV3
            {
                Relationship = g.Key,
                Count = g.Count()
            })
            .ToListAsync();

        #endregion

        #region Libraries
        var allLibraries = (await _unitOfWork.LibraryRepository.GetLibrariesAsync(LibraryIncludes.Folders |
            LibraryIncludes.FileTypes | LibraryIncludes.ExcludePatterns)).ToList();
        dto.Libraries ??= [];
        foreach (var library in allLibraries)
        {
            var libDto = new LibraryStatV3();
            libDto.IncludeInDashboard = library.IncludeInDashboard;
            libDto.IncludeInSearch = library.IncludeInSearch;
            libDto.LastScanned = library.LastScanned;
            libDto.NumberOfFolders = library.Folders.Count;
            libDto.FileTypes = library.LibraryFileTypes.Select(s => s.FileTypeGroup).Distinct().ToList();
            libDto.UsingExcludePatterns = library.LibraryExcludePatterns.Count > 0;
            libDto.UsingFolderWatching = library.FolderWatching;
            libDto.CreateCollectionsFromMetadata = library.ManageCollections;
            libDto.CreateReadingListsFromMetadata = library.ManageReadingLists;

            dto.Libraries.Add(libDto);
        }
        #endregion

        #region Users

        dto.Users ??= [];
        var allUsers = await _unitOfWork.UserRepository.GetAllUsersAsync(AppUserIncludes.UserPreferences
                                                                         | AppUserIncludes.ReadingLists | AppUserIncludes.Bookmarks
                                                                         | AppUserIncludes.Collections | AppUserIncludes.Devices
                                                                         | AppUserIncludes.Progress | AppUserIncludes.Ratings
                                                                         | AppUserIncludes.SmartFilters | AppUserIncludes.WantToRead, false);
        foreach (var user in allUsers)
        {
            var userDto = new UserStatV3();
            userDto.HasMALToken = !string.IsNullOrEmpty(user.MalAccessToken);
            userDto.HasAniListToken = !string.IsNullOrEmpty(user.AniListAccessToken);
            userDto.AgeRestriction = new AgeRestriction()
            {
                AgeRating = user.AgeRestriction,
                IncludeUnknowns = user.AgeRestrictionIncludeUnknowns
            };

            userDto.Locale = user.UserPreferences.Locale;
            userDto.Roles = [.. _userManager.GetRolesAsync(user).Result];
            userDto.LastLogin = user.LastActiveUtc;
            userDto.HasValidEmail = user.Email != null && _emailService.IsValidEmail(user.Email);
            userDto.IsEmailConfirmed = user.EmailConfirmed;
            userDto.ActiveTheme = user.UserPreferences.Theme.Name;
            userDto.CollectionsCreatedCount = user.Collections.Count;
            userDto.ReadingListsCreatedCount = user.ReadingLists.Count;
            userDto.PercentageOfLibrariesHasAccess = allLibraries.Count > 0
                ? ((1f * user.Libraries.Count) / allLibraries.Count)
                : 0;
            userDto.LastReadTime = user.Progresses
                .Select(p => p.LastModifiedUtc)
                .Max();
            userDto.DevicePlatforms = user.Devices.Select(d => d.Platform).ToList();
            userDto.SeriesBookmarksCreatedCount = user.Bookmarks.Count;
            userDto.SmartFilterCreatedCount = user.SmartFilters.Count;
            userDto.WantToReadSeriesCount = user.WantToRead.Count;

            dto.Users.Add(userDto);
        }

        #endregion

        return dto;
    }

    private async Task OpenRandomFile(ServerInfoV3Dto dto)
    {
        var random = new Random();
        List<string> extensions = [".cbz", ".zip"];
        var randomFile = await _context.MangaFile.AsNoTracking().Where(r => extensions.Contains(r.Extension))
            .OrderBy(r => random.Next())
            .FirstAsync();

        var sw = Stopwatch.StartNew();

        await _cacheService.Ensure(randomFile.ChapterId);
        var time = sw.ElapsedMilliseconds;
        sw.Stop();

        dto.TimeToOpeCbzMs = time;
        dto.TimeToOpenCbzPages = randomFile.Pages;
    }
}
