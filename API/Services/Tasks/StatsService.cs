using System;
using System.Linq;
using System.Net.Http;
using System.Runtime.InteropServices;
using System.Threading.Tasks;
using API.Data;
using API.DTOs.Stats;
using API.DTOs.Theme;
using API.Entities.Enums;
using Flurl.Http;
using Kavita.Common.EnvironmentInfo;
using Kavita.Common.Helpers;
using Microsoft.AspNetCore.Http;
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
    private const string ApiUrl = "https://stats.kavitareader.com";

    public StatsService(ILogger<StatsService> logger, IUnitOfWork unitOfWork)
    {
        _logger = logger;
        _unitOfWork = unitOfWork;

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

        var firstAdminUser = (await _unitOfWork.UserRepository.GetAdminUsersAsync()).First();
        var firstAdminUserPref = (await _unitOfWork.UserRepository.GetPreferencesAsync(firstAdminUser.UserName));

        var activeTheme = firstAdminUserPref.Theme ?? Seed.DefaultThemes.First(t => t.IsDefault);

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
            ActiveSiteTheme = activeTheme.Name,
            NumberOfCollections = (await _unitOfWork.CollectionTagRepository.GetAllTagsAsync()).Count(),
            NumberOfReadingLists = await _unitOfWork.ReadingListRepository.Count(),
            OPDSEnabled = (await _unitOfWork.SettingsRepository.GetSettingsDtoAsync()).EnableOpds,
            NumberOfUsers = (await _unitOfWork.UserRepository.GetAllUsers()).Count(),
            TotalFiles = await _unitOfWork.LibraryRepository.GetTotalFiles(),
            MangaReaderMode = firstAdminUserPref.ReaderMode
        };

        return serverInfo;
    }
}
