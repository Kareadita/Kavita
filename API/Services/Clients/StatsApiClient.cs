using System;
using System.Net.Http;
using System.Net.Http.Json;
using System.Threading.Tasks;
using API.Configurations.CustomOptions;
using API.DTOs;
using Microsoft.Extensions.Options;

namespace API.Services.Clients
{
    public class StatsApiClient
    {
        private readonly HttpClient _client;
        private readonly StatsOptions _options;

        public StatsApiClient(HttpClient client, IOptions<StatsOptions> options)
        {
            _client = client;
            _options = options.Value ?? throw new ArgumentNullException(nameof(options));
        }

        public async Task SendDataToStatsServer(UsageStatisticsDto data)
        {
            try
            {
                var response = await _client.PostAsJsonAsync("/api/UsageStatistics", data);

                var responseContent = await response.Content.ReadAsStringAsync();

                response.EnsureSuccessStatusCode();
            }
            catch (Exception e)
            {
                Console.WriteLine(e);
                throw;
            }
        }
    }
}