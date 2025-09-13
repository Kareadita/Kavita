using System;
using System.Threading;
using System.Threading.Tasks;
using API.Data;
using API.Services.Tasks.Scanner;
using Hangfire;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

namespace API.Services.HostedServices;
#nullable enable

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
        await taskScheduler.ScheduleTasks();
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

        try
        {
            var unitOfWork = scope.ServiceProvider.GetRequiredService<IUnitOfWork>();
            if ((await unitOfWork.SettingsRepository.GetSettingsDtoAsync()).EnableFolderWatching)
            {
                var libraryWatcher = scope.ServiceProvider.GetRequiredService<ILibraryWatcher>();
                // Push this off for a bit for people with massive libraries, as it can take up to 45 mins and blocks the thread
                BackgroundJob.Enqueue(() => libraryWatcher.StartWatching());
            }
        }
        catch (Exception)
        {
            // Fail silently
        }

    }

    public Task StopAsync(CancellationToken cancellationToken) => Task.CompletedTask;
}
