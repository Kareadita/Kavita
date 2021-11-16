using System;
using System.Net.Http;
using System.Runtime.InteropServices;
using System.Threading.Tasks;
using API.DTOs.Stats;
using API.Entities.Enums;
using API.Interfaces;
using API.Interfaces.Services;
using Flurl.Http;
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
        private const string ApiUrl = "http://stats2.kavitareader.com";
#pragma warning restore S1075

        public StatsService(ILogger<StatsService> logger, IUnitOfWork unitOfWork)
        {
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
            var serverInfo = new ServerInfoDto
            {
                InstallId = installId.Value,
                Os = RuntimeInformation.OSDescription,
                KavitaVersion = BuildInfo.Version.ToString(),
                DotnetVersion = Environment.Version.ToString(),
                IsDocker = new OsInfo(Array.Empty<IOsVersionAdapter>()).IsDocker,
                NumOfCores = Math.Max(Environment.ProcessorCount, 1)
            };

            return serverInfo;
        }
    }
}
