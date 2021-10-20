using System;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Runtime.InteropServices;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using API.Data;
using API.DTOs.Stats;
using API.Interfaces;
using API.Interfaces.Services;
using Flurl.Http;
using Hangfire;
using Kavita.Common;
using Kavita.Common.EnvironmentInfo;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace API.Services.Tasks
{
    public class StatsService : IStatsService
    {
        private const string StatFileName = "app_stats.json";

        private readonly DataContext _dbContext;
        private readonly ILogger<StatsService> _logger;
        private readonly IUnitOfWork _unitOfWork;

#pragma warning disable S1075
        private const string ApiUrl = "http://stats.kavitareader.com";
#pragma warning restore S1075
        private static readonly string StatsFilePath = Path.Combine(DirectoryService.StatsDirectory, StatFileName);

        private static bool FileExists => File.Exists(StatsFilePath);

        public StatsService(DataContext dbContext, ILogger<StatsService> logger,
            IUnitOfWork unitOfWork)
        {
            _dbContext = dbContext;
            _logger = logger;
            _unitOfWork = unitOfWork;
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

            var rnd = new Random();
            var offset = rnd.Next(0, 6);
            if (offset == 0)
            {
                await SendData();
            }
            else
            {
                _logger.LogInformation("KavitaStats upload has been schedule to run in {Offset} hours", offset);
                BackgroundJob.Schedule(() => SendData(), DateTimeOffset.Now.AddHours(offset));
            }
        }

        /// <summary>
        /// This must be public for Hangfire. Do not call this directly.
        /// </summary>
        // ReSharper disable once MemberCanBePrivate.Global
        public async Task SendData()
        {
            await CollectRelevantData();
            await FinalizeStats();
        }

        public async Task RecordClientInfo(ClientInfoDto clientInfoDto)
        {
            var statisticsDto = await GetData();
            statisticsDto.AddClientInfo(clientInfoDto);

            await SaveFile(statisticsDto);
        }

        private async Task CollectRelevantData()
        {
            var usageInfo = await GetUsageInfo();
            var serverInfo = GetServerInfo();

            await PathData(serverInfo, usageInfo);
        }

        private async Task FinalizeStats()
        {
            try
            {
                var data = await GetExistingData<UsageStatisticsDto>();
                var successful = await SendDataToStatsServer(data);

                if (successful)
                {
                    ResetStats();
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "There was an exception while sending data to KavitaStats");
            }
        }

        private async Task<bool> SendDataToStatsServer(UsageStatisticsDto data)
        {
            var responseContent = string.Empty;

            try
            {
                var response = await (ApiUrl + "/api/InstallationStats")
                    .WithHeader("Accept", "application/json")
                    .WithHeader("User-Agent", "Kavita")
                    .WithHeader("x-api-key", "MsnvA2DfQqxSK5jh")
                    .WithHeader("api-key", "MsnvA2DfQqxSK5jh")
                    .WithHeader("x-kavita-version", BuildInfo.Version)
                    .WithTimeout(TimeSpan.FromSeconds(30))
                    .PostJsonAsync(data);

                if (response.StatusCode != StatusCodes.Status200OK)
                {
                    _logger.LogError("KavitaStats did not respond successfully. {Content}", response);
                    return false;
                }

                return true;
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

            return false;
        }

        private static void ResetStats()
        {
            if (FileExists) File.Delete(StatsFilePath);
        }

        private async Task PathData(ServerInfoDto serverInfoDto, UsageInfoDto usageInfoDto)
        {
            var data = await GetData();

            data.ServerInfo = serverInfoDto;
            data.UsageInfo = usageInfoDto;

            data.MarkAsUpdatedNow();

            await SaveFile(data);
        }

        private static async ValueTask<UsageStatisticsDto> GetData()
        {
            if (!FileExists) return new UsageStatisticsDto {InstallId = HashUtil.AnonymousToken()};

            return await GetExistingData<UsageStatisticsDto>();
        }

        private async Task<UsageInfoDto> GetUsageInfo()
        {
            var usersCount = await _dbContext.Users.CountAsync();

            var libsCountByType = await _dbContext.Library
                .AsNoTracking()
                .GroupBy(x => x.Type)
                .Select(x => new LibInfo {Type = x.Key, Count = x.Count()})
                .ToArrayAsync();

            var uniqueFileTypes = await _unitOfWork.FileRepository.GetFileExtensions();

            var usageInfo = new UsageInfoDto
            {
                UsersCount = usersCount,
                LibraryTypesCreated = libsCountByType,
                FileTypes = uniqueFileTypes
            };

            return usageInfo;
        }

        public static ServerInfoDto GetServerInfo()
        {
            var serverInfo = new ServerInfoDto
            {
                Os = RuntimeInformation.OSDescription,
                DotNetVersion = Environment.Version.ToString(),
                RunTimeVersion = RuntimeInformation.FrameworkDescription,
                KavitaVersion = BuildInfo.Version.ToString(),
                Culture = Thread.CurrentThread.CurrentCulture.Name,
                BuildBranch = BuildInfo.Branch,
                IsDocker = new OsInfo(Array.Empty<IOsVersionAdapter>()).IsDocker,
                NumOfCores = Environment.ProcessorCount
            };

            return serverInfo;
        }

        private static async Task<T> GetExistingData<T>()
        {
            var json = await File.ReadAllTextAsync(StatsFilePath);
            return JsonSerializer.Deserialize<T>(json);
        }

        private static async Task SaveFile(UsageStatisticsDto statisticsDto)
        {
            DirectoryService.ExistOrCreate(DirectoryService.StatsDirectory);

            await File.WriteAllTextAsync(StatsFilePath, JsonSerializer.Serialize(statisticsDto));
        }
    }
}
