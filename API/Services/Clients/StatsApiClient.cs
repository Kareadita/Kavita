using System;
using System.Net.Http;
using System.Net.Http.Json;
using System.Threading.Tasks;
using API.DTOs.Stats;
using Microsoft.Extensions.Logging;

namespace API.Services.Clients
{
    public class StatsApiClient
    {
        private readonly HttpClient _client;
        private readonly ILogger<StatsApiClient> _logger;
        private const string ApiUrl = "http://stats.kavitareader.com";

        public StatsApiClient(HttpClient client, ILogger<StatsApiClient> logger)
        {
            _client = client;
            _logger = logger;
        }

        public async Task SendDataToStatsServer(UsageStatisticsDto data)
        {
            var responseContent = string.Empty;

            try
            {
                using var response = await _client.PostAsJsonAsync(ApiUrl + "/api/InstallationStats", data);

                responseContent = await response.Content.ReadAsStringAsync();

                response.EnsureSuccessStatusCode();
            }
            catch (HttpRequestException e)
            {
                var info = new
                {
                    dataSent = data,
                    response = responseContent
                };

                _logger.LogError(e, "The StatsServer did not respond successfully. {Content}", info);

                Console.WriteLine(e);
                throw;
            }
            catch (Exception e)
            {
                _logger.LogError(e, "An error happened during the request to the Stats Server");

                Console.WriteLine(e);
                throw;
            }
        }
    }
}
