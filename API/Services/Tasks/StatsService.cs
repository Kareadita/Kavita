using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
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
using API.Extensions;
using API.Services.Plus;
using API.Services.Tasks.Scanner.Parser;
using Flurl.Http;
using Kavita.Common;
using Kavita.Common.EnvironmentInfo;
using Kavita.Common.Helpers;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace API.Services.Tasks;

#nullable enable

public interface IStatsService
{
    Task Send();
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
    private readonly ILicenseService _licenseService;
    private readonly UserManager<AppUser> _userManager;
    private readonly IEmailService _emailService;
    private readonly ICacheService _cacheService;
    private readonly string _apiUrl = "";
    private const string ApiKey = "MsnvA2DfQqxSK5jh"; // It's not important this is public, just a way to keep bots from hitting the API willy-nilly

    public StatsService(ILogger<StatsService> logger, IUnitOfWork unitOfWork, DataContext context,
        ILicenseService licenseService, UserManager<AppUser> userManager, IEmailService emailService,
        ICacheService cacheService, IHostEnvironment environment)
    {
        _logger = logger;
        _unitOfWork = unitOfWork;
        _context = context;
        _licenseService = licenseService;
        _userManager = userManager;
        _emailService = emailService;
        _cacheService = cacheService;

        FlurlConfiguration.ConfigureClientForUrl(Configuration.StatsApiUrl);

        _apiUrl = environment.IsDevelopment() ? "http://localhost:5001" : Configuration.StatsApiUrl;
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
        var sw = Stopwatch.StartNew();
        var data = await GetStatV3Payload();
        _logger.LogDebug("Collecting stats took {Time} ms", sw.ElapsedMilliseconds);
        sw.Stop();
        await SendDataToStatsServer(data);
    }


    private async Task SendDataToStatsServer(ServerInfoV3Dto data)
    {
        var responseContent = string.Empty;

        try
        {
            var response = await (_apiUrl + "/api/v3/stats")
                .WithBasicHeaders(ApiKey)
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
            var response = await (_apiUrl + "/api/v2/stats/opt-out?installId=" + installId)
                .WithBasicHeaders(ApiKey)
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
            var response = await (Configuration.StatsApiUrl + "/api/health/")
                .WithBasicHeaders(ApiKey)
                .WithTimeout(TimeSpan.FromSeconds(30))
                .GetAsync();

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
                .Where(v => v.MinNumber == Parser.LooseLeafVolumeNumber)
                .SelectMany(v => v.Chapters!)
                .Count());
    }

    private async Task<ServerInfoV3Dto> GetStatV3Payload()
    {
        var serverSettings = await _unitOfWork.SettingsRepository.GetSettingsDtoAsync();
        var mediaSettings = await _unitOfWork.SettingsRepository.GetMetadataSettings();
        var dto = new ServerInfoV3Dto()
        {
            InstallId = serverSettings.InstallId,
            KavitaVersion = serverSettings.InstallVersion,
            InitialKavitaVersion = serverSettings.FirstInstallVersion,
            InitialInstallDate = (DateTime)serverSettings.FirstInstallDate!,
            IsDocker = OsInfo.IsDocker,
            Os = RuntimeInformation.OSDescription,
            NumOfCores = Math.Max(Environment.ProcessorCount, 1),
            DotnetVersion = Environment.Version.ToString(),
            OpdsEnabled = serverSettings.EnableOpds,
            EncodeMediaAs = serverSettings.EncodeMediaAs,
            MatchedMetadataEnabled = mediaSettings.Enabled
        };

        dto.OsLocale = CultureInfo.CurrentCulture.EnglishName;
        dto.LastReadTime = await _unitOfWork.AppUserProgressRepository.GetLatestProgress();
        dto.MaxSeriesInALibrary = await MaxSeriesInAnyLibrary();
        dto.MaxVolumesInASeries = await MaxVolumesInASeries();
        dto.MaxChaptersInASeries = await MaxChaptersInASeries();
        dto.TotalFiles = await _context.MangaFile.CountAsync();
        dto.TotalGenres = await _context.Genre.CountAsync();
        dto.TotalPeople = await _context.Person.CountAsync();
        dto.TotalSeries = await _context.Series.CountAsync();
        dto.TotalLibraries = await _context.Library.CountAsync();
        dto.NumberOfCollections =  await _context.AppUserCollection.CountAsync();
        dto.NumberOfReadingLists = await _context.ReadingList.CountAsync();

        try
        {
            var license = (await _unitOfWork.SettingsRepository.GetSettingAsync(ServerSettingKey.LicenseKey)).Value;
            dto.ActiveKavitaPlusSubscription = await _licenseService.HasActiveSubscription(license);
        }
        catch (Exception)
        {
            dto.ActiveKavitaPlusSubscription = false;
        }


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
            LibraryIncludes.FileTypes | LibraryIncludes.ExcludePatterns | LibraryIncludes.AppUser)).ToList();
        dto.Libraries ??= [];
        foreach (var library in allLibraries)
        {
            var libDto = new LibraryStatV3();
            libDto.IncludeInDashboard = library.IncludeInDashboard;
            libDto.IncludeInSearch = library.IncludeInSearch;
            libDto.LastScanned = library.LastScanned;
            libDto.NumberOfFolders = library.Folders.Count;
            libDto.FileTypes = library.LibraryFileTypes.Select(s => s.FileTypeGroup).Distinct().ToList();
            libDto.UsingExcludePatterns = library.LibraryExcludePatterns.Any(p => !string.IsNullOrEmpty(p.Pattern));
            libDto.UsingFolderWatching = library.FolderWatching;
            libDto.CreateCollectionsFromMetadata = library.ManageCollections;
            libDto.CreateReadingListsFromMetadata = library.ManageReadingLists;
            libDto.LibraryType = library.Type;

            dto.Libraries.Add(libDto);
        }
        #endregion

        #region Users

        // Create a dictionary mapping user IDs to the libraries they have access to
        var userLibraryAccess = allLibraries
            .SelectMany(l => l.AppUsers.Select(appUser => new { l, appUser.Id }))
            .GroupBy(x => x.Id)
            .ToDictionary(g => g.Key, g => g.Select(x => x.l).ToList());
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
            userDto.LastReadTime = user.Progresses
                .Select(p => p.LastModifiedUtc)
                .DefaultIfEmpty()
                .Max();
            userDto.DevicePlatforms = user.Devices.Select(d => d.Platform).ToList();
            userDto.SeriesBookmarksCreatedCount = user.Bookmarks.Count;
            userDto.SmartFilterCreatedCount = user.SmartFilters.Count;
            userDto.WantToReadSeriesCount = user.WantToRead.Count;

            if (allLibraries.Count > 0 && userLibraryAccess.TryGetValue(user.Id, out var accessibleLibraries))
            {
                userDto.PercentageOfLibrariesHasAccess = (1f * accessibleLibraries.Count) / allLibraries.Count;
            }
            else
            {
                userDto.PercentageOfLibrariesHasAccess = 0;
            }

            dto.Users.Add(userDto);
        }

        #endregion

        return dto;
    }

    private async Task OpenRandomFile(ServerInfoV3Dto dto)
    {
        var random = new Random();
        List<string> extensions = [".cbz", ".zip"];

        // Count the total number of files that match the criteria
        var count = await _context.MangaFile.AsNoTracking()
            .Where(r => r.Extension != null && extensions.Contains(r.Extension))
            .CountAsync();

        if (count == 0)
        {
            dto.TimeToOpeCbzMs = 0;
            dto.TimeToOpenCbzPages = 0;

            return;
        }

        // Generate a random skip value
        var skip = random.Next(count);

        // Fetch the random file
        var randomFile = await _context.MangaFile.AsNoTracking()
            .Where(r => r.Extension != null && extensions.Contains(r.Extension))
            .Skip(skip)
            .Take(1)
            .FirstAsync();

        var sw = Stopwatch.StartNew();

        await _cacheService.Ensure(randomFile.ChapterId);
        var time = sw.ElapsedMilliseconds;
        sw.Stop();

        dto.TimeToOpeCbzMs = time;
        dto.TimeToOpenCbzPages = randomFile.Pages;
    }
}
