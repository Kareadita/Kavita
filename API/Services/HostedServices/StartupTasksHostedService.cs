using System;
using System.Threading;
using System.Threading.Tasks;
using API.Interfaces;
using API.Interfaces.Services;
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

            try
            {
                await ManageStartupStatsTasks(scope, taskScheduler);
            }
            catch (Exception)
            {
                //If stats startup fail the user can keep using the app
            }
        }

        private async Task ManageStartupStatsTasks(IServiceScope serviceScope, ITaskScheduler taskScheduler)
        {
            var unitOfWork = serviceScope.ServiceProvider.GetRequiredService<IUnitOfWork>();

            var settingsDto = await unitOfWork.SettingsRepository.GetSettingsDtoAsync();

            if (!settingsDto.AllowStatCollection) return;

            taskScheduler.ScheduleStatsTasks();

            var statsService = serviceScope.ServiceProvider.GetRequiredService<IStatsService>();

            await statsService.CollectAndSendStatsData();
        }

        public Task StopAsync(CancellationToken cancellationToken) => Task.CompletedTask;
    }
}