using System;
using API.Configurations.CustomOptions;
using API.Interfaces.Services;
using API.Services.Clients;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace API.Extensions
{
    public static class ServiceCollectionExtensions
    {
        public static IServiceCollection AddStartupTask<T>(this IServiceCollection services)
            where T : class, IStartupTask
            => services.AddTransient<IStartupTask, T>();

        public static IServiceCollection AddStatsOptions(this IServiceCollection services, IConfiguration configuration)
        {
            services.Configure<StatsOptions>(configuration.GetSection(nameof(StatsOptions)));

            return services;
        }

        public static IServiceCollection AddStatsClient(this IServiceCollection services, IConfiguration configuration)
        {
            var statsOptions = configuration
                .GetSection(nameof(StatsOptions))
                .Get<StatsOptions>();

            services.AddHttpClient<StatsApiClient>(client =>
            {
                client.BaseAddress = new Uri(statsOptions.ServerUrl);
                client.DefaultRequestHeaders.Add("api-key", statsOptions.ServerSecret);
            });

            return services;
        }
    }
}