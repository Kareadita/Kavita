using System;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using API.Data;
using API.DTOs.Stats;
using API.Interfaces;
using API.Interfaces.Services;
using API.Services.Clients;
using Kavita.Common;
using Kavita.Common.EnvironmentInfo;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace API.Services.Tasks
{
    public class StatsService : IStatsService
    {
        private const string StatFileName = "app_stats.json";

        private readonly StatsApiClient _client;
        private readonly DataContext _dbContext;
        private readonly ILogger<StatsService> _logger;
        private readonly IUnitOfWork _unitOfWork;

        public StatsService(StatsApiClient client, DataContext dbContext, ILogger<StatsService> logger,
            IUnitOfWork unitOfWork)
        {
            _client = client;
            _dbContext = dbContext;
            _logger = logger;
            _unitOfWork = unitOfWork;
        }

        private static readonly string StatsFilePath = Path.Combine(DirectoryService.StatsDirectory, StatFileName);
        private static bool FileExists => File.Exists(StatsFilePath);

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
                _logger.LogDebug("Finalizing Stats collection flow");

                var data = await GetExistingData<UsageStatisticsDto>();

                _logger.LogDebug("Sending data to the Stats server");
                await _client.SendDataToStatsServer(data);

                _logger.LogDebug("Deleting the file from disk");
                if (FileExists) File.Delete(StatsFilePath);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error Finalizing Stats collection flow");
                throw;
            }
        }

        public async Task Send()
        {
            var allowStatCollection = (await _unitOfWork.SettingsRepository.GetSettingsDtoAsync()).AllowStatCollection;
            if (!allowStatCollection)
            {
                _logger.LogDebug("User has opted out of stat collection, not registering tasks");
                return;
            }
            await CollectRelevantData();
            await FinalizeStats();
        }

        private async Task PathData(ServerInfoDto serverInfoDto, UsageInfoDto usageInfoDto)
        {
            var data = await GetData();

            data.ServerInfo = serverInfoDto;
            data.UsageInfo = usageInfoDto;

            data.MarkAsUpdatedNow();

            await SaveFile(data);
        }

        private async ValueTask<UsageStatisticsDto> GetData()
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
