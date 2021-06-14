using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Text.Json;
using System.Threading.Tasks;
using API.Data;
using API.DTOs;
using API.Interfaces.Services;
using API.Services.Clients;
using Kavita.Common.EnvironmentInfo;
using Microsoft.EntityFrameworkCore;

namespace API.Services
{
    public class StatsService : IStatsService
    {
        private const string TempFilePath = "kavita_tmp/";
        private const string TempFileName = "app_stats.json";

        private readonly StatsApiClient _client;
        private readonly DataContext _dbContext;

        public StatsService(StatsApiClient client, DataContext dbContext)
        {
            _client = client;
            _dbContext = dbContext;
        }

        private static string FinalPath => Path.Combine(Directory.GetCurrentDirectory(), TempFilePath, TempFileName);

        public async Task PathData(ClientInfoDto clientInfoDto)
        {
            var statisticsDto = File.Exists(FinalPath)
                ? await GetExistingData<UsageStatisticsDto>()
                : new UsageStatisticsDto {Id = Guid.NewGuid()};

            statisticsDto.AddClientInfo(clientInfoDto);

            await SaveFile(statisticsDto);
        }

        private static async Task PathData(ServerInfoDto serverInfoDto, UsageInfoDto usageInfoDto)
        {
            var data = File.Exists(FinalPath)
                ? await GetExistingData<UsageStatisticsDto>()
                : new UsageStatisticsDto {Id = Guid.NewGuid()};

            data.ServerInfoDto = serverInfoDto;
            data.UsageInfoDto = usageInfoDto;

            data.MarkAsUpdatedNow();

            await SaveFile(data);
        }

        public async Task FinalizeStats()
        {
            await _client.SendDataToStatsServer(await GetExistingData<UsageStatisticsDto>());

            DeleteFile(FinalPath);
        }

        private static void DeleteFile(string path)
        {
            if (File.Exists(path))
            {
                File.Delete(path);
            }
        }

        public async Task CollectRelevantData()
        {
            var usageInfo = await GetUsageInfo();

            var serverInfo = GetServerInfo();

            await PathData(serverInfo, usageInfo);
        }

        private async Task<UsageInfoDto> GetUsageInfo()
        {
            var usersCount = await _dbContext.Users.CountAsync();

            var libsCountByType = await _dbContext.Library
                .AsNoTracking()
                .GroupBy(x => x.Type)
                .Select(x => new LibInfo {Type = x.Key, Count = x.Count()})
                .ToArrayAsync();

            var uniqueFileTypes = await GetFileExtensions();

            var usageInfo = new UsageInfoDto
            {
                UsersCount = usersCount,
                LibraryTypesCreated = libsCountByType,
                FileTypes = uniqueFileTypes
            };

            return usageInfo;
        }

        private async Task<IEnumerable<string?>> GetFileExtensions()
        {
            var fileExtensions = await _dbContext.MangaFile
                .AsNoTracking()
                .Select(x => x.FilePath)
                .Distinct()
                .ToArrayAsync();

            var uniqueFileTypes = fileExtensions.Select(Path.GetExtension).Distinct();

            return uniqueFileTypes;
        }

        private static ServerInfoDto GetServerInfo()
        {
            var appVersion = Assembly.GetEntryAssembly()
                ?.GetCustomAttribute<AssemblyInformationalVersionAttribute>()
                ?.InformationalVersion;

            var serverInfo = new ServerInfoDto
            {
                Os = RuntimeInformation.OSDescription,
                DotNetVersion = Environment.Version.ToString(),
                RunTimeVersion = RuntimeInformation.FrameworkDescription,
                KavitaVersion = appVersion ?? BuildInfo.Version.ToString(),
                Culture = CultureInfo.CurrentCulture.Name,
                BuildBranch = ""
            };

            return serverInfo;
        }

        private static async Task<T> GetExistingData<T>()
        {
            var existingDataJson = await GetFileDataAsString();

            var existingData = JsonSerializer.Deserialize<T>(existingDataJson);

            return existingData;
        }

        private static async Task<string> GetFileDataAsString()
        {
            return await File.ReadAllTextAsync(FinalPath);
        }

        private static async Task SaveFile(UsageStatisticsDto statisticsDto)
        {
            var finalDirectory = FinalPath.Replace(TempFileName, string.Empty);
            if (!Directory.Exists(finalDirectory))
            {
                Directory.CreateDirectory(finalDirectory);
            }

            var dataJson = JsonSerializer.Serialize(statisticsDto);

            await File.WriteAllTextAsync(FinalPath, dataJson);
        }
    }
}