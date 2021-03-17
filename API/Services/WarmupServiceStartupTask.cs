using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using API.Interfaces.Services;
using Microsoft.Extensions.DependencyInjection;

namespace API.Services
{
    public class WarmupServicesStartupTask : IStartupTask
    {
        private readonly IServiceCollection _services;
        private readonly IServiceProvider _provider;
        public WarmupServicesStartupTask(IServiceCollection services, IServiceProvider provider)
        {
            _services = services;
            _provider = provider;
        }

        public Task ExecuteAsync(CancellationToken cancellationToken = default)
        {
            using var scope = _provider.CreateScope();
            foreach (var singleton in GetServices(_services))
            {
                Console.WriteLine("DI preloading of " + singleton.FullName);
                scope.ServiceProvider.GetServices(singleton);
            }

            return Task.CompletedTask;
        }
        
        static IEnumerable<Type> GetServices(IServiceCollection services)
        {
            return services
                .Where(descriptor => descriptor.ImplementationType != typeof(WarmupServicesStartupTask))
                .Where(descriptor => !descriptor.ServiceType.ContainsGenericParameters)
                .Select(descriptor => descriptor.ServiceType)
                .Distinct();
        }
    }

}