using System;
using System.Net.Http;
using System.Runtime.InteropServices;
using System.Threading.Tasks;
using API.DTOs.Stats;
using API.Interfaces;
using API.Interfaces.Services;
using Flurl.Http;
using Kavita.Common;
using Kavita.Common.EnvironmentInfo;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;

namespace API.Services.Tasks
{
    public class StatsService : IStatsService
    {
        private readonly ILogger<StatsService> _logger;
        private readonly IUnitOfWork _unitOfWork;

#pragma warning disable S1075
        private const string ApiUrl = "http://stats2.kavitareader.com"; // TODO: Change this to stats for Release
#pragma warning restore S1075

        public StatsService(ILogger<StatsService> logger,
            IUnitOfWork unitOfWork)
        {
            _logger = logger;
            _unitOfWork = unitOfWork;
        }

        private async Task CollectAndSendRelevantData()
        {
            var data = GetServerInfo();

            _logger.LogDebug("Sending data to the Stats server");


            await SendDataToStatsServer(data);
        }

        private async Task SendDataToStatsServer(ServerInfoDto data)
        {
            try
            {
                var response = await (ApiUrl + "/api/Stats")
                    .WithHeader("Accept", "application/json")
                    .WithHeader("User-Agent", "Kavita")
                    .WithHeader("x-api-key", "MsnvA2DfQqxSK5jh")
                    .PostJsonAsync(data);
                if (response.StatusCode != StatusCodes.Status200OK)
                {
                    _logger.LogError("KavitaStats did not respond successfully. {Content}", response);
                }
            }
            catch (HttpRequestException e)
            {
                _logger.LogError(e, "KavitaStats did not respond successfully");
            }
            catch (Exception e)
            {
                _logger.LogError(e, "An error happened during the request to KavitaStats");
            }
        }

        public static ServerInfoDto GetServerInfo()
        {
            return new ServerInfoDto
            {
                Os = RuntimeInformation.OSDescription,
                DotnetVersion = Environment.Version.ToString(),
                KavitaVersion = BuildInfo.Version.ToString(),
                InstallId = HashUtil.AnonymousToken(),
                IsDocker = new OsInfo(Array.Empty<IOsVersionAdapter>()).IsDocker
            };
        }


        /// <summary>
        /// If Data Collection is enabled, will Send information about this install to KavitaStats
        /// </summary>
        public async Task Send()
        {
            var allowStatCollection = (await _unitOfWork.SettingsRepository.GetSettingsDtoAsync()).AllowStatCollection;
            if (!allowStatCollection)
            {
                return;
            }
            await CollectAndSendRelevantData();

        }
    }
}
