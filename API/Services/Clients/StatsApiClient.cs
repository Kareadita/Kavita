using System;
using System.Net.Http;
using System.Net.Http.Json;
using System.Threading.Tasks;
using API.DTOs.Stats;
using Flurl.Http;
using Kavita.Common.EnvironmentInfo;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;

namespace API.Services.Clients
{
    public class StatsApiClient
    {
        private readonly ILogger<StatsApiClient> _logger;
#pragma warning disable S1075
        private const string ApiUrl = "http://stats.kavitareader.com";
#pragma warning restore S1075

        public StatsApiClient(ILogger<StatsApiClient> logger)
        {
            _logger = logger;
        }

        public async Task<bool> SendDataToStatsServer(UsageStatisticsDto data)
        {
            var responseContent = string.Empty;

            try
            {
                var response = await (ApiUrl + "/api/Stats")
                    .WithHeader("Accept", "application/json")
                    .WithHeader("User-Agent", "Kavita")
                    .WithHeader("x-api-key", "MsnvA2DfQqxSK5jh")
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
                throw;
            }
            catch (Exception e)
            {
                _logger.LogError(e, "An error happened during the request to KavitaStats");
                throw;
            }

            return false;
        }
    }
}
