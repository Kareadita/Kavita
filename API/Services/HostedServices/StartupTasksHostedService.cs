using System;
using System.Threading;
using System.Threading.Tasks;
using API.Interfaces;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

namespace API.Services.HostedServices
{
    public class StartupTasksHostedService : IHostedService
    {
        private readonly IServiceProvider _provider;

        public StartupTasksHostedService(IServiceProvider serviceProvider)
        {
            _provider = serviceProvider;
        }

        public async Task StartAsync(CancellationToken cancellationToken)
        {
            using var scope = _provider.CreateScope();

            var taskScheduler = scope.ServiceProvider.GetRequiredService<ITaskScheduler>();
            taskScheduler.ScheduleTasks();
            taskScheduler.ScheduleUpdaterTasks();

            try
            {
                // These methods will automatically check if stat collection is disabled to prevent sending any data regardless
                // of when setting was changed
                await taskScheduler.ScheduleStatsTasks();
                await taskScheduler.RunStatCollection();
            }
            catch (Exception)
            {
                //If stats startup fail the user can keep using the app
            }
        }

        public Task StopAsync(CancellationToken cancellationToken) => Task.CompletedTask;
    }
}
