using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Text.Json;
using System.Threading.Tasks;
using API.Data;
using API.DTOs;
using Kavita.Common.EnvironmentInfo;
using Microsoft.EntityFrameworkCore;

namespace API.Services
{
    public interface IStatsService
    {
        Task PathData(UsageStatisticsDto data);
        Task PathData(ClientInfo clientInfo);

        Task FinalizeStats();

        // Task CollectRelevantData();
        Task<UsageStatisticsDto> CollectRelevantData();
    }

    public class StatsApiClient
    {
        private readonly HttpClient _client;

        public StatsApiClient(HttpClient client)
        {
            _client = client;
        }

        public Task SendDataToStatsServer(UsageStatisticsDto data)
        {
            return SendDataToStatsServer(JsonSerializer.Serialize(data));
        }

        public async Task SendDataToStatsServer(string data)
        {
            if (string.IsNullOrEmpty(data))
                throw new InvalidOperationException("The stats file is empty");

            var response = await _client.PostAsync("", new StringContent(data));

            response.EnsureSuccessStatusCode();
        }
    }

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

        public async Task PathData(UsageStatisticsDto data)
        {
            data.UpdateTime();

            if (File.Exists(FinalPath))
            {
                var existingData = await GetExistingData<UsageStatisticsDto>();
                //do stuff here

                await SaveFile(existingData);
            }

            await SaveFile(data);
        }

        public async Task PathData(ClientInfo clientInfo)
        {
            var statisticsDto = File.Exists(FinalPath)
                ? await GetExistingData<UsageStatisticsDto>()
                : new UsageStatisticsDto();

            statisticsDto.AddClientInfo(clientInfo);

            await SaveFile(statisticsDto);
        }

        public async Task FinalizeStats()
        {
            await _client.SendDataToStatsServer(await GetExistingData<UsageStatisticsDto>());
            DeleteFile(FinalPath);
        }

        // public async Task CollectRelevantData()
        public async Task<UsageStatisticsDto> CollectRelevantData()
        {
            var usersCount = await _dbContext.Users.CountAsync();

            var libsCountByType = await _dbContext.Library
                .AsNoTracking()
                .GroupBy(x => x.Type)
                .Select(x => new LibInfo {Type = x.Key, Count = x.Count()})
                .ToArrayAsync();

            var uniqueFileTypes = await GetFileExtensions();

            var serverInfo = GetServerInfo();

            var usageStats = new UsageStatisticsDto
            {
                ServerInfo = serverInfo,
                LibraryTypesCreated = libsCountByType,
                UsersCount = usersCount,
                FileTypes = uniqueFileTypes
            };

            return usageStats;
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

        private static ServerInfo GetServerInfo()
        {
            var appVersion = Assembly.GetEntryAssembly()
                ?.GetCustomAttribute<AssemblyInformationalVersionAttribute>()
                ?.InformationalVersion;

            var serverInfo = new ServerInfo
            {
                Os = RuntimeInformation.OSDescription,
                DotNetVersion = Environment.Version.ToString(),
                RunTimeVersion = RuntimeInformation.FrameworkDescription,
                KavitaVersion = appVersion ?? BuildInfo.Version.ToString(),
                Locale = RegionInfo.CurrentRegion.EnglishName,
                BuildBranch = ""
            };

            return serverInfo;
        }

        private static void DeleteFile(string path)
        {
            File.Delete(path);
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
            var dataJson = JsonSerializer.Serialize(statisticsDto);

            await File.WriteAllTextAsync(FinalPath, dataJson);
        }
    }
}