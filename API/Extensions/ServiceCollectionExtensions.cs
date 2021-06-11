using System;
using API.Interfaces.Services;
using API.Services;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace API.Extensions
{
    public static class ServiceCollectionExtensions
    {
        public static IServiceCollection AddStartupTask<T>(this IServiceCollection services)
            where T : class, IStartupTask
            => services.AddTransient<IStartupTask, T>();

        public static IServiceCollection AddStatsClient(this IServiceCollection services, IConfiguration configuration)
        {
            var url = configuration["StatsOptions:Url"];
            
            services.AddHttpClient<StatsApiClient>(client =>
            {
                client.BaseAddress = new Uri(url);
            });

            return services;
        }
    }
}