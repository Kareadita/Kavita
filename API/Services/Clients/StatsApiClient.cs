using System;
using System.Net.Http;
using System.Net.Http.Json;
using System.Threading.Tasks;
using API.Configurations.CustomOptions;
using API.DTOs;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace API.Services.Clients
{
    public class StatsApiClient
    {
        private readonly HttpClient _client;
        private readonly StatsOptions _options;
        private readonly ILogger<StatsApiClient> _logger;

        public StatsApiClient(HttpClient client, IOptions<StatsOptions> options, ILogger<StatsApiClient> logger)
        {
            _client = client;
            _logger = logger;
            _options = options.Value ?? throw new ArgumentNullException(nameof(options));
        }

        public async Task SendDataToStatsServer(UsageStatisticsDto data)
        {
            var responseContent = string.Empty;

            try
            {
                var response = await _client.PostAsJsonAsync("/api/InstallationStatistics", data);

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