using System;
using System.IO;
using System.Runtime.InteropServices;
using System.Text.Json;
using System.Threading.Tasks;
using API.Data;
using API.DTOs.Stats;
using API.Interfaces;
using API.Interfaces.Services;
using API.Services.Clients;
using Kavita.Common;
using Kavita.Common.EnvironmentInfo;
using Microsoft.Extensions.Logging;

namespace API.Services.Tasks
{
    public class StatsService : IStatsService
    {
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
        #region Communcation Methods
        private async Task CollectAndSendRelevantData()
        {
            _logger.LogDebug("Collecting server info");

            var data = await GetData();

            _logger.LogDebug("Sending data to the Stats server");

            await _client.SendDataToStatsServer(data);
        }

        #endregion


        #region Data Collection

        public async Task CollectAndSendStatsData()
        {
            var allowStatCollection = (await _unitOfWork.SettingsRepository.GetSettingsDtoAsync()).AllowStatCollection;
            if (!allowStatCollection)
            {
                _logger.LogDebug("User has opted out of stat collection, not registering tasks");
                return;
            }
            await CollectAndSendRelevantData();

        }

        private async ValueTask<InstallationStatsDto> GetData()
        {

            return new InstallationStatsDto
            {
                Os = RuntimeInformation.OSDescription,
                DotnetVersion = Environment.Version.ToString(),
                KavitaVersion = BuildInfo.Version.ToString(),
                InstallId = HashUtil.AnonymousToken(),
                IsDocker = new OsInfo(Array.Empty<IOsVersionAdapter>()).IsDocker
            };


        }


        public static ServerInfoDto GetServerInfo()
        {
            var serverInfo = new ServerInfoDto
            {
                Os = RuntimeInformation.OSDescription,
                DotnetVersion = Environment.Version.ToString(),
                KavitaVersion = BuildInfo.Version.ToString(),
                InstallId = HashUtil.AnonymousToken(),
                IsDocker = new OsInfo(Array.Empty<IOsVersionAdapter>()).IsDocker

            };

            return serverInfo;
        }
        #endregion
    }
}
