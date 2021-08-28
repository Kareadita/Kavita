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
        private const string TempFilePath = "stats/";
        private const string TempFileName = "app_stats.json";

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

        private static string FinalPath => Path.Combine(Directory.GetCurrentDirectory(), TempFilePath, TempFileName);
        private static bool FileExists => File.Exists(FinalPath);

        public async Task PathData(ClientInfoDto clientInfoDto)
        {
            _logger.LogDebug("Pathing client data to the file");

            var statisticsDto = await GetData();

            statisticsDto.AddClientInfo(clientInfoDto);

            await SaveFile(statisticsDto);
        }

        private async Task CollectRelevantData()
        {
            _logger.LogDebug("Collecting data from the server and database");

            _logger.LogDebug("Collecting usage info");
            var usageInfo = await GetUsageInfo();

            _logger.LogDebug("Collecting server info");
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
                if (FileExists) File.Delete(FinalPath);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error Finalizing Stats collection flow");
                throw;
            }
        }

        public async Task CollectAndSendStatsData()
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
            _logger.LogDebug("Pathing server and usage info to the file");

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

        private async Task<T> GetExistingData<T>()
        {
            _logger.LogInformation("Fetching existing data from file");
            var existingDataJson = await GetFileDataAsString();

            _logger.LogInformation("Deserializing data from file to object");
            var existingData = JsonSerializer.Deserialize<T>(existingDataJson);

            return existingData;
        }

        private async Task<string> GetFileDataAsString()
        {
            _logger.LogInformation("Reading file from disk");
            return await File.ReadAllTextAsync(FinalPath);
        }

        private async Task SaveFile(UsageStatisticsDto statisticsDto)
        {
            _logger.LogDebug("Saving file");

            var finalDirectory = FinalPath.Replace(TempFileName, string.Empty);
            if (!Directory.Exists(finalDirectory))
            {
                _logger.LogDebug("Creating tmp directory");
                Directory.CreateDirectory(finalDirectory);
            }

            _logger.LogDebug("Serializing data to write");
            var dataJson = JsonSerializer.Serialize(statisticsDto);

            _logger.LogDebug("Writing file to the disk");
            await File.WriteAllTextAsync(FinalPath, dataJson);
        }
    }
}
