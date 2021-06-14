using System;
using System.Net.Http;
using System.Text.Json;
using System.Threading.Tasks;
using API.DTOs;

namespace API.Services.Clients
{
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
}